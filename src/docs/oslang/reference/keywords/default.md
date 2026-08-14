# DEFAULT

Defines the default branch in a `SWITCH` block.

## Syntax

```osl
SWITCH expression
    CASE value
        ' statements
    END
    DEFAULT
        ' statements
    END
END
```

or (expression form):

```osl
result = SWITCH expression
    CASE value => result
    DEFAULT => defaultResult
```

## Description

`DEFAULT` runs when no `CASE` matches the `SWITCH` expression. A `SWITCH` can have at most one `DEFAULT` block.

## Example

```osl
SWITCH status
    CASE "ok"
        PRINT "All good"
    END
    CASE "error"
        PRINT "Something failed"
    END
    DEFAULT
        PRINT "Unknown status"
    END
END
```

## Related

- [SWITCH](/src/docs/oslang/reference/keywords/switch.md)
- [CASE](/src/docs/oslang/reference/keywords/case.md)
- [Control Flow](/src/docs/oslang/guide/control-flow.md)
