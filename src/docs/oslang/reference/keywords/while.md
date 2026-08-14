# WHILE

Creates a while loop.

## Syntax

```osl
WHILE condition
    ' statements
END
```

## Description

`WHILE` repeats its body while `condition` is true. The condition is evaluated before each iteration. If the condition is false initially, the body never runs.

## Example

```osl
VAR i = 1
WHILE i <= 5
    PRINT i
    i = i + 1
END
' Prints: 1 2 3 4 5
```

## Related

- [FOR](/src/docs/oslang/reference/keywords/for.md)
- [DO](/src/docs/oslang/reference/keywords/do.md)
- [BREAK](/src/docs/oslang/reference/keywords/break.md)
- [CONTINUE](/src/docs/oslang/reference/keywords/continue.md)
- [Control Flow](/src/docs/oslang/guide/control-flow.md)
