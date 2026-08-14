# NOT

Logical negation operator.

## Syntax

```osl
NOT expression
```

## Description

`NOT` inverts a boolean value. If `expression` is `TRUE`, the result is `FALSE`. If `expression` is `FALSE`, the result is `TRUE`.

## Example

```osl
VAR a = TRUE
PRINT NOT a  ' FALSE

IF NOT isReady THEN
    PRINT "Not ready"
END IF
```

## Related

- [AND](/src/docs/oslang/reference/keywords/and.md)
- [OR](/src/docs/oslang/reference/keywords/or.md)
- [Operators](/src/docs/oslang/guide/operators.md)
