# CONTINUE

Skips the rest of the current loop iteration and continues with the next one.

## Syntax

```osl
CONTINUE
```

## Description

`CONTINUE` can only be used inside a loop (`FOR`, `WHILE`, or `DO`/`WHILE`). When executed, it stops the current iteration and jumps to the loop's condition check or next step.

## Example

```osl
FOR i = 1 TO 10
    IF i % 2 = 0 THEN
        CONTINUE
    END IF
    PRINT i  ' Prints only odd numbers: 1, 3, 5, 7, 9
END
```

## Related

- [BREAK](/src/docs/oslang/reference/keywords/break.md)
- [FOR](/src/docs/oslang/reference/keywords/for.md)
- [WHILE](/src/docs/oslang/reference/keywords/while.md)
- [Control Flow](/src/docs/oslang/guide/control-flow.md)
