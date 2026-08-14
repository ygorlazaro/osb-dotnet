# TRY

Starts a try/catch block for error handling.

## Syntax

```osl
TRY
    ' statements
CATCH err
    ' error handling
END
```

## Description

`TRY` wraps code that might produce a runtime error. If an error occurs, control jumps to the `CATCH` block. The `CATCH` variable receives the error message as a string.

## Example

```osl
FUNCTION SAFE_DIVIDE(A, B)
    VAR result = 0
    TRY
        result = A / B
    CATCH err
        PRINT "Error: " + err
        result = 0
    END
    RETURN result
END FUNCTION
```

## Related

- [CATCH](/src/docs/oslang/reference/keywords/catch.md)
- [Error Handling](/src/docs/oslang/guide/error-handling.md)
