# OSLANG 0.1 → 0.73 Gap Analysis

**Question answered here:** walking the language forward from 0.1 to 0.73, what is still
missing — in the reference implementation, in the bare-metal kernel port, and in the shell?

**Language reference:** [OSLANG-SPEC.md](OSLANG-SPEC.md)
**Latest spec:** 0.73 · **Reference implementation:** `src/Osb.Lang` (`Version = "0.72"`) ·
**Kernel port:** `src/Osb.Kernel/OslangKernel.cs` · **Host apps:** `src/Osb.Shell`

## Legend

| Mark | Meaning |
|---|---|
| ✅ | implemented and reachable |
| ⚠️ | implemented but broken / partial / divergent from spec |
| ❌ | missing |
| 🔄 | implemented against an older spec version, needs the 0.73 change |

Every claim below is backed by a file and line in the three code bases. "Verified" means
the code was read directly for this report.

---

## 0. Scoreboard

| Area | Spec | `src/Osb.Lang` | Kernel port | Notes |
|---|---|---|---|---|
| Lexical (comments, escapes, multiline, interpolation) | 0.1 / 0.61 | ⚠️ | ⚠️ | both miss triple-quote indentation stripping |
| Numbers / types | 0.1 | ⚠️ | ⚠️ | neither implements `DATE`/`TIME` (0.5) |
| Operators (`MOD`, `**`, `++`, `+=`, `|`) | 0.1 / 0.51 / 0.61 | ✅ | ⚠️ | kernel lexes `++`/`--` but never parses them |
| Control flow (`IF`/`WHILE`/`FOR`/`DO`/`BREAK`/`TRY`) | 0.1 | ✅ | ⚠️ | kernel `TRY/CATCH` cannot catch function errors |
| `SWITCH`/`CASE`/`DEFAULT` | 0.3 / 0.61 | ✅ | ❌ | |
| `FOR IN` / `FOR EACH` statements | 0.62 | ❌ | ❌ | neither has it |
| Functions (params, recursion, overloading) | 0.1 / 0.3 | ✅ | ⚠️ | kernel: no overloading, no arity check |
| Classes (fields, ctors, `NEW`, inheritance) | 0.2 | ⚠️ | ⚠️ | **kernel: bare assignment never writes a field** |
| `ME` implicit / `BASE` | 0.2 / 0.72 | ⚠️ | ⚠️ | both diverge on the read/write order |
| Visibility `PUBLIC`/`PRIVATE`/`PROTECTED` | 0.2 | ✅ | ❌ | kernel parses and discards it |
| `INTERFACE` | 0.2 | ⚠️ | ❌ | kernel: parsed then thrown away |
| `VIRTUAL`/`OVERRIDE`, `ABSTRACT`, `STATIC` | 0.3 | ❌ | ❌ | |
| `EVENT`/`ON`/`RAISE` | 0.3 | ❌ | ❌ | dead code in both |
| Generics | 0.3 | ❌ | ❌ | |
| Enums + enum sets (`|`) | 0.61 | ⚠️ | ⚠️ | |
| Builtin keyboard enum | 0.72 / 0.73 | 🔄 | 🔄 | name and registration both wrong for 0.73 |
| `USING` / multi-file | 0.3 / 0.6 | ✅ | ❌ | |
| `MATH` | 0.4 | ✅ | ❌ | kernel only has bare `ABS`/`SQRT`/… |
| `FILE` / `DIR` | 0.4 | ⚠️ | ⚠️ | both delegate I/O to the host |
| `STRING`/`NUMBER`/`BOOLEAN`/`ARRAY` methods | 0.4 / 0.51 | ✅ | ❌ | kernel has **no** method dispatcher |
| `OSL.I18N` | 0.6 | ✅ | ❌ | |
| `OSL.JSON` / `CSV` / `XML` / `CNF` | 0.62 | ⚠️ | ❌ | XML stringifier corrupts text |
| `OSL.CONSOLE` | 0.7 | ⚠️ | ❌ | kernel: 6 of 26 members |
| `OSL.APP` | 0.7 | ⚠️ | ⚠️ | |
| `OSB.NET` | 0.62 | ✅ | ❌ | |
| `DATE` / `TIME` | 0.5 | ❌ | ❌ | **not implemented anywhere** |
| Arrow functions / closures | 0.51 | ✅ | ❌ | |
| `SHOW`, `MOD`, `+=`, postfix `++` | 0.51 | ✅ | ⚠️ | |
| Enum auto-increment after an explicit value | 0.61 | ⚠️ | ✅ | host bug, kernel correct |
| `EXIT()` | 0.7 | ❌ | ⚠️ | kernel has `OSL.APP.EXIT`; host relies on the app |
| Error taxonomy (4 categories, `ERR` message) | 0.1 | ⚠️ | ⚠️ | |

