# CATCH

Catches a runtime error inside a `TRY` block.

## Syntax

```osl
TRY
    ' statements that may fail
CATCH err
    ' error handling
END
```

## Description

`CATCH` defines the error handler for a `TRY` block. The variable after `CATCH` receives the error message as a `STRING`.

If no error occurs, the `CATCH` block is skipped.

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

- [TRY](/src/docs/oslang/reference/keywords/try.md)
- [Error Handling](/src/docs/oslang/guide/error-handling.md)
