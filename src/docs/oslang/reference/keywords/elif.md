# ELIF

Else-if branch in a conditional.

## Syntax

```osl
IF condition THEN
    ' statements
ELIF condition THEN
    ' statements
ELSE
    ' statements
END
```

## Description

`ELIF` adds an additional condition to an `IF` block. It is checked only if all previous `IF` and `ELIF` conditions were false. You can chain multiple `ELIF` blocks.

## Example

```osl
IF score >= 90 THEN
    PRINT "A"
ELIF score >= 80 THEN
    PRINT "B"
ELIF score >= 70 THEN
    PRINT "C"
ELSE
    PRINT "F"
END
```

## Related

- [IF](/src/docs/oslang/reference/keywords/if.md)
- [ELSE](/src/docs/oslang/reference/keywords/else.md)
- [Control Flow](/src/docs/oslang/guide/control-flow.md)
