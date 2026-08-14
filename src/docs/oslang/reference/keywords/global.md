# GLOBAL

Declares a global variable.

## Syntax

```osl
GLOBAL name [= expression]
```

## Description

`GLOBAL` declares a variable accessible from any function or method in the program. Unlike `VAR` inside a function, `GLOBAL` variables persist for the lifetime of the program.

## Example

```osl
GLOBAL counter = 0

FUNCTION INCREMENT()
    counter = counter + 1
END FUNCTION

FUNCTION MAIN()
    INCREMENT()
    INCREMENT()
    PRINT counter  ' 2
END FUNCTION
```

## Related

- [VAR](/src/docs/oslang/reference/keywords/var.md)
- [Variables and Types](/src/docs/oslang/guide/variables-and-types.md)
