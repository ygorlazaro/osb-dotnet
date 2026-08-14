# Control Flow

This guide covers conditional statements and loops in OSLANG: `IF`/`ELIF`/`ELSE`, `FOR`, `WHILE`, `DO`/`WHILE`, and `SWITCH`/`CASE`/`DEFAULT`.

## IF / ELIF / ELSE

Execute code conditionally.

### Syntax

```osl
IF condition THEN
    ' statements
ELIF condition THEN
    ' statements
ELSE
    ' statements
END
```

### Example

```osl
IF score >= 90 THEN
    PRINT "A"
ELIF score >= 80 THEN
    PRINT "B"
ELSE
    PRINT "F"
END
```

### Single-line form

```osl
IF x > 0 THEN PRINT "Positive"
```

## SWITCH / CASE / DEFAULT

Match a value against multiple cases.

### Statement Form

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

### Expression Form

`SWITCH` can also be used as an expression that returns a value:

```osl
Result = SWITCH day
    CASE "Mon" => "Weekday"
    CASE "Tue" => "Weekday"
    CASE "Wed" => "Weekday"
    CASE "Thu" => "Weekday"
    CASE "Fri" => "Weekday"
    CASE "Sat" => "Weekend"
    CASE "Sun" => "Weekend"
    DEFAULT => "Unknown"
PRINT Result
```

### Example

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

## FOR Loop

Iterate over a range of numbers.

### Syntax

```osl
FOR variable = start TO end [STEP increment]
    ' statements
END
```

- `start` and `end` are expressions that evaluate to numbers.
- `increment` is optional; defaults to `1`.
- Use `STEP -1` to count down.

### Example

```osl
FOR i = 1 TO 10
    PRINT i
END

FOR i = 10 TO 1 STEP -1
    PRINT i
END
```

## WHILE Loop

Repeat while a condition is true.

### Syntax

```osl
WHILE condition
    ' statements
END
```

### Example

```osl
VAR i = 1
WHILE i <= 5
    PRINT i
    i = i + 1
END
```

## DO / WHILE Loop

Execute the body at least once, then repeat while the condition is true.

### Syntax

```osl
DO
    ' statements
WHILE condition
```

### Example

```osl
VAR input = ""
DO
    INPUT "Enter text (or 'quit' to stop): ", input
    PRINT "You typed: " + input
WHILE input <> "quit"
```

## BREAK and CONTINUE

Inside loops:

- `BREAK` — exits the loop immediately.
- `CONTINUE` — skips the rest of the current iteration and continues with the next.

```osl
FOR i = 1 TO 100
    IF i = 5 THEN
        CONTINUE
    END IF
    IF i = 10 THEN
        BREAK
    END IF
    PRINT i
END
```

## Related Topics

- [Syntax Basics](/src/docs/oslang/guide/syntax-basics.md)
- [Operators](/src/docs/oslang/guide/operators.md)
- [Functions](/src/docs/oslang/guide/functions.md)
