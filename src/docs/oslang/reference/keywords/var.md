# VAR

Declares a variable.

## Syntax

```osl
VAR name [TYPE] [= expression]
```

## Description

`VAR` declares a variable with an optional type annotation and optional initializer.

- Without a type, the variable is dynamically typed.
- Without an initializer, the variable is initialized to `NULL`.

## Example

```osl
VAR x
VAR count NUMBER
VAR name STRING = "Alice"
VAR sum = 0
```

## Related

- [GLOBAL](/src/docs/oslang/reference/keywords/global.md)
- [Variables and Types](/src/docs/oslang/guide/variables-and-types.md)
