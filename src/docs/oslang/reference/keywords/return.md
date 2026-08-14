# RETURN

Returns from a function.

## Syntax

```osl
RETURN [expression]
```

## Description

`RETURN` exits the current function and optionally passes a value back to the caller. If no expression is provided, the function returns `NULL`.

## Example

```osl
FUNCTION ADD(A NUMBER, B NUMBER)
    RETURN A + B
END FUNCTION

FUNCTION MAIN()
    VAR sum = ADD(2, 3)
    PRINT sum  ' 5
END FUNCTION
```

## Related

- [FUNCTION](/src/docs/oslang/reference/keywords/function.md)
- [Functions](/src/docs/oslang/guide/functions.md)
