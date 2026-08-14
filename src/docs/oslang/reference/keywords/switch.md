# SWITCH

Switch statement or expression.

## Statement Form

```osl
SWITCH expression
    CASE value1
        ' statements
    END
    CASE value2
        ' statements
    END
    DEFAULT
        ' statements
    END
END
```

## Expression Form

```osl
result = SWITCH expression
    CASE value1 => result1
    CASE value2 => result2
    DEFAULT => defaultResult
```

## Description

`SWITCH` matches `expression` against `CASE` values. The first matching case executes. If no case matches, the `DEFAULT` branch runs (if present).

In expression form, `SWITCH` returns the value from the matched branch.

## Example

```osl
FUNCTION GET_DAY_NAME(day NUMBER)
    SWITCH day
        CASE 1
            RETURN "Monday"
        END
        CASE 2
            RETURN "Tuesday"
        END
        DEFAULT
            RETURN "Unknown"
        END
    END
END FUNCTION
```

## Related

- [CASE](/src/docs/oslang/reference/keywords/case.md)
- [DEFAULT](/src/docs/oslang/reference/keywords/default.md)
- [Control Flow](/src/docs/oslang/guide/control-flow.md)
