// Class member tables for the OSLANG parser.
//
// A CLASS body is a flat list of fields, methods and one constructor, parsed
// into these growable tables and then copied into the ClassDef. Split out of
// OslangKernel.cs (one partial class of Kernel) to keep that file navigable:
// the tables are needed in four places and the add/grow logic was triplicated.

internal static unsafe partial class Kernel
{
    // Fields, in declaration order. The index of a field in this list is its
    // slot in an instance, so the order must match the instance layout built
    // by OsEvalNewObj (which walks the class then its parents).
    private struct OsFieldTable
    {
        public char** Names;
        public int* Lens;
        public byte* Vis;
        public int Count;
        public int Cap;
    }

    // Methods, plus the constructor stored under the reserved name CONSTRUCTOR.
    private struct OsMethodTable
    {
        public char** Names;
        public byte* Vis;
        public byte* Flags;         // MF_VIRTUAL / MF_OVERRIDE
        public Stmt** Bodies;
        public int* BodyLens;
        public char*** ParamNames;
        public int** ParamLens;
        public int* ParamCounts;
        public int Count;
        public int Cap;
    }

    // The constructor is stored as a method under this reserved name so the call
    // path needs no special case.
    private const int OS_CTOR_NAME_LEN = 11;

    private static void OsFieldsInit(ref OsFieldTable t, int cap)
    {
        t.Names = (char**)OsAlloc(cap * sizeof(char*));
        t.Lens = (int*)OsAlloc(cap * sizeof(int));
        t.Vis = (byte*)OsAlloc(cap);
        t.Count = 0;
        t.Cap = cap;
    }

    private static void OsFieldsAdd(ref OsFieldTable t, char* name, int len, byte vis)
    {
        if (t.Count >= t.Cap)
        {
            int nc = t.Cap * 2;
            char** nn = (char**)OsAlloc(nc * sizeof(char*));
            int* nl = (int*)OsAlloc(nc * sizeof(int));
            byte* nv = (byte*)OsAlloc(nc);
            for (int i = 0; i < t.Count; i++) { nn[i] = t.Names[i]; nl[i] = t.Lens[i]; nv[i] = t.Vis[i]; }
            t.Names = nn; t.Lens = nl; t.Vis = nv; t.Cap = nc;
        }
        t.Names[t.Count] = name;
        t.Lens[t.Count] = len;
        t.Vis[t.Count] = vis;
        t.Count++;
    }

    private static void OsMethodsInit(ref OsMethodTable t, int cap)
    {
        t.Names = (char**)OsAlloc(cap * sizeof(char*));
        t.Vis = (byte*)OsAlloc(cap);
        t.Bodies = (Stmt**)OsAlloc(cap * sizeof(Stmt*));
        t.BodyLens = (int*)OsAlloc(cap * sizeof(int));
        t.ParamNames = (char***)OsAlloc(cap * sizeof(char**));
        t.ParamLens = (int**)OsAlloc(cap * sizeof(int*));
        t.ParamCounts = (int*)OsAlloc(cap * sizeof(int));
        t.Count = 0;
        t.Cap = cap;
    }

    private static void OsMethodsGrow(ref OsMethodTable t)
    {
        int nc = t.Cap * 2;
        char** nn = (char**)OsAlloc(nc * sizeof(char*));
        byte* nv = (byte*)OsAlloc(nc);
        Stmt** nb = (Stmt**)OsAlloc(nc * sizeof(Stmt*));
        int* nbl = (int*)OsAlloc(nc * sizeof(int));
        char*** npm = (char***)OsAlloc(nc * sizeof(char**));
        int** npl = (int**)OsAlloc(nc * sizeof(int*));
        int* npc = (int*)OsAlloc(nc * sizeof(int));
        for (int i = 0; i < t.Count; i++)
        {
            nn[i] = t.Names[i]; nv[i] = t.Vis[i];
            nb[i] = t.Bodies[i]; nbl[i] = t.BodyLens[i];
            npm[i] = t.ParamNames[i]; npl[i] = t.ParamLens[i]; npc[i] = t.ParamCounts[i];
        }
        t.Names = nn; t.Vis = nv; t.Bodies = nb; t.BodyLens = nbl;
        t.ParamNames = npm; t.ParamLens = npl; t.ParamCounts = npc;
        t.Cap = nc;
    }

