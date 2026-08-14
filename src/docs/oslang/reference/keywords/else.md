# ELSE

Default branch of a conditional.

## Syntax

```osl
IF condition THEN
    ' statements
ELSE
    ' statements
END
```

## Description

`ELSE` is optional and runs when the `IF` condition (and any `ELIF` conditions) are false.

## Example

```osl
IF age >= 18 THEN
    PRINT "Adult"
ELSE
    PRINT "Minor"
END
```

## Related

- [IF](/src/docs/oslang/reference/keywords/if.md)
- [ELIF](/src/docs/oslang/reference/keywords/elif.md)
- [Control Flow](/src/docs/oslang/guide/control-flow.md)
