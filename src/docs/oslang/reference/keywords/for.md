# FOR

Creates a for loop.

## Syntax

```osl
FOR variable = start TO end [STEP increment]
    ' statements
END
```

## Description

`FOR` iterates `variable` from `start` to `end`, inclusive. The optional `STEP` clause sets the increment; if omitted, it defaults to `1`. Use a negative step to count down.

## Example

```osl
FOR i = 1 TO 5
    PRINT i
END
' Prints: 1 2 3 4 5

FOR i = 10 TO 1 STEP -1
    PRINT i
END
' Prints: 10 9 8 7 6 5 4 3 2 1
```

## Related

- [WHILE](/src/docs/oslang/reference/keywords/while.md)
- [DO](/src/docs/oslang/reference/keywords/do.md)
- [STEP](/src/docs/oslang/reference/keywords/step.md)
- [BREAK](/src/docs/oslang/reference/keywords/break.md)
- [CONTINUE](/src/docs/oslang/reference/keywords/continue.md)
- [Control Flow](/src/docs/oslang/guide/control-flow.md)
