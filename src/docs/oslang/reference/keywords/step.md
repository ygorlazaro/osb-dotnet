# STEP

Sets the increment in a `FOR` loop.

## Syntax

```osl
FOR variable = start TO end STEP increment
    ' statements
END
```

## Description

`STEP` specifies how much to add to (or subtract from) the loop variable on each iteration. If omitted, the step defaults to `1`.

## Example

```osl
FOR i = 0 TO 10 STEP 2
    PRINT i
END
' Prints: 0 2 4 6 8 10

FOR i = 10 TO 0 STEP -1
    PRINT i
END
' Prints: 10 9 8 7 6 5 4 3 2 1 0
```

## Related

- [FOR](/src/docs/oslang/reference/keywords/for.md)
- [TO](/src/docs/oslang/reference/keywords/to.md)
- [Control Flow](/src/docs/oslang/guide/control-flow.md)
