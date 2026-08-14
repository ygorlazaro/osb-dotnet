# STR

Converts a value to a string.

## Syntax

```osl
STR(value)
```

## Description

`STR()` converts numbers, booleans, and other values to their string representation.

- Numbers are formatted as decimal strings.
- `TRUE` becomes `"TRUE"`.
- `FALSE` becomes `"FALSE"`.
- `NULL` becomes `"NULL"`.

## Example

```osl
PRINT STR(42)       ' "42"
PRINT STR(3.14)     ' "3.14"
PRINT STR(TRUE)     ' "TRUE"
PRINT STR(NULL)     ' "NULL"
```

## Related

- [NUMBER](/src/docs/oslang/reference/built-ins/number.md)
- [BOOL](/src/docs/oslang/reference/built-ins/bool.md)
- [Variables and Types](/src/docs/oslang/guide/variables-and-types.md)
