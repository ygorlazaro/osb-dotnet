# BREAK

Exits a loop immediately.

## Syntax

```osl
BREAK
```

## Description

`BREAK` can only be used inside a `FOR`, `WHILE`, or `DO`/`WHILE` loop. When executed, it terminates the innermost loop and transfers control to the statement following the loop.

## Example

```osl
FOR i = 1 TO 100
    IF i = 5 THEN
        PRINT "Found 5, exiting."
        BREAK
    END IF
    PRINT i
END
' Prints 1, 2, 3, 4, then "Found 5, exiting."
```

## Related

- [CONTINUE](/src/docs/oslang/reference/keywords/continue.md)
- [FOR](/src/docs/oslang/reference/keywords/for.md)
- [WHILE](/src/docs/oslang/reference/keywords/while.md)
- [Control Flow](/src/docs/oslang/guide/control-flow.md)
