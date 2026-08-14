# DO

Starts a do/while loop.

## Syntax

```osl
DO
    ' statements
WHILE condition
```

## Description

A `DO`/`WHILE` loop executes its body at least once, then repeats while the condition is true.

## Example

```osl
VAR input = ""
DO
    INPUT "Enter text (or 'quit'): ", input
    PRINT "You typed: " + input
WHILE input <> "quit"
```

## Related

- [WHILE](/src/docs/oslang/reference/keywords/while.md)
- [FOR](/src/docs/oslang/reference/keywords/for.md)
- [BREAK](/src/docs/oslang/reference/keywords/break.md)
- [CONTINUE](/src/docs/oslang/reference/keywords/continue.md)
- [Control Flow](/src/docs/oslang/guide/control-flow.md)
