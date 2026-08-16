# OSLANG 0.61 Specification

**Language:** OSLANG  
**Version:** 0.61  
**Base Version:** 0.6  
**File Extension:** `.osl`  
**Release Type:** Additive / Non-Breaking  
**Breaking Changes:** None

## 1. Overview

OSLANG 0.61 adds:

- Enumerated types (`ENUM`).
- Numeric and string enum values.
- Enum sets using `|`.
- `SWITCH`, `CASE`, and `DEFAULT`.
- `BREAK` for `SWITCH`, `FOR`, `WHILE`, and `DO WHILE`.
- String interpolation with `${...}`.
- Multiline strings using triple quotes.
- Basic string escapes: `\n`, `\t`, and `\\`.

All existing OSLANG 0.6 behavior remains valid.

## 2. ENUM

Enums define a named set of values.

```osl
ENUM Color

    RED
    GREEN
    BLUE

END
```

Usage:

```osl
Color = Color.RED

IF Color = Color.RED THEN
    PRINT "Red"
END
```

### 2.1 Numeric values

Enum members may have numeric values:

```osl
ENUM Weekday

    Sunday = 0
    Monday = 1
    Tuesday = 2
    Wednesday = 3
    Thursday = 4
    Friday = 5
    Saturday = 6

END
```

When no value is specified, members receive sequential numeric values beginning at 0.

### 2.2 String values

Enum members may also have string values:

```osl
ENUM LogLevel

    DEBUG = "debug"
    INFO = "info"
    WARNING = "warning"
    ERROR = "error"

END
```

An enum must use a consistent underlying value type: `NUMBER` or `STRING`.

### 2.3 TYPEOF

`TYPEOF` returns the enum's type name:

```osl
Value = Weekday.Saturday

PRINT TYPEOF(Value)
```

Result:

```text
Weekday
```

The enum member remains an enum value even when its underlying value is numeric or string.

### 2.4 Enum methods

Enums provide:

- `NAME()`
- `VALUE()`
- `TOSTRING()`

Example:

```osl
Value = LogLevel.ERROR

PRINT Value.NAME()
PRINT Value.VALUE()
PRINT Value.TOSTRING()
```

Results:

```text
ERROR
error
error
```

For numeric enums:

```osl
PRINT Weekday.Saturday.VALUE()
```

returns:

```text
6
```

## 3. Enum Sets

Multiple values from the same enum can be combined with `|`.

```osl
Weekend = Weekday.Saturday | Weekday.Sunday
```

`Weekend` is an enum set containing both values.

### 3.1 Valid combinations

```text
EnumValue | EnumValue
EnumSet   | EnumValue
EnumValue | EnumSet
EnumSet   | EnumSet
```

All operands must belong to the same enum.

This is invalid:

```osl
Mixed = Weekday.Saturday | LogLevel.ERROR
```

The `|` operator is not a general-purpose bitwise operator in OSLANG 0.61.

### 3.2 Combining sets

```osl
Weekend = Weekday.Saturday | Weekday.Sunday
DaysOff = Weekend | Weekday.Monday
```

Duplicate values are removed.

### 3.3 Enum set methods

Enum sets support:

- `CONTAINS()`
- `COUNT()`
- `FOREACH()`

Example:

```osl
Weekend = Weekday.Saturday | Weekday.Sunday

IF Weekend.CONTAINS(Weekday.Saturday) THEN
    PRINT "Weekend"
END

PRINT Weekend.COUNT()
```

Enum sets can be iterated:

```osl
Weekend.FOREACH(Day => PRINT Day.NAME())
```

Enum sets are immutable in 0.61. There is no `PUSH()`, `POP()`, `ADD()`, or `REMOVE()` for enum sets.

## 4. SWITCH

`SWITCH` selects a branch according to an expression.

```osl
SWITCH Expression

    CASE Value
        Statements

    CASE Value
        Statements

    DEFAULT
        Statements

END
```

Example:

```osl
SWITCH Day

    CASE Weekday.Saturday
        PRINT "Saturday"

    CASE Weekday.Sunday
        PRINT "Sunday"

    DEFAULT
        PRINT "Weekday"

END
```