    // Register one method. The name is copied out of the token buffer so it
    // stays valid for the life of the program; parameter names are copied the
    // same way because they become bindings in the method's scope.
    private static void OsMethodsAdd(ref OsMethodTable t, char* name, int nameLen,
                                     Stmt* body, int bodyLen,
                                     char** paramNames, int* paramLens, int pc, byte vis)
    {
        if (t.Count >= t.Cap) OsMethodsGrow(ref t);
        char* copy = (char*)OsAlloc((nameLen + 1) * sizeof(char));
        if (copy == null) return;   // arena exhausted; OsAlloc already raised
        for (int i = 0; i < nameLen; i++) copy[i] = name[i];
        copy[nameLen] = '\0';

        t.Names[t.Count] = copy;
        t.Vis[t.Count] = vis;
        t.Bodies[t.Count] = body;
        t.BodyLens[t.Count] = bodyLen;
        t.ParamCounts[t.Count] = pc;
        if (pc > 0)
        {
            char** names = (char**)OsAlloc(pc * sizeof(char*));
            int* lens = (int*)OsAlloc(pc * sizeof(int));
            if (names == null || lens == null) return;
            for (int i = 0; i < pc; i++)
            {
                names[i] = (char*)OsAlloc((paramLens[i] + 1) * sizeof(char));
                for (int j = 0; j < paramLens[i]; j++) names[i][j] = paramNames[i][j];
                names[i][paramLens[i]] = '\0';
                lens[i] = paramLens[i];
            }
            t.ParamNames[t.Count] = names;
            t.ParamLens[t.Count] = lens;
        }
        else
        {
            t.ParamNames[t.Count] = null;
            t.ParamLens[t.Count] = null;
        }
        t.Count++;
    }

    // Parse a `(a, b, c)` parameter list. The current token must be the '('.
    private static void OsParseParamList(out char** names, out int* lens, out int count)
    {
        int cap = 8;
        names = (char**)OsAlloc(cap * sizeof(char*));
        lens = (int*)OsAlloc(cap * sizeof(int));
        count = 0;
        while (!OsCheck(Tk.RParen) && !OsAtEnd())
        {
            Tok* pt = OsTakeName("parameter name");
            if (_osErr) return;
            if (count >= cap)
            {
                int nc = cap * 2;
                char** nn = (char**)OsAlloc(nc * sizeof(char*));
                int* nl = (int*)OsAlloc(nc * sizeof(int));
                for (int i = 0; i < count; i++) { nn[i] = names[i]; nl[i] = lens[i]; }
                names = nn; lens = nl; cap = nc;
            }
            names[count] = pt->T;
            lens[count] = pt->L;
            count++;
            if (OsCheck(Tk.Comma)) OsAdvance(); else break;
        }
        OsExpect(Tk.RParen, "Expected ')' after parameters.");
    }

    // ==================================================================
    //  Visibility (OSLANG 0.2)
    // ==================================================================
    // PUBLIC is visible everywhere; PRIVATE only inside the declaring class;
    // PROTECTED inside the declaring class and its descendants. Outside a
    // method body _osCurrentClassId is -1, so nothing but PUBLIC is reachable.
    private static bool OsVisible(int declClassId, byte vis)
    {
        byte v = (byte)(vis & 3);
        if (v == VIS_PUBLIC) return true;
        if (_osCurrentClassId < 0) return false;
        if (v == VIS_PRIVATE) return _osCurrentClassId == declClassId;
        // PROTECTED: walk up from the current class looking for the declarer.
        int id = _osCurrentClassId;
        while (id >= 0)
        {
            if (id == declClassId) return true;
            id = _osClasses[id].ParentClassId;
        }
        return false;
    }

    // Slot of a field in an instance: fields of the class come first, then the
// parent's, recursively. Returns -1 when the name is not declared.
// `declaring` receives the class that declares the field and `declIndex` its
// index inside that class (the visibility table is indexed by the latter).
    private static int OsObjectFieldIndex(int classId, char* name, int len, out int declaring, out int declIndex)
    {
        declaring = -1;
        declIndex = -1;
        int slot = 0;
        int id = classId;
        while (id >= 0)
        {
            ClassDef* cls = &_osClasses[id];
            for (int i = 0; i < cls->FieldCount; i++)
            {
                if (cls->FieldLens[i] == len && OsNameCI(cls->Fields[i], cls->FieldLens[i], name, len))
                {
                    declaring = id;
                    declIndex = i;
                    return slot;
                }
                slot++;
            }
            id = cls->ParentClassId;
        }
        return -1;
    }
}