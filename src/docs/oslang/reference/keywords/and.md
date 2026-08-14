# AND

Logical conjunction operator.

## Syntax

```osl
result = expression1 AND expression2
```

Both `expression1` and `expression2` must evaluate to `BOOLEAN`.

## Description

`AND` returns `TRUE` only if both operands are `TRUE`. Otherwise, it returns `FALSE`.

## Truth Table

| A | B | A AND B |
|---|---|---------|
| FALSE | FALSE | FALSE |
| FALSE | TRUE | FALSE |
| TRUE | FALSE | FALSE |
| TRUE | TRUE | TRUE |

## Example

```osl
VAR a = TRUE
VAR b = FALSE
PRINT a AND b    ' FALSE

IF age >= 18 AND hasTicket = TRUE THEN
    PRINT "Allowed"
END IF
```

## Related

- [OR](/src/docs/oslang/reference/keywords/or.md)
- [NOT](/src/docs/oslang/reference/keywords/not.md)
- [Operators](/src/docs/oslang/guide/operators.md)