The first matching `CASE` is executed.

### 4.1 Strings

```osl
Command = "start"

SWITCH Command

    CASE "start"
        PRINT "Starting"

    CASE "stop"
        PRINT "Stopping"

    CASE "restart"
        PRINT "Restarting"

    DEFAULT
        PRINT "Unknown command"

END
```

### 4.2 Numbers

```osl
Status = 404

SWITCH Status

    CASE 200
        PRINT "OK"

    CASE 404
        PRINT "Not Found"

    CASE 500
        PRINT "Server Error"

    DEFAULT
        PRINT "Unknown status"

END
```

### 4.3 Enums

```osl
Day = Weekday.Saturday

SWITCH Day

    CASE Weekday.Saturday
        PRINT "Saturday"

    CASE Weekday.Sunday
        PRINT "Sunday"

    DEFAULT
        PRINT "Weekday"

END
```

### 4.4 Multiple enum values

Enum sets can be used in a `CASE`:

```osl
SWITCH Day

    CASE Weekday.Saturday | Weekday.Sunday
        PRINT "Weekend"

    DEFAULT
        PRINT "Weekday"

END
```

### 4.5 DEFAULT

`DEFAULT` is optional and may occur at most once.

```osl
SWITCH Value

    CASE 1
        PRINT "One"

    DEFAULT
        PRINT "Other"

END
```

### 4.6 Fall-through

OSLANG 0.61 has no implicit fall-through.

Once a `CASE` matches, its statements execute and the `SWITCH` completes normally.

## 5. BREAK

`BREAK` exits the nearest enclosing breakable control structure.

In 0.61, the following structures are breakable:

- `SWITCH`
- `FOR`
- `WHILE`
- `DO WHILE`

### 5.1 BREAK in SWITCH

```osl
SWITCH Value

    CASE 1
        PRINT "One"
        BREAK

    CASE 2
        PRINT "Two"

END
```

### 5.2 BREAK in FOR

```osl
FOR Number = 1 TO 100

    IF Number > 10 THEN
        BREAK
    END

    PRINT Number

END
```

### 5.3 BREAK in WHILE

```osl
Number = 1

WHILE Number <= 100

    IF Number > 10 THEN
        BREAK
    END

    PRINT Number
    Number++

END
```

### 5.4 BREAK in DO WHILE

```osl
DO

    IF Condition THEN
        BREAK
    END

    PRINT "Running"

WHILE Condition

END
```

### 5.5 Nested structures

`BREAK` always affects the nearest enclosing breakable structure.

```osl
FOR Number = 1 TO 10

    SWITCH Number

        CASE 5
            BREAK

    END

END
```

The `BREAK` above exits the `SWITCH`, not the `FOR`.

## 6. String Escape Sequences

OSLANG 0.61 initially supports:

| Escape | Meaning |
| ------ | ------- |
| `\n` | Newline |
| `\t` | Tab |
| `\\` | Backslash |

Examples:

```osl
Message = "Hello\nWorld"
Message = "Name:\tYgor"
Path = "C:\\OSB\\DATA"
```

Unknown escape sequences are invalid.

## 7. Multiline Strings

Triple quotes create a multiline `STRING`.

```osl
Message = """
Hello!

This is a multiline string.
It can contain multiple lines.
"""

PRINT Message
```

A multiline string is still of type `STRING`.

```osl
PRINT TYPEOF(Message)
```

returns:

```text
STRING
```

### 7.1 Indentation

Common indentation is removed from the content.

```osl
FUNCTION TEST()

    Message = """
        Hello
        World
    """

END
```

The resulting content is conceptually:

```text
Hello
World
```

Relative indentation is preserved.

## 8. String Interpolation

String interpolation uses `${...}`.

```osl
Name = "Ygor"
Age = 40

Message = "Hello ${Name}, you are ${Age} years old."

PRINT Message
```

Result:

```text
Hello Ygor, you are 40 years old.
```

### 8.1 Expressions

Interpolation accepts expressions:

