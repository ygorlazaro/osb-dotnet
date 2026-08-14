# THEN

Separates the condition from the body in an `IF` statement.

## Syntax

```osl
IF condition THEN
    ' statements
END
```

## Description

`THEN` appears after the `IF` condition and before the statement block. It is required in `IF` and `ELIF` branches.

## Example

```osl
IF x > 0 THEN
    PRINT "Positive"
END IF
```

## Related

- [IF](/src/docs/oslang/reference/keywords/if.md)
- [ELIF](/src/docs/oslang/reference/keywords/elif.md)
- [Control Flow](/src/docs/oslang/guide/control-flow.md)