**One-line verdict.** The reference implementation covers most of 0.1–0.7 language surface
and is a solid 0.72, but is still missing `DATE`/`TIME`, `VIRTUAL`/`OVERRIDE`, events,
generics, `FOR IN`, and several 0.73 rules. The kernel port is a *sketch* of that: it can
parse and run straight-line scripts, but arrays, OOP and `TRY/CATCH` are broken in ways
that no script exercises, and the interpreter is not even reachable from the shell.

---

## 1. Blockers: the kernel port cannot be exercised at all

These six are not "missing features", they are holes in what already exists. Fix them
before adding any 0.7x feature, because nothing can be validated until they are fixed.

**B1 — The interpreter is unreachable from the shell.**
`OslangCmd(char*, int)` is defined at `OslangKernel.cs:3715` and referenced nowhere.
`Shell/Shell.cs` dispatches `VER ABOUT HELP CLS ECHO SET DIR CD TYPE MEM HALT POWEROFF
REBOOT TIME DATE TICKS SLEEP DEMO TASK KILL WAIT TAB SPAWN` — there is no `OSLANG`
command, so the whole interpreter and every `.OSL` file seeded into `A:` is dead weight.
*Fix:* add `OSLANG <script> [args…]` to `Shell.Execute`, and make the argument parse use the
received `len` instead of the hard-coded `i = 6` at `:3719`.

**B2 — A bare assignment inside a method never writes the class field.**
`OsAssignTarget` (`:2484`) sends every `Ek.Id` target to `OsAssign` (`:2228`), which only
touches *scope* variables (`OsFindVar`, else `OsScopeAdd`). Class fields live in the object
block, not in any scope, and no code path converts a bare-identifier assignment into
`OsSetObjectField`. Reads do resolve to fields (`:2566`), so the result is: `ME.NAME = "x"`
then `PRINT NAME` prints `NULL`, while `NAME = "x"` silently creates a local that shadows
the field. Every class script is affected. This is also the *same* rule 0.73 changes
(deliberately) for locals/parameters — so the fix belongs in one place: resolve the write
target through the full 0.73 order.

**B3 — Array literals always report zero elements.**
`OsArrMake` (`:620`) writes capacity at `+0` and count at `+4`, initialising count to `0`.
`OsEvalArr` (`:3266`) fills `OsArrElem(arr)[i]` for every element but never updates the
count word. So `[1, 2, 3]` has `count == 0`: every index read/write fails with
"Array index out of range.", `COUNT`/`LEN` return 0, and `PRINT arr` prints nothing
(`OsPrintVal` has no array case). Nested arrays inherit the same bug.

**B4 — `TRY/CATCH` cannot catch an error raised inside a function or method.**
`OsCallFunction` (`:2266`) runs the body and then unconditionally `_osCtl = CTL_NONE`
(`:2268`); `OsCallMethod` does the same at `:3093`. `Sk.TryCatch` (`:2369`) only handles
`CTL_EXCEPTION`, so an error raised inside a call reaches the enclosing `TRY` as
`CTL_NONE` — the `CATCH` block never runs and the script dies. `OsFailName` (`:275`),
`OsFail2` (`:2472`) and the setter inside `OsNums` (`:2923`) also fail to set
`CTL_EXCEPTION`, so those errors are uncatchable even without a call frame. `OsAlloc`
failure is worse: `OsFail` allocates, so an out-of-memory inside it cannot even record the
error.

