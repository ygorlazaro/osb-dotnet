# IF

Conditional statement.

## Syntax

```osl
IF condition THEN
    ' statements
[ELIF condition THEN
    ' statements]
[ELSE
    ' statements]
END
```

## Description

`IF` executes a block of code only if `condition` evaluates to `TRUE`. You can chain `ELIF` branches and optionally add an `ELSE` fallback.

## Example

```osl
IF score >= 90 THEN
    PRINT "A"
ELIF score >= 80 THEN
    PRINT "B"
ELSE
    PRINT "F"
END
```

## Related

- [ELIF](/src/docs/oslang/reference/keywords/elif.md)
- [ELSE](/src/docs/oslang/reference/keywords/else.md)
- [THEN](/src/docs/oslang/reference/keywords/then.md)
- [Control Flow](/src/docs/oslang/guide/control-flow.md)
