# OR

Logical disjunction operator.

## Syntax

```osl
result = expression1 OR expression2
```

## Description

`OR` returns `TRUE` if at least one operand is `TRUE`. It returns `FALSE` only when both operands are `FALSE`.

## Truth Table

| A | B | A OR B |
|---|---|--------|
| FALSE | FALSE | FALSE |
| FALSE | TRUE | TRUE |
| TRUE | FALSE | TRUE |
| TRUE | TRUE | TRUE |

## Example

```osl
IF isAdmin OR isOwner THEN
    PRINT "Access granted"
END IF
```

## Related

- [AND](/src/docs/oslang/reference/keywords/and.md)
- [NOT](/src/docs/oslang/reference/keywords/not.md)
- [Operators](/src/docs/oslang/guide/operators.md)
