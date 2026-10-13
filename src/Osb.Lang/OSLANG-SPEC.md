# OSLANG — Unified Specification

**Status:** canonical language reference (single source of truth)
**Latest specification version:** 0.73
**Reference implementation:** `src/Osb.Lang` (`OslangInterpreter`, currently 0.72)
**Bare-metal port:** `src/Osb.Kernel/OslangKernel.cs` (subset, see [OSLANG-GAP.md](OSLANG-GAP.md))

This document is the unified, deduplicated feature reference for OSLANG. It folds
together the twelve per-version specs that still live next to it in this folder;
those files are kept as the historical record of *what changed when*, and are linked
from the [Version history](#1-version-history).

---

## How to read this document

- Language features are documented **once**, in the section that owns them, with the
  version that introduced them in parentheses: `OSL.CONSOLE.GETKEY()` *(0.7)*.
- "Since 0.x" always means "first specified in 0.x". A feature may be extended later;
  the later versions are called out explicitly at the point of change.
- Implementation status per feature is **not** in this document. That lives in
  [OSLANG-GAP.md](OSLANG-GAP.md), which compares the spec against both implementations.

### Layering rule (0.6 onward)

OSLANG is three layers, and a change belongs in exactly one of them:

| Layer | Prefix | Contents |
|---|---|---|
| Language core | — | keywords, statements, types, semantics. No I/O primitives beyond `PRINT`/`INPUT`/`CLEAR`. |
| Standard library | `OSL.*` | generic capabilities: console, files, dirs, math, dates, JSON, i18n. |
| Host extensions | `OSB.*` | host-specific infrastructure (`OSB.NET`). |

Application behaviour belongs in the application (`KISS.OSL`), never in the runtime.
This rule is restated as the closing constraint of both 0.72 and 0.73.

---

## 1. Version history

| Version | Goal (from the spec) | Kind | Spec file |
|---|---|---|---|
| 0.1 | BASIC-flavoured core: simple to learn, implement and read; small programs and scripts | initial | [OSLANG 0.1 — Especificação da Linguagem.md](OSLANG%200.1%20—%20Especificação%20da%20Linguagem.md) |
| 0.2 | Add OOP without losing BASIC simplicity | additive | [OSLANG-0.2-SPEC.md](OSLANG-0.2-SPEC.md) |
| 0.3 | Multi-file programs, `SWITCH`, events, generics, overloading, virtual/override | additive | [OSLANG-0.3-SPEC.md](OSLANG-0.3-SPEC.md) |
| 0.4 | Practical standard library: primitive methods, collections, math, files, dirs | **breaking** (math functions namespaced) | [OSLANG-0.4-SPEC.md](OSLANG-0.4-SPEC.md) |
| 0.5 | Native temporal values (`DATE`, `TIME`) | additive | [OSLANG-0.5-SPEC.md](OSLANG-0.5-SPEC.md) |
| 0.51 | `SHOW`, arrow functions, operators (`MOD`, `**`, `++`, `+=`), nested arrays | additive | [OSLANG-0.51-SPEC.md](OSLANG-0.51-SPEC.md) |
| 0.6 | The `OSL` standard-library concept, first library `OSL.I18N` | additive | [OSLANG-0.6-SPEC.md](OSLANG-0.6-SPEC.md) |
| 0.61 | Enums, enum sets, `SWITCH`, universal `BREAK`, string escapes/multiline/interpolation | additive | [OSLANG-0.61-SPEC.md](OSLANG-0.61-SPEC.md) |
| 0.62 | Data interchange and network: JSON, CSV, XML, CNF, HTTP | additive | [OSLANG-0.62-SPEC.md](OSLANG-0.62-SPEC.md) |
| 0.7 | Full-screen interactive terminal applications (`OSL.CONSOLE`, `OSL.APP`) | additive | [OSLANG-0.7-SPEC.md](OSLANG-0.7-SPEC.md) |
| 0.72 | Ergonomic object model (implicit `ME`), typed keyboard events, enum flags | **breaking** | [OSLANG-0.72-SPEC.md](OSLANG-0.72-SPEC.md) |
| 0.73 | Fix the identifier/enum/assignment problems 0.72 introduced | **breaking** | [OSLANG-0.73-SPEC.md](OSLANG-0.73-SPEC.md) |

Two versions are referenced by later specs but have no spec file in this folder:
**0.41** (source of `ARGS`, cited by 0.5/0.51) and **0.63/0.64** (roadmap placeholders in
0.62 for object mapping and full HTTP).

### 1.1 What each version added, in one line

- **0.1** — statements (`IF/WHILE/FOR/DO`, `TRY/CATCH`), `VAR`/`GLOBAL`, `PRINT`/`INPUT`,
  `STR`/`NUMBER`/`BOOL`, math, `COUNT`/`TYPEOF`, four error categories.
- **0.2** — `CLASS`, `CONSTRUCTOR`, `NEW`, single inheritance, `ME`, `PUBLIC`/`PRIVATE`/`PROTECTED`,
  `INTERFACE` contracts, implicit method overriding.
- **0.3** — `USING` + multi-file, `SWITCH`/`CASE`/`DEFAULT` (statement and expression),
  `EVENT`/`ON`/`RAISE`, generics, function overloading, `VIRTUAL`/`OVERRIDE`, `EXTENDS`,
  `END CLASS`/`END FUNCTION`, order-independent declarations.
- **0.4** — methods on values (`STRING`/`NUMBER`/`BOOLEAN`/`ARRAY`), method chaining,
  callbacks by named function, `MATH`/`FILE`/`DIR` namespaces, file streams.
- **0.5** — `DATE` and `TIME` primitive types + namespaces, chronological comparison.
- **0.51** — `SHOW`, `MOD`, `**`, postfix `++`/`--`, `+=`, arrow functions and closures,
  nested arrays, `MATH` trigonometry.
- **0.6** — the `OSL` namespace as the standard-library root, `USING` for library modules,
  `OSL.I18N`, `.I18N` resource files.
- **0.61** — `ENUM`/`END ENUM`, enum sets via `|`, `BREAK` inside `SWITCH`, `\n`/`\t`/`\\`
  escapes, triple-quoted multiline strings, `${...}` interpolation.
- **0.62** — `OSL.JSON`, `OSL.CSV`, `OSL.XML`, `OSL.CNF`, `OSB.NET`.
- **0.7** — `OSL.CONSOLE` (screen, colour, cursor, keys, frames), `OSL.APP` (`ARGS`, `EXIT`),
  the keyboard event object, `FILE` renamed to `OSL.FILE`.
- **0.72** — implicit `ME`, the identifier resolution order, keyboard keys as the `KEY` enum,
  enum combination with `|`.
- **0.73** — `KEY` renamed to `KEYCODE`, enum types in a dedicated registry, assignment that
  respects local/parameter shadowing, legacy console constants returning enum values.

---

## 2. Lexical structure

- **Case-insensitive** everywhere: keywords, identifiers, enum members, property names.
  `PRINT`, `Print` and `print` are the same token.
- **Line-oriented**: a statement ends at the end of the line. `;` is *not* a statement
  terminator (0.1).
- **Program files**: `.osl`.
- **Entry point**: `FUNCTION MAIN()`, optionally taking the `ARGS` array.
- **Comments** (0.1): `REM ...` and `' ...`. In 0.1 a comment occupies a whole line; both
  implementations also accept a trailing comment after a statement.

### 2.1 Keywords

Complete set, with the version that introduced each:

| Group | Keywords |
|---|---|
| Control flow (0.1) | `IF` `THEN` `ELIF` `ELSE` `END` `FOR` `TO` `STEP` `WHILE` `DO` `BREAK` `CONTINUE` |
| Declarations (0.1/0.2) | `FUNCTION` `END FUNCTION` `VAR` `GLOBAL` `RETURN` `CLASS` `END CLASS` `CONSTRUCTOR` `PUBLIC` `PRIVATE` `PROTECTED` `ME` `NEW` |
| Values (0.1) | `TRUE` `FALSE` `NULL` |
| Operators (0.1/0.51) | `AND` `OR` `NOT` `MOD` |
| Built-ins (0.1) | `PRINT` `INPUT` `CLEAR` `STR` `NUMBER` `BOOL` `SQRT` `ABS` `POW` `FLOOR` `CEIL` `COUNT` `TYPEOF` |
| Modules (0.3/0.6) | `USING` |
| Statements (0.3/0.61) | `SWITCH` `CASE` `DEFAULT` |
| Objects (0.3) | `EVENT` `ON` `END ON` `RAISE` `VIRTUAL` `OVERRIDE` `EXTENDS` `THIS` |
| Types (0.61) | `ENUM` `END ENUM` |
| Output (0.51) | `SHOW` |
| Interfaces (0.2) | `INTERFACE` `END INTERFACE` |

Notes: `SQRT`/`ABS`/`POW`/`FLOOR`/`CEIL` were **removed as global functions in 0.4** in favour
of `MATH.*` (the keywords remain listed for history only).

---

## 3. Types and values

| Type | Since | Notes |
|---|---|---|
| `NULL` | 0.1 | The absence of a value. Assignable to any type. |
| `NUMBER` | 0.1 | One numeric type (floating point where the host needs it; integer syntax allowed). |
| `STRING` | 0.1 | Unicode text. |
| `BOOLEAN` | 0.1 | `TRUE` / `FALSE`. |
| `ARRAY` | 0.1 | Homogeneous (`ARRAY<STRING>`). Nested arrays allowed from 0.51. |
| `OBJECT` | 0.2 | Class instance. |
| `ENUM` | 0.61 | Value with `NAME()`/`VALUE()`/`TOSTRING()`. Numeric or string members. |
| `DATE` | 0.5 | Calendar date (`YEAR`/`MONTH`/`DAY`). |
| `TIME` | 0.5 | Time of day. |
| Key event | 0.7 | Object with `KEY`, `CHAR`, `CTRL`, `ALT`, `SHIFT`. |
| `SIZE`, `CURSOR` | 0.7 | Returned by `OSL.CONSOLE.SIZE()` / `GETCURSOR()`. |

- `DATETIME` is deliberately **not** a primitive: it is the structural array `[DATE, TIME]`,
  so `TYPEOF(Datetime)` is `ARRAY` (0.5).
- `TYPEOF(value)` returns the runtime type name; for a class instance it returns the class
  name, for an enum the enum name (0.51, 0.61).
- Type stability (0.1): once a variable holds a non-`NULL` value of a type, it keeps that
  type; `NULL` may be assigned to it until a real value arrives.

---

## 4. Variables, scope and ARGS

- `VAR name` declares a local variable; `GLOBAL name` a global one (0.1).
- Command-line arguments arrive as the read-only `ARGS` array, exposed as `ARGS` globally
  and as `OSL.APP.ARGS` (0.41/0.7).
- **0.73 identifier resolution order** — the single rule for reading a bare identifier;
  the first match wins:

  1. current local scope (locals and parameters)
  2. current class properties (implicit `ME`)
  3. global variables
  4. enum types
  5. functions
  6. classes and interfaces

  Explicit references (`ME.NAME`) bypass the search. Because the order is total, ordinary
  collisions never raise an ambiguous-reference error — the higher-priority scope simply wins.
- **0.73 assignment rule**: assigning to a bare identifier inside a method follows the same
  order. `Age = Age + 1` inside a method with a parameter `Age` increments the *parameter*;
  use `ME.Age = Age + 1` to touch the property. (0.72 always fell through to the property.)
- **0.73 enum rule**: enum types live in their own registry, not in the global variable
  table, so `GLOBAL Status = "ready"` creates a variable that *shadows* the enum type in
  that scope instead of destroying the enum binding.

---

## 5. Expressions and operators

Precedence, highest first (0.51 pins the relative order of the 0.51 additions):

| Precedence | Operators | Notes |
|---|---|---|
| 1 | `**` | power, right-associative, above `*` `/` |
| 2 | unary `-` `NOT` | |
| 3 | `*` `/` `%` `MOD` | |
| 4 | `+` `-` | also string concatenation |
| 5 | `<` `<=` `>` `>=` | `DATE`/`TIME` compare chronologically (0.5) |
| 6 | `=` `<>` | |
| 7 | `AND` | short-circuit |
| 8 | `OR` | short-circuit |
| 9 | postfix `++` `--` | postfix only; prefix form is invalid |
| 10 | `=` (assignment), `+=` | `+=` is the only compound assignment |

- `|` combines enum values into an enum set (0.61). It is **not** logical OR and never
  applies to booleans.
- `=>` introduces an arrow function (0.51): `X => X * 2`, `(A, B) => A + B`,
  `X => … END` for a block body. Arrow functions capture their enclosing scope (closures).
- Method chaining works on any expression returning a value: `"abc".TOUPPER().TRIM()`.
- A named function is a first-class value and can be passed as the callback argument of
  collection methods (0.4) — anonymous functions were explicitly out of scope.

---

## 6. Statements

| Statement | Since | Form |
|---|---|---|
| `PRINT` | 0.1 | `PRINT expr` — one expression per statement; strings are concatenated with `+` |
| `SHOW` | 0.51 | like `PRINT` but without the trailing newline |
| `INPUT` | 0.1 | reads a line, always returns `STRING` |
| `CLEAR` | 0.1 | clears the screen |
| `IF` | 0.1 | `IF c THEN … ELIF c THEN … ELSE … END` |
| `WHILE` | 0.1 | `WHILE c … END` |
| `DO`/`WHILE` | 0.1 | `DO … WHILE c` (post-test) |
| `FOR` | 0.1 | `FOR i = a TO b [STEP s] … END`, bounds inclusive |
| `FOR`/`IN` | 0.62 | `FOR Item IN Collection … END`, over any collection |
| `SWITCH` | 0.3, 0.61 | `SWITCH v CASE 1 … CASE 2 … DEFAULT … END`, first match only, no fall-through |
| `BREAK` / `CONTINUE` | 0.1 | loops; from 0.61 `BREAK` also exits `SWITCH` |
| `TRY`/`CATCH` | 0.1 | `TRY … CATCH ERR … END`; `ERR` holds the message |
| `RETURN` | 0.1 | from a function or method |
| `VAR` / `GLOBAL` | 0.1 | declarations |
| `USING` | 0.3 | module import, must precede all declarations |
| `EVENT` / `ON` / `RAISE` | 0.3 | event declaration, handler block, and raise |

`SWITCH` is also available as an expression: `CASE1 => "Monday"` (0.3).

---

## 7. Functions

```
FUNCTION Add(A, B)
    RETURN A + B
END FUNCTION
```

- Parameters are passed by value; recursion is allowed.
- A function may be called bare (`Add(1, 2)`) or with `OSL.`/class qualification.
- **Overloading** (0.3): same name, different signature; an ambiguous call is a compile error.
- **Arrow functions** (0.51) are the inline alternative; see [§5](#5-expressions-and-operators).
- `FUNCTION MAIN()` is the entry point and may take zero parameters or a single `ARGS` parameter.

---

## 8. Classes and objects

```
CLASS PERSON EXTENDS ENTITY
    PRIVATE AGE

    CONSTRUCTOR(Name, Age)
        ME.NAME = Name
        ME.AGE  = Age
    END CONSTRUCTOR

    PUBLIC FUNCTION GREET()
        PRINT "Hello " + NAME          ' implicit ME (0.72)
    END
END CLASS
```

- Fields default to `PUBLIC`; `PUBLIC`/`PRIVATE`/`PROTECTED` are enforced.
- `NEW Class()` / `NEW Class(args)` calls `CONSTRUCTOR`; a class without one gets a
  default parameterless constructor. Constructor overloading is allowed from 0.3.
- **Single inheritance** only, written `CLASS Child: Parent` or `CLASS Child EXTENDS Parent`
  (0.3 introduced `EXTENDS`; the 0.72/0.73 examples use `:`).
- `ME` is the current object. From **0.72** it may be omitted whenever the identifier
  resolves unambiguously — for reads *and* for calls (`SAVE()` == `ME.SAVE()`), for
  properties and for inherited members. `ME` is only valid inside a method; using it in a
  free function is an error.
- `BASE` refers to the parent instance inside a derived class.
- `VIRTUAL`/`OVERRIDE` (0.3): a derived class must override virtual methods explicitly;
  overriding a non-virtual method is an error. Implicit overriding (0.2) is superseded.
- `INTERFACE`/`END INTERFACE` (0.2): a contract of member signatures; a class implementing
  it must provide public implementations — a `PRIVATE`/`PROTECTED` member does not satisfy
  the contract. A class may implement several interfaces.
- Events (0.3): `EVENT Name`, `ON Name … END ON` handlers, `RAISE Name`.
- Not in any version: multiple class inheritance, abstract classes, static members/classes,
  operator overloading, custom property getters/setters, attributes, reflection,
  async/await, lambdas-with-`->`, records, structs, `METHOD` as a keyword.

---

## 9. Enums

```
ENUM PERMISSION
    READ
    WRITE = 4
    EXECUTE
END ENUM
```

- Declared with `ENUM … END ENUM` (0.61); members default to auto-incrementing integers
  from 0, or to strings when the first explicit value is a string.
- Access: `PERMISSION.READ`, `PERMISSION.NAME()` for the name, `PERMISSION.VALUE()` for the
  underlying value, `TOSTRING()`.
- `TYPEOF(Permission)` is the enum type name.
- **Enum sets** (0.61): `Weekend = Weekday.Saturday | Weekday.Sunday`. Test membership with
  `Permissions.CONTAINS(PERMISSION.READ)` (preferred, 0.72/0.73) or `Permissions | PERMISSION.READ`.
  Enum sets are immutable and offer `CONTAINS()`, `COUNT()`, `FOREACH()`.
- Keyboard modifiers are *not* enum members: represent `CTRL + S` as `CTRL` plus `KEYCODE.S`
  rather than inventing thousands of combined values (0.72/0.73).

---

## 10. Modules and namespaces

- `USING ModuleName` (0.3) imports a library module or another source file; it must appear
  before any declaration, circular imports are rejected, unknown modules are an error.
- Declarations are order-independent within a module (0.3): lexing → parsing → symbol
  discovery → type resolution → semantic validation → execution. Modules are cached by
  canonical path.
- Library modules live under the reserved `OSL` namespace (0.6); host infrastructure under
  `OSB`. Users cannot define names inside those namespaces.
- From 0.7 the file namespace is `OSL.FILE` (it was the bare `FILE` from 0.4).

---

## 11. Standard library

### 11.1 `OSL.CONSOLE` *(0.7; keyboard enum typed in 0.72, renamed in 0.73)*

Screen and keyboard for terminal programs. Terminal coordinates are **1-based**.

| Group | Members |
|---|---|
| Size | `WIDTH()` `HEIGHT()` `SIZE()` `RESIZED()` |
| Cursor | `SETCURSOR(Row, Col)` `GETCURSOR()` `HIDECURSOR()` `SHOWCURSOR()` |
| Output | `CLEAR()` `CLEARLINE()` `CLEARLINE(Row)` `CLEARAREA(T, L, B, R)` `WRITE(Row, Col, Text)` |
| Colour | `COLOR(Fg, Bg)` `RESETCOLOR()` + constants `BLACK` `BLUE` `GREEN` `CYAN` `RED` `MAGENTA` `YELLOW` `WHITE` and `BRIGHT_*` variants |
| Input | `GETKEY()` (blocks) `READKEY()` (returns `NULL` when nothing pending) `KEYAVAILABLE()` |
| Legacy key constants | `ENTER` `ESC` `TAB` `BACKSPACE` `DELETE` `INSERT` `SPACE` `UP` `DOWN` `LEFT` `RIGHT` `HOME` `END` `PAGEUP` `PAGEDOWN` `F1`…`F12` — from 0.73 these return enum values instead of strings, and are interchangeable with the enum members |
| Screen modes | `ENTER()` `EXIT()` `ALTERNATE(TRUE|FALSE)` |
| Frames | `BEGINFRAME()` `ENDFRAME()` `FLUSH()` |
| Misc | `BEEP()` |

The keyboard event object returned by `GETKEY()` exposes `KEY` (enum `KEYCODE`), `CHAR`
(printable character or `NULL`), `CTRL`, `ALT`, `SHIFT`. For printable keys `KEY` is
`KEYCODE.UNKNOWN`; use `Key.CHAR <> NULL` to distinguish "printable" from "special".

### 11.2 `OSL.FILE` *(0.4 as `FILE`, 0.7 as `OSL.FILE`)*

`EXISTS` `READ` `READTEXT` `WRITE` `WRITETEXT` `APPEND` `READLINES` `WRITELINES` `CREATE`
`DELETE` `DEL` `COPY` `MOVE` `RENAME` `SIZE` `EXTENSION` `NAME` `DIR` `OPEN`.

Streams (`OPEN` result): `READLINE()` `READ()` `WRITE(Text)` `EOF()` `CLOSE()`.

### 11.3 `DIR` *(0.4; still bare in 0.61)*

`EXISTS` `CREATE` `DELETE` `LIST` `FILES` `DIRS` `CURRENT` `CHANGE` `RENAME` `COPY`.

### 11.4 `MATH` *(0.4)*

`SQRT` `ABS` `POW` `FLOOR` `CEIL` `ROUND` `ROUND(V, Digits)` `TRUNC` `TRUNC(V, Digits)`
`SIGN` `MOD` `MIN` `MAX` `CLAMP` `RANDOM` `RANDOM(Min, Max)` `LOG` `LOG10` `EXP`
`SIN` `COS` `TAN` `ASIN` `ACOS` `ATAN` `ATAN2` (radians), constants `MATH.PI`, `MATH.E`.

### 11.5 `DATE` / `TIME` *(0.5)*

`DATE.NOW()` `DATE.NEW(Y, M, D)` `DATE.FROMNUMBER(UnixEpoch)`;
methods `YEAR()` `MONTH()` `DAY()` `DAYOFWEEK()` `DAYOFYEAR()` `MONTHNAME()` `DAYNAME()`
`ISLEAPYEAR()` `DAYSINMONTH()` `FORMAT(Pattern)` `TONUMBER()` `TOSTRING()`.

`TIME.NOW()` `TIME.NEW(H, M, S)` `TIME.FROMNUMBER(Seconds)` `TIME.MIDNIGHT()`;
methods `HOUR()` `MINUTE()` `SECOND()` `MILLISECOND()` `FORMAT(Pattern)` `TONUMBER()` `TOSTRING()`.

No implicit date/time arithmetic, no timezone objects, no duration type (0.5).

### 11.6 `OSL.I18N` *(0.6)*

`GET(Key, …)` `HAS(Key)` `KEYS()` `LANGUAGE()` `SETLANGUAGE(Lang)` `LANGUAGES()` `LOAD(Path)`
`LOADLANGUAGE(Lang, Path)` `RELOAD(Lang?)` `UNLOAD(Lang)` `DEFAULT()` `SETDEFAULT(Lang)`
`SETFALLBACK(Lang)`.

`.I18N` resource files are UTF-8 `key=value` with `#` comments and `{0}` positional
placeholders. A missing translation returns the key itself (not an error). Exactly one
explicit fallback language; no parent-locale fallback.

### 11.7 `OSL.JSON`, `OSL.CSV`, `OSL.XML`, `OSL.CNF` *(0.62)*

- `OSL.JSON`: `PARSE(String)` `STRINGIFY(Value)` `STRINGIFY(Value, TRUE)` `READ(path)` `WRITE(...)`
- `OSL.CSV`: `PARSE(Content)` `PARSE(Content, TRUE)` `STRINGIFY(Data)` `READ()` `WRITE()` `APPEND()`
- `OSL.XML`: `PARSE` `STRINGIFY` `READ` `WRITE`; node members `NAME()` `VALUE()` `ATTRIBUTES()` `CHILDREN()` `CHILD()` `HAS()`
- `OSL.CNF` (OSB configuration, not a generic format): `READ(path)` `WRITE(Config, path)`; on the
  returned object `GET(key)` `SET(key, value)` `HAS(key)` `SAVE()` `DELETE(key)` `KEYS()`

`PARSE(UserClass, Text)` — binding text onto a user class — is **deferred to a later
version**; in 0.62 `PARSE` is a library operation, not a grammar feature.

### 11.8 `OSL.APP` *(0.7)*

`ARGS` (global argument array) `NAME()` `EXIT(Code)`.

### 11.9 `OSB.NET` *(0.62, host extension)*

`PING(Host)` → `{ success, host, time }`; `DOWN(URL)` and `DOWN(URL, Options)`
(`timeout`, `headers`). Response fields `STATUS`/`HEADERS`/`BODY` and verbs
`GET`/`POST`/`PUT`/`PATCH`/`DELETE`/`HEAD` are future work.

---

## 12. Primitive methods (0.4, extended 0.51)

Methods are called on values: `"abc".TOUPPER()`.

**`STRING`** — `TOUPPER()` `TOLOWER()` `TRIM()` `STARTSWITH(v)` `ENDSWITH(v)` `CONTAINS(v)`
`INDEXOF(v)` `SUBSTR(start, length)` `REPLACE(old, new)` `SPLIT(sep)` `COUNT()` `ISEMPTY()`
`REVERSE()` `TOSTRING()` `PADSTART(len, pad)` `PADEND(len, pad)` `REPEAT(n)` `LEFT(n)`
`RIGHT(n)` `FIND(v)` `INSERT(v, pos)` `REMOVE(pos)` `JOIN(sep)`.

**`NUMBER`** — `TOSTRING()` `ABS()` `FLOOR()` `CEIL()` `TRUNC([digits])` `ISINTEGER()`
`BETWEEN(min, max)`.

**`BOOLEAN`** — `TOSTRING()` `TOGGLE()`.

**`ARRAY`** — `COUNT()` `FIRST()` `LAST()` `CONTAINS(v)` `INDEXOF(v)` `FINDINDEX(f)`
`FOREACH(f)` `ADD(v)` `REMOVE(v)` `CLEAR()` `REVERSE()` `SORT()` `MAP(f)` `FILTER(f)`
`ANY(f)` `SOME(f)` `ALL(f)` `REDUCE(f, initial)` `PUSH(v)` `POP()` `FLAT()` `FLATMAP(f)`
`JOIN(sep)`. Arrays stay homogeneous; `SORT()` does not mutate.

Callback-taking methods take a **named function** (0.4). Arrow functions (0.51) and
closures work as callbacks as well.

---

## 13. Strings

- Escapes *(0.61)*: `\n`, `\t`, `\\`. An unknown escape is invalid.
- Multiline *(0.61)*: triple quotes `"""…"""`, with common indentation stripped.
- Interpolation *(0.61)*: `"x = ${X + 1}"`, expressions and method calls allowed inside;
  escape a literal `${` as `\${`; works inside multiline strings too.

---

## 14. Errors

Four categories are distinguished from 0.1: lexical, syntax, semantic and runtime.
0.2 adds interface/implementation errors, 0.3 adds duplicate-declaration, unknown-symbol,
invalid-inheritance, invalid-override, ambiguous-overload, circular-`USING` and
missing-`MAIN` errors.

`TRY … CATCH ERR … END` catches runtime errors; `ERR` carries the message. `EXIT(Code)`
(0.7) ends the program. There is no second exception mechanism: I/O, JSON and date errors
all surface through `CATCH`.

---

## 15. Explicit non-goals

Never specified, in any version: `GOTO`/`GOSUB`, multiple class inheritance, abstract or
static classes, operator overloading, anonymous classes, records, structs, dictionaries,
`FOR EACH` over anything other than collections, attribute/macro/reflection systems,
async/await, coroutines, threads, pointers, delegates, arbitrary host-language interop.

Deferred with a named future version: `PARSE(UserClass, text)` (object mapping), full HTTP
verbs/auth/cookies (0.62 roadmap), strict i18n missing-key mode, timezone-aware date/time,
mouse and clipboard support (`OSL.CONSOLE` extensions).

---

## 16. Spec drift and open questions

Things the specs disagree about or never resolved — worth fixing in the next revision:

1. **`KEY` vs `KEYCODE`** — 0.72 introduced `KEY`, 0.73 renames it back to `KEYCODE` because
   OSLANG identifiers are case-insensitive and `Key` (the natural name for a key event
   variable) collides with the enum type. Both names are in wide use; 0.73 is canonical.
2. **`ME` vs `THIS`** — 0.2 introduced `ME`, 0.3 examples use `THIS`, 0.72/0.73 use `ME`.
3. **`END` vs `END CLASS` / `END FUNCTION`** — 0.1/0.2 use bare `END`; 0.3 mandates
   `END CLASS`/`END FUNCTION`.
4. **Namespace spelling** — `MATH`/`FILE`/`DIR` are bare in 0.4–0.61 and `OSL.*` from 0.6/0.7.
5. **`METHOD` keyword** — never specified; examples in 0.3 use `FUNCTION` or the bare form.
6. **Overriding** — implicit in 0.2, explicit `VIRTUAL`/`OVERRIDE` in 0.3; the reference
   implementation accepts both.
7. **Missing specs** — 0.41 (source of `ARGS`) and 0.63/0.64 (roadmap) are referenced but absent.
8. **Events** — specified once in 0.3 and never revisited; no version states whether they are
   required for 0.73.