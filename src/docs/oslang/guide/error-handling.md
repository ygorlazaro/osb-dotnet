# Error Handling

This guide explains how to catch and handle runtime errors using `TRY` and `CATCH`.

## TRY / CATCH

Wrap risky code in a `TRY` block and handle errors in `CATCH`:

```osl
TRY
    result = 10 / 0
CATCH err
    PRINT "Error: " + err
END
```

The `CATCH` variable receives the error message as a string.

## Example

```osl
FUNCTION SAFE_DIVIDE(A NUMBER, B NUMBER)
    VAR result = 0
    TRY
        result = A / B
    CATCH err
        PRINT "Division failed: " + err
        result = 0
    END
    RETURN result
END FUNCTION

FUNCTION MAIN()
    PRINT SAFE_DIVIDE(10, 2)  ' 5
    PRINT SAFE_DIVIDE(10, 0)  ' Error message, then 0
END FUNCTION
```

## Common Runtime Errors

- **Division by zero** — Dividing a number by zero.
- **Type mismatch** — Performing an operation on incompatible types.
- **Undefined variable** — Using a variable that was never declared.
- **Unknown class** — Using `NEW` with a class name that does not exist.
- **Index out of bounds** — Accessing an array with an invalid index.
- **Stack overflow** — Infinite or excessively deep recursion.

## Error Flow

If an error occurs inside a `TRY` block and there is no matching `CATCH`, the error propagates up the call stack.

```osl
FUNCTION LEVEL3()
    TRY
        ERROR "Something went wrong"
    END
END FUNCTION

FUNCTION LEVEL2()
    LEVEL3()
END FUNCTION

FUNCTION LEVEL1()
    TRY
        LEVEL2()
    CATCH err
        PRINT "Caught at level 1: " + err
    END
END FUNCTION
```

## Related Topics

- [Syntax Basics](/src/docs/oslang/guide/syntax-basics.md)
- [Functions](/src/docs/oslang/guide/functions.md)
