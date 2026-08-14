# BOOL

Converts a value to a boolean.

## Syntax

```osl
BOOL(value)
```

## Description

`BOOL()` converts its argument to a `BOOLEAN` value.

- `0` and `NULL` convert to `FALSE`.
- Any non-zero number converts to `TRUE`.
- Non-empty strings convert to `TRUE`.
- Empty strings convert to `FALSE`.

## Example

```osl
VAR x = BOOL(0)       ' FALSE
VAR y = BOOL(1)       ' TRUE
VAR z = BOOL("")      ' FALSE
VAR w = BOOL("hello") ' TRUE
```

## Related

- [NUMBER](/src/docs/oslang/reference/built-ins/number.md)
- [STR](/src/docs/oslang/reference/built-ins/str.md)
- [Variables and Types](/src/docs/oslang/guide/variables-and-types.md)
