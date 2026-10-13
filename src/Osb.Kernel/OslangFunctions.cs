// First-class functions and arrow functions (OSLANG 0.51 section 3).
//
//   DOUBLE = X => X * 2          ' a value in a variable
//   Numbers.MAP(X => X * 10)     ' an argument
//   Numbers.FILTER(X => X MOD 2 = 0)
//   Multiplier = 10
//   Multiply = X => X * Multiplier    ' captures Multiplier
//   () => MATH.RANDOM()                ' zero parameters
//   (A, B) => A + B                    ' several parameters
//
// A VKFunc value points at a Callable: either a reference to a declared
// FUNCTION (Named) or a lambda (parameter list plus body). Lambdas close over
// the scope they are *evaluated* in; the arena never frees, so the captured
// Scope survives after the defining frame has been popped, which is what lets a
// closure outlive its creator.
//
// Arguments are passed around as two scalars rather than an array: the freestanding
// CoreLib has no ThrowOverflowException, and the JIT emits exactly that check for
// an indexed stackalloc of a struct.
//
// Split out of OslangKernel.cs (one partial class of Kernel) to keep that file
// navigable.

internal static unsafe partial class Kernel
{
    // An anonymous function. Body is either an expression (Short != null) or a
    // statement list (Body) that ends with RETURN.
    private struct LambdaDef
    {
        public char** Params;
        public int* ParamLens;
        public int ParamCount;
        public Expr* Short;
        public Stmt* Body;
        public int BodyLen;
        public Scope* Captured;      // defining scope, for closures
    }

    // What a VKFunc value points at: a declared FUNCTION, or a lambda.
    private struct Callable
    {
        public FuncDef* Named;
        public LambdaDef* Lambda;
    }

    private static OslVal OsNamedFunc(FuncDef* f)
    {
        Callable* c = (Callable*)OsAlloc(sizeof(Callable));
        if (c == null) return OsNull();
        c->Named = f;
        c->Lambda = null;
        OslVal v = default;
        v.K = VKFunc;
        v.P = (ulong)c;
        return v;
    }

    private static OslVal OsLambdaFunc(LambdaDef* l)
    {
        Callable* c = (Callable*)OsAlloc(sizeof(Callable));
        if (c == null) return OsNull();
        c->Named = null;
        c->Lambda = l;
        OslVal v = default;
        v.K = VKFunc;
        v.P = (ulong)c;
        return v;
    }

    // Argument selector for the two-argument call shapes.
    private static OslVal OsArgAt(OslVal a, OslVal b, int i)
    {
        if (i == 0) return a;
        if (i == 1) return b;
        return OsNull();
    }

    // ---- calling ---------------------------------------------------------

    // Call a function value from a call expression. Arguments are still
    // unevaluated expressions.
    private static OslVal OsCallFuncValue(OslVal fn, Expr** args, int argc)
    {
        Callable* c = (Callable*)fn.P;
        if (c->Named != null) return OsCallFunction(c->Named, args, argc);

        LambdaDef* l = c->Lambda;
        if (_osCallDepth >= MaxCallDepth)
        {
            OsFail("Recursion too deep (limit reached).");
            return OsNull();
        }
        _osCallDepth++;

        OsPushScope();
        Scope* sc = (Scope*)_osScope;

        // Arguments are evaluated while the caller's scope is still the parent,
        // matching OsCallFunction: `f(v)` must be able to read the caller's `v`.
        for (int i = 0; i < l->ParamCount; i++)
        {
            OslVal v = (i < argc) ? OsEval(args[i]) : OsNull();
            if (_osErr) { OsPopScope(); _osCallDepth--; return OsNull(); }
            OsScopeAdd(sc, l->Params[i], l->ParamLens[i], v);
        }

        // OsRunLambdaBody pops the scope and the depth it was given.
        return OsRunLambdaBody(l, sc);
    }

    // Call a function value with one already-evaluated argument. This is what
    // the callback-taking array methods use.
    private static OslVal OsApplyCallable1(OslVal fn, OslVal a)
    {
        Callable* c = (Callable*)fn.P;
        if (c->Named != null) return OsCallNamedValues(c->Named, a, OsNull(), 1);

        LambdaDef* l = c->Lambda;
        if (_osCallDepth >= MaxCallDepth)
        {
            OsFail("Recursion too deep (limit reached).");
            return OsNull();
        }
        _osCallDepth++;
        OsPushScope();
        Scope* sc = (Scope*)_osScope;
        for (int i = 0; i < l->ParamCount; i++)
        {
            OsScopeAdd(sc, l->Params[i], l->ParamLens[i], i == 0 ? a : OsNull());
        }
        // OsRunLambdaBody pops the scope and the depth it was given.
        return OsRunLambdaBody(l, sc);
    }

    // Two already-evaluated arguments: REDUCE passes (accumulator, item).
    private static OslVal OsApplyCallable2(OslVal fn, OslVal a, OslVal b)
    {
        Callable* c = (Callable*)fn.P;
        if (c->Named != null) return OsCallNamedValues(c->Named, a, b, 2);

        LambdaDef* l = c->Lambda;
        if (_osCallDepth >= MaxCallDepth)
        {
            OsFail("Recursion too deep (limit reached).");
            return OsNull();
        }
        _osCallDepth++;
        OsPushScope();
        Scope* sc = (Scope*)_osScope;
        for (int i = 0; i < l->ParamCount; i++)
        {
            OsScopeAdd(sc, l->Params[i], l->ParamLens[i], OsArgAt(a, b, i));
        }
        return OsRunLambdaBody(l, sc);
    }