**B5 — The builtin `KEY` enum declares 35 members and writes 28.**
`OsDefineKeyEnum` sets `MemberCount = 35` (`:3814`) and assigns members `0..27`
(`:3826-3880`); slots 28–34 hold whatever `OsAlloc` returned. Four loops iterate
`MemberCount` and dereference those pointers (`OsPrintVal :571`, `OsDisplayLen :493`,
`OsWriteDisplay :528`, `OsEvalMember :3223`) → wild pointer reads, and a page fault would
kill the kernel outright. Also: 24 non-special keys are absent, and the enum values are
set-1 scancodes rather than the spec's arbitrary numbering.

**B6 — The script size cap counts characters against a byte arena.**
`OslangCmd` passes `(int)(_osHeapSize / 2)` as the character cap (`:3757`) — 2 MiB of
*chars* against a 4 MiB *byte* arena — then adds `(srcLen + 1) * 2` bytes at `:3759` with no
bounds check. A script of ~1 MiB (512 Ki chars) already doubles past the arena end, which
is unmapped. Needs a real `if (_osHeapUsed + needed > _osHeapSize)` guard, and the cap
should be expressed in bytes.

**B7 — (robustness) No recursion limit.** Each OSLANG frame costs ~408 arena bytes plus
several native frames; runaway recursion exhausts the arena or overflows the CPU stack
with no guard. A depth counter in `OsCallFunction` is a few lines.

---

## 2. Version by version

For each version: what it introduced, where it stands today, and what is left.

### 0.1 — core language ✅ (host) / ⚠️ (kernel)
`IF/ELIF/ELSE`, `WHILE`, `DO WHILE`, `FOR…TO…STEP`, `BREAK`, `CONTINUE`, `TRY/CATCH`,
`RETURN`, `VAR`, `GLOBAL`, `PRINT`, `INPUT`, `CLEAR`, `STR/NUMBER/BOOL`, `COUNT`,
`TYPEOF`, short-circuit `AND/OR/NOT`, four error categories.

- Host: complete. Missing only the *global* `SQRT/ABS/POW/FLOOR/CEIL` calls — they are
  keywords the parser accepts but `StandardLibrary.FunctionNames` (13 names) never
  dispatches, so `SQRT(9)` throws "Unknown standard library function" (correct per 0.4,
  which removed them, but the keywords should be deleted from the parser).
- Kernel: statements all parse and run; `VAR x TYPE` annotation is not supported (`VAR x N`
  silently swallows the type token); `++`/`--` tokens are lexed and never parsed; the four
  error categories collapse into one untyped message with no source location.

### 0.2 — OOP ⚠️ / ⚠️
`CLASS`, `NEW`, `CONSTRUCTOR`, single inheritance, `ME`, visibility, `INTERFACE`.

- Host: works, except that the *strict* interface checker (`Runtime/SemanticAnalyzer.cs`,
  which validates visibility and signatures up the base chain) is **dead code** — only
  `Compilation.SemanticAnalyzer.Validate` runs, and it checks names on the class's own
  members only, ignoring inherited ones and visibility.
- Kernel: B2 (assignment) and the missing visibility enforcement; interfaces are parsed into
  `_osInterfaces` and never read (`:1494` writes, nothing reads); method dispatch searches
  only the object's own class (`:3018`), so **inherited methods are unreachable**; the
  parent constructor is only called when the child does not define one.

### 0.3 — multi-file, `SWITCH`, events, generics, overloading ⚠️ / ⚠️
`USING`, `SWITCH`/`CASE`/`DEFAULT` (statement *and* expression form), `EVENT`/`ON`/`RAISE`,
generics, overloading, `VIRTUAL`/`OVERRIDE`, `EXTENDS`, declaration order independence.

- Host: `SWITCH` ✅, function overloading ✅ (`FunctionOverloadSet.Resolve`), `USING` ✅.
  **Events, generics and `VIRTUAL`/`OVERRIDE` are missing**: `Runtime/Events/` and
  `Runtime/Generics/` are unreferenced directories, `VirtualMethodDecl`/`OverrideMethodDecl`
  exist only as AST records that are never constructed, and there is no `EXTENDS` keyword.
  Also: `DO WHILE` is implemented as a pre-test loop (a deliberate but permanent footgun).
