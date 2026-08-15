# OSLANG Documentation

Welcome to the official OSLANG documentation. OSLANG is a modern, interpreted programming language designed for simplicity, readability, and rapid development. It supports procedural, object-oriented, and event-driven programming paradigms with a focus on order-independent declarations and multi-file programs.

## Quick Links

- **[Getting Started](/src/docs/oslang/getting-started/index.md)** — Install OSLANG and write your first program.
- **[Hello World](/src/docs/oslang/getting-started/hello-world.md)** — A minimal OSLANG example.
- **[Syntax Basics](/src/docs/oslang/guide/syntax-basics.md)** — Learn the foundational syntax rules.
- **[Variables and Types](/src/docs/oslang/guide/variables-and-types.md)** — Declare and use variables.
- **[Control Flow](/src/docs/oslang/guide/control-flow.md)** — `IF`, `FOR`, `WHILE`, and `SWITCH`.
- **[Functions](/src/docs/oslang/guide/functions.md)** — Define and call functions.
- **[Classes and Objects](/src/docs/oslang/guide/classes-and-objects.md)** — Object-oriented programming.
- **[Interfaces](/src/docs/oslang/guide/interfaces.md)** — Define and implement interfaces.
- **[Inheritance](/src/docs/oslang/guide/inheritance.md)** — Extend classes with `VIRTUAL` and `OVERRIDE`.
- **[Generics](/src/docs/oslang/guide/generics.md)** — Write reusable generic types and functions.
- **[Events](/src/docs/oslang/guide/events.md)** — Declare and handle events with `EVENT`, `ON`, and `RAISE`.
- **[Modules](/src/docs/oslang/guide/modules.md)** — Organize code across files with `USING`.
- **[Namespaces](/src/docs/oslang/guide/namespaces.md)** — `MATH`, `FILE`, `DIR` namespaces and primitive methods.
- **[Error Handling](/src/docs/oslang/guide/error-handling.md)** — Catch runtime errors with `TRY` and `CATCH`.
- **[Keywords Reference](/src/docs/oslang/reference/keywords/index.md)** — Complete list of OSLANG keywords.
- **[Built-in Functions](/src/docs/oslang/reference/built-ins/index.md)** — Standard library functions.

## Specification

- **[OSLANG 0.51 Specification](/src/Osb.Lang/OSLANG-0.51-SPEC.md)** — Full language specification (SHOW, arrow functions, MOD, **, ++, --, +=, nested arrays, FINDINDEX, FOREACH, CONTAINS, JOIN, PUSH, POP, SORT, FLAT, FLATMAP, MATH.PI, trig functions, NUMBER.TRUNC, STRING.PADSTART/PADEND/REPEAT).
- **[OSLANG 0.5 Specification](/src/Osb.Lang/OSLANG-0.5-SPEC.md)** — Full language specification (DATE, TIME, ARGS).
- **[OSLANG 0.41 Specification](/src/Osb.Lang/OSLANG-0.4-SPEC.md)** — Previous version with ARGS support.

## Version Notes

- **OSLANG 0.51** adds SHOW, arrow functions, MOD, **, postfix ++/--, +=, TYPEOF, nested arrays, array methods (FINDINDEX, FOREACH, CONTAINS, JOIN, PUSH, POP, SORT, FLAT, FLATMAP), MATH.PI, trig functions, NUMBER.TRUNC with decimals, STRING.PADSTART/PADEND/REPEAT.
- **OSLANG 0.5** adds native `DATE` and `TIME` types, `DATE.*` and `TIME.*` namespaces, and temporal conversions.
- **OSLANG 0.41** adds optional `ARGS` parameter to `FUNCTION MAIN(Args)` for command-line argument access.
- **OSLANG 0.4** adds primitive methods, namespaces (`MATH`, `FILE`, `DIR`), and stable dynamic typing.

## Contributing

Found a mistake or want to improve these docs? Open an issue or pull request in the OSB repository.
