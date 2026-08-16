# OSLANG Documentation

Welcome to the official OSLANG documentation. OSLANG is a modern, interpreted programming language designed for simplicity, readability, and rapid development. It supports procedural, object-oriented, and event-driven programming paradigms with a focus on order-independent declarations and multi-file programs.

## Quick Links

- **[Getting Started](/src/docs/oslang/getting-started/index.md)** — Install OSLANG and write your first program.
- **[Hello World](/src/docs/oslang/getting-started/hello-world.md)** — A minimal OSLANG example.
- **[Syntax Basics](/src/docs/oslang/guide/syntax-basics.md)** — Learn the foundational syntax rules.
- **[Variables and Types](/src/docs/oslang/guide/variables-and-types.md)** — Declare and use variables, including ENUM types.
- **[Control Flow](/src/docs/oslang/guide/control-flow.md)** — `IF`, `FOR`, `WHILE`, `DO WHILE`, and `SWITCH`/`CASE`/`DEFAULT`.
- **[Functions](/src/docs/oslang/guide/functions.md)** — Define and call functions, including arrow functions.
- **[Classes and Objects](/src/docs/oslang/guide/classes-and-objects.md)** — Object-oriented programming with constructors, properties, and methods.
- **[Interfaces](/src/docs/oslang/guide/interfaces.md)** — Define and implement interfaces.
- **[Inheritance](/src/docs/oslang/guide/inheritance.md)** — Extend classes with `VIRTUAL` and `OVERRIDE`.
- **[Generics](/src/docs/oslang/guide/generics.md)** — Write reusable generic types and functions.
- **[Events](/src/docs/oslang/guide/events.md)** — Declare and handle events with `EVENT`, `ON`, and `RAISE`.
- **[Modules](/src/docs/oslang/guide/modules.md)** — Organize code across files with `USING`.
- **[Namespaces](/src/docs/oslang/guide/namespaces.md)** — `MATH`, `FILE`, `DIR`, `DATE`, `TIME` namespaces and primitive methods.
- **[Error Handling](/src/docs/oslang/guide/error-handling.md)** — Catch runtime errors with `TRY` and `CATCH`.
- **[Keywords Reference](/src/docs/oslang/reference/keywords/index.md)** — Complete list of OSLANG keywords.
- **[Built-in Functions](/src/docs/oslang/reference/built-ins/index.md)** — Standard library functions.

## Specification

- **[OSLANG 0.62 Specification](/src/Osb.Lang/OSLANG-0.62-SPEC.md)** — Full language specification (OSL.JSON, OSL.CSV, OSL.XML, OSL.CNF, OSB.NET, JSON/CSV/XML/CNF data interchange and HTTP).
- **[OSLANG 0.61 Specification](/src/Osb.Lang/OSLANG-0.61-SPEC.md)** — Full language specification (ENUM, ENUM SETS, SWITCH, CASE, DEFAULT, BREAK, string interpolation, multiline strings, escape sequences).
- **[OSLANG 0.6 Specification](/src/Osb.Lang/OSLANG-0.6-SPEC.md)** — Full language specification (SHOW, arrow functions, MOD, **, ++, --, +=, nested arrays, FINDINDEX, FOREACH, CONTAINS, JOIN, PUSH, POP, SORT, FLAT, FLATMAP, MATH.PI, trig functions, NUMBER.TRUNC, STRING.PADSTART/PADEND/REPEAT).
- **[OSLANG 0.5 Specification](/src/Osb.Lang/OSLANG-0.5-SPEC.md)** — Full language specification (DATE, TIME, ARGS).
- **[OSLANG 0.4 Specification](/src/Osb.Lang/OSLANG-0.4-SPEC.md)** — Previous version with ARGS support.

## Version Notes

- **OSLANG 0.62** adds OSL.JSON (PARSE, STRINGIFY, PRETTY, READ, WRITE), OSL.CSV (PARSE, STRINGIFY, READ, WRITE), OSL.XML (PARSE, STRINGIFY, READ, WRITE, NAME, VALUE, ATTRIBUTES, CHILDREN, CHILD, HAS), OSL.CNF (READ, WRITE, GET, SET, HAS, DELETE, KEYS, SAVE), and OSB.NET (PING, DOWN).
- **OSLANG 0.61** adds ENUM, ENUM SETS, SWITCH/CASE/DEFAULT, BREAK, string interpolation, multiline strings, escape sequences (`\n`, `\t`, `\\`).
- **OSLANG 0.6** adds SHOW, arrow functions, MOD, **, postfix ++/--, +=, TYPEOF, nested arrays, array methods (FINDINDEX, FOREACH, CONTAINS, JOIN, PUSH, POP, SORT, FLAT, FLATMAP), MATH.PI, trig functions, NUMBER.TRUNC with decimals, STRING.PADSTART/PADEND/REPEAT.
- **OSLANG 0.5** adds native `DATE` and `TIME` types, `DATE.*` and `TIME.*` namespaces, and temporal conversions.
- **OSLANG 0.41** adds optional `ARGS` parameter to `FUNCTION MAIN(Args)` for command-line argument access.
- **OSLANG 0.4** adds primitive methods, namespaces (`MATH`, `FILE`, `DIR`), and stable dynamic typing.

## Tools and Extensions

- **VS Code Extension** — Syntax highlighting, intellisense, hover, signature help, document symbols, code folding, and snippets for OSLANG 0.61. See [`oslang-vscode/README.md`](../oslang-vscode/README.md).
- **KISS Editor** — Built-in text editor with OSLANG syntax highlighting for `.osl`, `.oslang`, `.cfg`, `.i18n`, `.hlp`, and `.wds` files.
- **TYPE Command** — Displays files with OSLANG syntax highlighting for `.osl`, `.oslang`, `.cfg`, `.i18n`, `.hlp`, and `.wds` files.

## Contributing

Found a mistake or want to improve these docs? Open an issue or pull request in the OSB repository.