- Kernel: none of this except `USING`-less scripts. No `SWITCH`, no `ON`/`RAISE` (`EVENT`
  at top level explicitly fails with "Events are not supported in this OSLANG subset",
  `:911`), no overloading (`OsFindFunc` returns the first name match), no `BASE` (the
  keyword is lexed at `:815` and never parsed).

### 0.4 — standard library ⚠️ / ⚠️
`MATH`/`FILE`/`DIR` namespaces; methods on `STRING`/`NUMBER`/`BOOLEAN`/`ARRAY`; chaining;
named-function callbacks; file streams. **Breaking:** bare math functions removed.

- Host: `MATH` ✅ (25 members), primitive-method dispatcher ✅ (23 `STRING`, 7 `NUMBER`,
  2 `BOOLEAN`, 25 `ARRAY`). Defects: `ARRAY.ADD` does not update `ElementType` while
  `PUSH`/`INSERTAT` do; `ARRAY.SORT` throws a raw `InvalidOperationException` for
  non-number/string elements, which `TRY/CATCH` cannot catch.
  `FILE`/`DIR` are pure extension passthrough — `Osb.Lang` alone implements none of them.
- Kernel: **no method dispatcher at all** (only `.LENGTH`/`.COUNT`, `:3154`), and calling
  any other method is a hard parse/eval error ("Cannot call this expression."). No `MATH.*`
  namespace — `MATH.SQRT(4)` fails because namespace detection only fires for a leftmost
  `OSL` (`:2986`). `PRINT arr` / `SHOW obj` print nothing.

### 0.5 — `DATE` / `TIME` ❌ / ❌
New primitive types, `DATE`/`TIME` namespaces, chronological comparison, extended `TYPEOF`.

- **Not implemented in either implementation.** Zero occurrences of `DATE`/`TIME` in
  `src/Osb.Lang`. This is the one whole version nobody has started.

### 0.51 — `SHOW`, operators, arrow functions, nested arrays ✅ / ⚠️
`SHOW`, `MOD`, `**`, postfix `++`/`--`, `+=`, `=>` arrow functions and closures, nested
arrays, `FINDINDEX/FOREACH/PUSH/POP/FLAT/FLATMAP`, `MATH` trigonometry, `PADSTART/PADEND/
REPEAT`, `TYPEOF` formalised.

- Host: complete.
- Kernel: `SHOW` ✅, `MOD`/`**` ✅, `+=` ✅, nested arrays parse ✅ — but B3 makes all of
  them empty at runtime. `++`/`--` are lexed and never parsed. Arrow functions absent
  (`Tk.DoubleArrow` is produced at `:759` and never consumed).

### 0.6 — the `OSL` standard-library concept ⚠️ / ❌
Reserved `OSL` namespace, `USING OSL.I18N`, `.I18N` resource files, 13-member I18N API,
the Core/Stdlib/Extensions layering rule.

- Host: ✅ (13 members in `I18nNamespace`), with one wiring gap: `I18nNamespace.Call`
  accepts a `basePath` the interpreter never supplies, so only the process base directory
  is searched.
- Kernel: ❌ — no `USING` enforcement, no I18N, and no notion of library modules.

### 0.61 — enums, `SWITCH`, strings ⚠️ / ⚠️
`ENUM/END ENUM`, enum sets via `|`, `BREAK` inside `SWITCH`, escapes, triple-quoted strings
with indentation stripping, `${}` interpolation, enum `NAME()/VALUE()/TOSTRING()`.

- Host: enums ✅, enum sets ✅, interpolation ✅ (evaluated at runtime by re-parsing the
  hole). Defects: **auto-increment ignores explicit values** (`A = 5, B` yields `B = 1`,
  `Interpreter.Execution.Enums.cs:22-26`); `VALUE()` returns a *string* and is identical to
  `TOSTRING()`; enum-type-level members (`KEY.COUNT()`) are unreachable; triple-quoted
  strings do **not** strip common indentation.