    // Shared tail: close over the defining scope and run the body. The scope is
    // already pushed and its parameters bound.
    private static OslVal OsRunLambdaBody(LambdaDef* l, Scope* sc)
    {
        // Captured variables stay reachable because the new scope's parent
        // becomes the scope the lambda was evaluated in.
        sc->Parent = (ulong)l->Captured;

        OslVal ret;
        if (l->Short != null)
        {
            ret = OsEval(l->Short);
            if (_osErr) ret = OsNull();
        }
        else
        {
            byte prev = _osCtl;
            _osCtl = CTL_NONE;
            OsExecBlock(l->Body, l->BodyLen);
            ret = (_osCtl == CTL_RETURN) ? _osRet : OsNull();
            _osCtl = prev;    // a lambda never ends the caller's control flow
        }

        OsPopScope();
        _osCallDepth--;
        return ret;
    }

    // Call a declared FUNCTION with already-evaluated arguments (the callback
    // path). Mirrors OsCallFunction's body, including the recursion guard.
    private static OslVal OsCallNamedValues(FuncDef* f, OslVal a, OslVal b, int argc)
    {
        if (_osCallDepth >= MaxCallDepth)
        {
            OsFail("Recursion too deep (limit reached).");
            return OsNull();
        }
        _osCallDepth++;
        OsPushScope();
        Scope* sc = (Scope*)_osScope;
        for (int i = 0; i < f->PC; i++)
        {
            OsScopeAdd(sc, f->P[i], f->PL[i], i < argc ? OsArgAt(a, b, i) : OsNull());
        }
        OsExecBlock(f->Body, f->BL);
        OslVal ret = (_osCtl == CTL_RETURN) ? _osRet : OsNull();
        if (_osCtl != CTL_EXCEPTION) _osCtl = CTL_NONE;
        OsPopScope();
        _osCallDepth--;
        return ret;
    }

    // ---- parsing ---------------------------------------------------------

    // Parameters are either a single bare name (`X => ...`) or a parenthesised
    // list (`(A, B) => ...`, `() => ...`); the opening '(' has been consumed.
    private static Expr* OsParseLambda(char* firstParam, int firstParamLen, bool sawParen)
    {
        LambdaDef* l = (LambdaDef*)OsAlloc(sizeof(LambdaDef));
        if (l == null) return null;

        int cap = 4;
        l->Params = (char**)OsAlloc(cap * sizeof(char*));
        l->ParamLens = (int*)OsAlloc(cap * sizeof(int));
        l->ParamCount = 0;

        if (firstParam != null)
        {
            l->Params[0] = firstParam;
            l->ParamLens[0] = firstParamLen;
            l->ParamCount = 1;
        }

        if (sawParen)
        {
            while (!OsCheck(Tk.RParen) && !OsAtEnd())
            {
                Tok* p = OsTakeName("parameter name");
                if (_osErr) return null;
                if (l->ParamCount >= cap)
                {
                    int nc = cap * 2;
                    char** np = (char**)OsAlloc(nc * sizeof(char*));
                    int* nl = (int*)OsAlloc(nc * sizeof(int));
                    for (int i = 0; i < l->ParamCount; i++) { np[i] = l->Params[i]; nl[i] = l->ParamLens[i]; }
                    l->Params = np; l->ParamLens = nl; cap = nc;
                }
                l->Params[l->ParamCount] = p->T;
                l->ParamLens[l->ParamCount] = p->L;
                l->ParamCount++;
                if (OsCheck(Tk.Comma)) OsAdvance();
                else break;
            }
            OsExpect(Tk.RParen, "Expected ')' after lambda parameters.");
            if (_osErr) return null;
        }

        if (!OsCheck(Tk.DoubleArrow)) { OsFail("Expected '=>' in arrow function."); return null; }
        OsAdvance(); // =>

        // Block form: `X =>` NEWLINE statements END (which must RETURN).
        if (OsCheck(Tk.Newline) || OsCheck(Tk.End) || OsAtEnd())
        {
            OsSkipNewlines();
            Stmt* body; int bl;
            OsParseBlock(out body, out bl, (int)Tk.End, (int)Tk.None, (int)Tk.None);
            if (_osErr) return null;
            OsExpect(Tk.End, "Expected END to close arrow function.");
            if (_osErr) return null;
            if (OsCheck(Tk.Function)) OsAdvance();  // optional END FUNCTION
            l->Short = null;
            l->Body = body;
            l->BodyLen = bl;
        }
        else
        {
            // The body may be a single statement rather than an expression:
            // `Numbers.FOREACH(X => PRINT X)` is the 0.51 section 11 example.
            if (OsIsStatementStart(OsCur()))
            {
                Stmt one = OsParseStatement();
                if (_osErr) return null;
                Stmt* oneBody = (Stmt*)OsAllocStmtArr(1);
                if (oneBody == null) return null;
                oneBody[0] = one;
                l->Short = null;
                l->Body = oneBody;
                l->BodyLen = 1;
            }
            else
            {
                l->Short = OsParseExpr();
                if (_osErr) return null;
                l->Body = null;
                l->BodyLen = 0;
            }
        }

        // Captured is filled in when the expression is evaluated, not here:
        // parsing happens before any scope exists.
        l->Captured = null;
        return OsMakeFuncExpr(OsLambdaFunc(l));
    }

    // A function value in expression position (the parser returns Expr*, not a
    // bare OslVal).
    private static Expr* OsMakeFuncExpr(OslVal v)
    {
        Expr* e = OsAllocExpr();
        if (e == null) return null;
        e->K = Ek.FuncRef;
        e->V = v;
        return e;
    }
}