```osl
Price = 10
Quantity = 5

PRINT "Total: ${Price * Quantity}"
```

Result:

```text
Total: 50
```

Methods may also be used:

```osl
Name = "ygor"

PRINT "Name: ${Name.TOUPPER()}"
```

### 8.2 Arrays

```osl
Numbers = [1, 2, 3]

PRINT "Numbers: ${Numbers.JOIN(", ")}"
```

### 8.3 Escaping interpolation

A literal `${` is written as:

```osl
Message = "\${Name}"
```

Result:

```text
${Name}
```

## 9. Multiline Strings with Interpolation

Interpolation works inside multiline strings:

```osl
Name = "Ygor"
Year = 2026

Message = """
Hello ${Name}!

We are in ${Year}.

This is a multiline
interpolated string.
"""

PRINT Message
```

Escape sequences are also supported:

```osl
Message = """
Name:\tYgor
Line 1\nLine 2
"""
```

## 10. Grammar Additions

New keywords:

- `ENUM`
- `SWITCH`
- `CASE`
- `DEFAULT`
- `BREAK`

New syntax:

- `|`
- `${ ... }`
- `""" ... """`
- `\n`
- `\t`
- `\\`

The parser must distinguish `|` as enum-set construction rather than treating it as a general bitwise operator.

## 11. Backward Compatibility

OSLANG 0.61 is additive and non-breaking.

Existing OSLANG 0.6 features remain valid, including:

- Variables
- `NULL`
- `NUMBER`
- `STRING`
- `BOOLEAN`
- Arrays
- Functions
- Local and global scope
- Classes
- Interfaces
- Inheritance
- Constructors
- Properties
- `PUBLIC`
- `PRIVATE`
- `PROTECTED`
- `ME`
- `TRY`
- `CATCH`
- `IF`
- `ELIF`
- `ELSE`
- `FOR`
- `WHILE`
- `DO WHILE`
- `PRINT`
- `SHOW`
- `INPUT`
- `CLEAR`
- `MATH`
- `FILE`
- `DIR`
- Primitive methods
- Arrow functions
- Existing operators

No existing feature is removed.

## 12. Complete Example

```osl
ENUM Weekday

    Sunday = 0
    Monday = 1
    Tuesday = 2
    Wednesday = 3
    Thursday = 4
    Friday = 5
    Saturday = 6

END


Weekend = Weekday.Saturday | Weekday.Sunday

Day = Weekday.Saturday

SWITCH Day

    CASE Weekday.Saturday | Weekday.Sunday

        Message = """
Today is ${Day.NAME()}.
It is the weekend!
"""

        PRINT Message
        BREAK

    DEFAULT

        PRINT "It is a weekday."

END
```

## 13. Design Philosophy

OSLANG 0.61 continues the incremental evolution of the language.

The enum features work together:

```text
ENUM
  ↓
Named typed values
  ↓
Enum Sets with |
  ↓
SWITCH / CASE
  ↓
Clear control flow
```

Strings evolve into practical templates:

```text
STRING
  ↓
Escape sequences
  ↓
Multiline strings
  ↓
Interpolation
  ↓
Templates and generated text
```

`BREAK` provides a consistent escape mechanism for all current breakable control structures:

```text
SWITCH
FOR
WHILE
DO WHILE
```

## 14. Version Summary

OSLANG 0.61 adds:

**Enums**
- `ENUM`
- `NUMBER` enum values
- `STRING` enum values
- `NAME()`
- `VALUE()`
- `TOSTRING()`
- `TYPEOF()`

**Enum Sets**
- `|`
- `CONTAINS()`
- `COUNT()`
- `FOREACH()`

**Control Flow**
- `SWITCH`
- `CASE`
- `DEFAULT`
- `BREAK`

**Strings**
- `\n`
- `\t`
- `\\`
- Multiline Strings (`""" ... """`)
- Interpolation (`${expression}`)

## 15. Release Constraint

OSLANG 0.61 MUST remain fully backward compatible with OSLANG 0.6.

The release is additive.

No existing OSLANG 0.6 syntax or behavior may be removed or intentionally changed as part of this release.