- Kernel: enums ✅ but numeric-only values (string enums unsupported), no `END`-token
  tolerance issues, no enum methods at all, no `TYPEOF` for enum values (`VKEnum` falls
  through to `"UNKNOWN"` at `:3577`), no triple-quoted strings, `IsFlags` is dead code
  (`:1343-1347` is an empty `if`), interpolation ✅ (parse-time, with a 256-token sub-buffer
  per hole that is never reclaimed).

### 0.62 — JSON/CSV/XML/CNF/NET ⚠️ / ❌
`OSL.JSON` (5 members), `OSL.CSV` (4), `OSL.XML` (10), `OSL.CNF` (8), `OSB.NET` (2).

- Host: present. Defects: **`OSL.XML` stringification is broken** — `EscapeXml`
  (`OslXmlNamespace.cs:124`) re-appends the original characters instead of entities, so
  writing any text containing `& < > " '` produces invalid XML; `ToDisplayString`
  (`Conversions.cs:55`) throws `ArgumentOutOfRangeException` for module/function/key/JSON/
  XML/CNF values, so `PRINT OSL.JSON.PARSE("{}")` crashes with an exception `TRY/CATCH`
  cannot catch; `OSL.CNF` has no way to create an empty config (only `READ` produces one).
- Kernel: ❌ entirely (correctly so — there is no writable filesystem yet).

### 0.7 — terminal applications ⚠️ / ⚠️
`OSL.CONSOLE` (26 members), `OSL.APP`, the key event object, 1-based coordinates,
`GETKEY` blocking / `READKEY` non-blocking.

- Host: dispatches colours and key constants itself and forwards everything else to the
  host extension; the two tables disagree (console constants have no `UNKNOWN`, the enum
  does). `Osb.Lang` standalone ships no console at all — `Osb.Shell/Kernel/ConsoleHost.cs`
  supplies it.
- Kernel: 6 of 26 — `PRINT`, `SHOW`, `CLEAR`, `INPUT`/`READ`, `WRITE`, `GETKEY`. The other
  20 have no backing support: `Hardware/Vga.cs` only does `Clear/Scroll/PutChar/Write*`, so
  there is no cursor, no geometry, no colour and no frame concept to build them on.
  `GETKEY` is also wrong in three ways: serial input always yields `KEY.UNKNOWN` (`sc`
  stays `-1`), the CHAR filter compares *ASCII* against *scancode* constants so the digits
  `6 8 9` and `*` are silently swallowed (`:3420`), and each call leaks a 5-field object
  into the arena.

### 0.72 — implicit `ME`, typed keys, enum flags ⚠️ / ⚠️
Implicit `ME` for reads *and* calls, the four-step resolution order, `KEY` enum, typed
`Key.KEY`, `|` combination.

- Host: implicit `ME` works for reads and calls. The 0.72 **order** is not what ships:
  reads consult *globals before* the class property (`EvalIdentifier`,
  `Interpreter.Expressions.Core.cs:107`), writes consult the property first
  (`ExecuteAssign`, `Interpreter.Execution.Variables.cs:148`). So `GLOBAL X = 1` +
  `PUBLIC X` makes `PRINT X` read the global and `X = 2` write the property. Property
  writes also bypass `TypeSystem.Assign`, so property types are never stabilised.
- Kernel: implicit `ME` is a hack — `OsCallMethod` injects two arena strings `"ME"` and
  `"THIS"` into the scope (`:3056`) and resolution finds them by linear scan, so a script
  variable named `me` shadows it, and only two levels of parent chain are ever walked.

### 0.73 — clean enum model, predictable shadowing 🔄 / 🔄
The four breaking changes, current state:

| 0.73 change | `src/Osb.Lang` | Kernel | `src/Osb.Shell` + KISS |
|---|---|---|---|
| `KEY` → **`KEYCODE`** | 🔄 `KEY` (and the `KEYCODE` alias does not exist) | 🔄 `KEY` | **already `KEYCODE`** |
| Enum types in a dedicated registry, not globals | ✅ (`_enumTypes`, resolved at read step 5) | ⚠️ separate list, but resolution order is scope → class → parent → enum | n/a |
| Bare assignment respects local/parameter shadowing | ⚠️ write order is right; read order is not | ❌ (B2) | n/a |
| Legacy `OSL.CONSOLE.*` constants return enum values | ❌ still numbers | ❌ not present | n/a |
| Full resolution order (local → class → globals → enums → functions → classes) | ⚠️ read diverges (globals before class, enums after functions); classes/interfaces are not in the search path at all | ⚠️ parent before enums | n/a |

> **Immediate consequence of the rename in `b78a24b`.** The spec's answer is `KEYCODE`, but
> that commit renamed the host enum `KEYCODE` → `KEY`. The shell and its apps were left
> behind: `src/Osb.Shell/Kernel/ConsoleHost.cs:311` still builds `EnumValue(…, "KEYCODE", …)`
> and `APPS/KISS/main.osl` / `APPS/CONFIG/main.osl` use `CASE KEYCODE.ESC`, `Key.KEY =
> KEYCODE.ENTER`. After that commit those scripts fail with `Undefined variable 'KEYCODE'`
> at runtime. No test covers them, which is why CI stayed green. Moving to 0.73 fixes this
> by adopting the spec's name — the rename should be completed, not reverted.

---

## 3. Feature matrix: what each implementation is missing

Legend: ✅ works · ⚠️ works with defects · ❌ missing.

### Language

| Feature | Spec | Host | Kernel |
|---|---|---|---|
| `SWITCH`/`CASE`/`DEFAULT` + `BREAK` inside | 0.61 | ✅ | ❌ |
| `FOR IN` / `FOR EACH` statement | 0.62 | ❌ | ❌ |
| Arrow functions, closures | 0.51 | ✅ | ❌ |
| Postfix `++`/`--` | 0.51 | ✅ | ❌ (lexed only) |
| `VIRTUAL`/`OVERRIDE`, `ABSTRACT`, `STATIC` | 0.3 | ❌ | ❌ |
| `EVENT`/`ON`/`RAISE` | 0.3 | ❌ (dead code) | ❌ (explicit refusal) |
| Generics | 0.3 | ❌ (dead code) | ❌ |
| Function overloading | 0.3 | ✅ | ❌ |
| Default parameter values | — | ❌ | ❌ |
| `BASE(...)` | 0.2 | ✅ | ❌ (lexed only) |
| Visibility enforcement | 0.2 | ✅ | ❌ (parsed, discarded) |
| Interface enforcement | 0.2 | ⚠️ (names only) | ❌ (parsed, discarded) |
| Inherited method dispatch | 0.2 | ✅ | ❌ (own class only) |
| Multi-level inheritance (grandparent fields) | 0.2 | ✅ | ❌ (2 levels) |
| Enum string underlying type | 0.61 | ✅ | ❌ |
| Enum auto-increment after explicit value | 0.61 | ⚠️ | ✅ |
| Enum methods (`NAME`/`VALUE`/`CONTAINS`/…) | 0.61 | ⚠️ | ❌ |
| Enum sets (`.CONTAINS`, `.COUNT`, `.FOREACH`) | 0.61 | ✅ | ❌ |
| `TYPEOF` for enums | 0.61 | ✅ | ❌ |
| Triple-quoted strings + indentation stripping | 0.61 | ⚠️ | ❌ |
| `DATE`/`TIME` types and namespaces | 0.5 | ❌ | ❌ |
| `EXIT()` | 0.7 | ❌ | ⚠️ (`OSL.APP.EXIT`) |
| 4-category error taxonomy with source location | 0.1 | ⚠️ | ⚠️ |
| `USING` + multi-file module resolution | 0.3 | ✅ | ❌ |

### Standard library

