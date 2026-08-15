# FUNCTION

Defines a function.

## Syntax

```osl
FUNCTION name([parameter1 TYPE, parameter2 TYPE, ...])
    ' statements
    RETURN value
END [FUNCTION]
```

## Description

`FUNCTION` declares a reusable block of code. The optional `FUNCTION` keyword at the end (`END FUNCTION`) can be shortened to just `END`.

Every OSLANG program must have exactly one `FUNCTION MAIN()` as its entry point. Optionally, `MAIN` can declare a single `ARRAY` parameter to receive command-line arguments: `FUNCTION MAIN(Args)`.

## Example

```osl
FUNCTION ADD(A NUMBER, B NUMBER)
    RETURN A + B
END FUNCTION

FUNCTION MAIN()
    PRINT ADD(2, 3)  ' 5
END FUNCTION
```

## Related

- [RETURN](/src/docs/oslang/reference/keywords/return.md)
- [VAR](/src/docs/oslang/reference/keywords/var.md)
- [Functions](/src/docs/oslang/guide/functions.md)
