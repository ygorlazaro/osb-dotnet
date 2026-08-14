# BOOLEAN

Boolean type annotation.

## Syntax

```osl
VAR flag BOOLEAN
```

## Description

`BOOLEAN` is used as a type annotation for variables and parameters that hold logical values: `TRUE` or `FALSE`.

## Example

```osl
VAR isReady BOOLEAN
isReady = TRUE

IF isReady THEN
    PRINT "Ready"
END IF
```

## Related

- [TRUE](/src/docs/oslang/reference/keywords/true.md)
- [FALSE](/src/docs/oslang/reference/keywords/false.md)
- [NULL](/src/docs/oslang/reference/keywords/null.md)
- [Variables and Types](/src/docs/oslang/guide/variables-and-types.md)