| Namespace | Members | Host | Kernel |
|---|---|---|---|
| `MATH` | 25 | ✅ | ❌ (7 bare globals only) |
| `FILE` / `OSL.FILE` | 15 + streams | ⚠️ host-provided | ⚠️ read-only 3 |
| `DIR` | 10 | ⚠️ host-provided | ❌ |
| `OSL.CONSOLE` | 26 | ⚠️ host-provided | ⚠️ 6 |
| `OSL.APP` | 3 | ⚠️ host-provided | ⚠️ 3 |
| `OSL.I18N` | 13 | ✅ | ❌ |
| `OSL.JSON` | 5 | ⚠️ | ❌ |
| `OSL.CSV` | 4 | ⚠️ | ❌ |
| `OSL.XML` | 10 | ⚠️ (escaping broken) | ❌ |
| `OSL.CNF` | 8 | ⚠️ | ❌ |
| `OSB.NET` | 2 | ✅ | ❌ |
| `DATE` / `TIME` | 8 + 6 | ❌ | ❌ |
| `STRING` methods | 23 | ✅ | ❌ |
| `NUMBER` methods | 7 | ✅ | ❌ |
| `BOOLEAN` methods | 2 | ✅ | ❌ |
| `ARRAY` methods | 25 | ⚠️ | ❌ |

---

## 4. Defects to fix in `src/Osb.Lang` (ordered by impact)

1. **`OSL.XML.EscapeXml` is a no-op** (`Runtime/OslXmlNamespace.cs:124`) — emit
   `&amp; &lt; &gt; &quot; &apos;`.
2. **`Conversions.ToDisplayString` throws for 8 value kinds** (`Runtime/Conversions.cs:55`)
   — every `PRINT`/`SHOW`/interpolation of a module, function, key event, JSON, XML or CNF
   value dies with an uncatchable `ArgumentOutOfRangeException`.
3. **Read/write resolution asymmetry** (`Interpreter.Expressions.Core.cs:107` vs
   `Interpreter.Execution.Variables.cs:148`) — reorder reads to local → parameter → current
   class → parent class → globals → enum types → functions → classes/interfaces, i.e. the
   0.73 §11–18 table. This also fixes the KISS-case `Key` variable.
4. **Enum auto-increment ignores explicit values** (`Interpreter.Execution.Enums.cs:22`).
5. **Enum `VALUE()`/`TOSTRING()` semantics** — `VALUE()` should return the underlying
   number, `TOSTRING()` the member name.
6. **`ARRAY.ADD` does not set `ElementType`**; **`ARRAY.SORT` throws a non-OSLANG
   exception** (`PrimitiveMethodDispatcher.cs:193`, `:484`).
7. **`Runtime/SemanticAnalyzer.cs`, `Runtime/Events/`, `Runtime/Generics/` are dead code**
   — either wire the interface checker up or delete the directories so the gap is honest.
8. **Dead math keywords** — `SQRT/ABS/POW/FLOOR/CEIL` parse as calls but have no dispatch.
9. **Interpolation is re-parsed on every evaluation**; no nested braces; `start > 0` guard.
10. **Two divergent keyboard tables** (`RegisterBuiltinKeyEnum` vs the hard-coded console
    constants in `Interpreter.Calls.cs:133`).
11. **`OSL.CNF` cannot create an empty config.**

## 5. Defects to fix in `src/Osb.Kernel/OslangKernel.cs`

1. **B1 no `OSLANG` shell command** — the interpreter is unreachable.
2. **B2 bare assignment never reaches class fields**; inherited methods unreachable;
   visibility discarded; interfaces discarded; two-level chain limit.
3. **B3 array literals have count 0**; no array methods; `PRINT arr` prints nothing.
4. **B4 `CATCH` cannot catch errors from calls**; `OsFailName`/`OsFail2`/`OsNums` do not set
   `CTL_EXCEPTION`; `OsAlloc` failure cannot record an error.
5. **B5 `KEY` enum: `MemberCount = 35` with 28 members written** — wild pointer reads.
6. **B6 script-size cap in chars against a byte arena** — out-of-bounds write.
7. **B7 no recursion limit.**
8. `TYPEOF` has no `VKEnum` case (`:3577`); `PRINT`/`SHOW` have no array/object case
   (`:505`, `:590`).
9. `GETKEY`: serial input yields `KEY.UNKNOWN`; the CHAR filter swallows `6 8 9 *` (`:3420`);
   the key-event object leaks per call; 24 keys missing.
