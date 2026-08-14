# CASE

Defines a case in a `SWITCH` block.

## Syntax (statement)

```osl
SWITCH expression
    CASE value
        ' statements
    END
END
```

## Syntax (expression)

```osl
result = SWITCH expression
    CASE value => resultExpression
```

## Description

`CASE` matches a single value against the `SWITCH` expression. The first matching case executes. Cases are evaluated in order.

## Example

```osl
SWITCH day
    CASE "Mon"
        PRINT "Monday"
    END
    CASE "Tue"
        PRINT "Tuesday"
    END
    DEFAULT
        PRINT "Unknown"
    END
END
```

## Related

- [SWITCH](/src/docs/oslang/reference/keywords/switch.md)
- [DEFAULT](/src/docs/oslang/reference/keywords/default.md)
- [Control Flow](/src/docs/oslang/guide/control-flow.md)