10. `OsEvalMember` returns a bogus method reference when the first field is NULL
    (`:3199` — the method name is never compared).
11. Method dispatch compares method names **case-sensitively** (`:3029`), unlike everything else.
12. `OsAssign` silently creates a variable for an unknown name — no diagnostic.
13. No `SWITCH`, no arrow functions, no `MATH.*`, no primitive methods, no `USING`
    enforcement, no string-enum values, and no rounding in number formatting (12 truncated
    fractional digits with trailing zeros stripped, `OsFormatDouble:420` — so `0.1 + 0.2`
    prints `0.3`, but `1 / 3` prints `0.333333333333` and large magnitudes lose precision).
14. Dead code to remove or wire up: `Ek.EnumVal`, `Ek.ClassRef`, `Sk.ClassDecl/EnumDecl/
    MethodDecl/AddAssign/Nop`, `Tk.PlusPlus/MinusMinus/DoubleArrow/LBrace/RBrace/Base`,
    `_osInterfaces`, `IsFlags`, `_osCurrentEnumId`, the empty loops at `:2584` and `:3181`.

---

## 6. Recommended path to 0.73

**Phase 0 — make the kernel interpreter testable** (nothing else can be validated first)
B1 (wire the `OSLANG` command, add the seeded `.OSL` scripts to the boot smoke test) ·
B5 (fix `MemberCount`) · B6 (byte-based cap + guard) · B7 (recursion depth).

**Phase 1 — fix the three features the sample scripts depend on**
B2 (write resolution, implemented directly in the 0.73 order so Phase 3 is free) · B3 (array
count) · B4 (propagate `CTL_EXCEPTION` out of calls; make every `OsFail*` set it).

**Phase 2 — close 0.1 gaps in the kernel**
visibility enforcement · inherited method dispatch · `SWITCH` · postfix `++/--` · `MATH.*`
namespace · `STRING`/`NUMBER`/`ARRAY` method dispatcher (start with `COUNT`, `PUSH`, `POP`,
`SORT`, `JOIN`, `TOUPPER`, `TOLOWER`, `TRIM`, `SUBSTR`, `SPLIT`) · `TYPEOF` for enums and
arrays · printing arrays and objects.

**Phase 3 — adopt 0.73** (host first, then port)
`KEY` → `KEYCODE` everywhere, which also repairs `Osb.Shell` and the KISS/CONFIG apps ·
enum types out of the global variable table · read path reordered to the 0.73 §11–18 table ·
legacy `OSL.CONSOLE.*` constants returning enum values · 0.73 §20 assignment shadowing
(live in the host write path already; the kernel gets it with B2).

**Phase 4 — the remaining versions, in value order**
`DATE`/`TIME` (0.5, the only untouched version) · `FOR IN` (0.62) · `VIRTUAL`/`OVERRIDE`
(0.3) · interfaces (0.2, kernel side) · `OSL.I18N` on the host shell (0.6) · `OSL.CONSOLE`
geometry/colour/cursor in the kernel, which needs `Hardware/Vga.cs` to grow a cursor and
attribute API first (0.7).

**Regression net first.** The kernel port has no tests at all, which is why B2–B5 survived:
the sample scripts in `build.sh` (`SIMPLE`, `CLASS_DEMO`, `SIMPLE_CLASS`, `DOWHILE`,
`TRYCATCH`, `ENUM_DEMO`, `TEST_FUNC`, `HELLO`) are the natural test corpus — they need to
be run by the QEMU smoke test with their output asserted. The host already has 149 tests;
the two failures to expect are the known date-dependent `cal` test in `Osb.Shell.Tests` and
nothing in `Osb.Lang.Tests`.

---

## Appendix — running an OSLANG script today

| Where | How | Status |
|---|---|---|
| Host (`src/Osb.Shell`) | `dotnet run --project src/Osb.Shell` then the app's entry, or a `.osl` in `APPS/` | works; KISS/CONFIG need the 0.73 `KEYCODE` name |
| Kernel | `OSLANG <script>` | **not reachable — B1** |