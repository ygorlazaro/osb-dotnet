# Variables and Types

This guide explains how to declare variables, use built-in types, and perform type conversions in OSLANG.

## Declaring Variables

Use the `VAR` keyword to declare a variable:

```osl
VAR age NUMBER
VAR name STRING
VAR isActive BOOLEAN
```

Variables can also be declared without an explicit type:

```osl
VAR x
```

In this case, the variable is dynamically typed and can hold any value.

## Built-in Types

OSLANG provides three built-in primitive types:

| Type | Description | Literal Example |
|------|-------------|-----------------|
| `NUMBER` | Double-precision floating-point number | `42`, `3.14`, `-0.5` |
| `STRING` | Text string | `"Hello"`, `"World"` |
| `BOOLEAN` | Boolean value | `TRUE`, `FALSE` |
| `OBJECT` | Base type for all class instances | (see [Classes and Objects](/src/docs/oslang/guide/classes-and-objects.md)) |

## Type Annotations

When declaring a variable, you can optionally specify its type:

```osl
VAR count NUMBER
VAR title STRING
VAR active BOOLEAN
```

If you omit the type, the variable is untyped and can hold any value.

## Assignment

Assign values to variables using the `=` operator:

```osl
VAR x NUMBER
x = 10
x = x + 5
```

You can also initialize variables at declaration time:

```osl
VAR name STRING = "Alice"
VAR age NUMBER = 30
```

## Type Conversion

OSLANG provides built-in functions for converting between types:

- `STR(value)` — Converts a value to a `STRING`.
- `NUMBER(value)` — Converts a value to a `NUMBER`.
- `BOOL(value)` — Converts a value to a `BOOLEAN`.

Example:

```osl
VAR n NUMBER = 42
VAR s STRING = STR(n)      ' s = "42"

VAR s2 STRING = "3.14"
VAR n2 NUMBER = NUMBER(s2) ' n2 = 3.14
```

## Dynamic Typing

OSLANG uses dynamic typing with stable variable types. Once a variable holds a value of a certain type, operations on it are validated at runtime.

```osl
VAR x = 10      ' x is a NUMBER
x = "Hello"     ' x is now a STRING
x = TRUE        ' x is now a BOOLEAN
```

## Constants

Use `GLOBAL` to declare a global constant or variable:

```osl
GLOBAL PI = 3.14159
GLOBAL APP_NAME = "MyApp"
```

`GLOBAL` declarations are accessible from any function or method in the program.

## Null

The special value `NULL` represents the absence of a value.

```osl
VAR result = NULL
IF result = NULL THEN
    PRINT "No result"
END IF
```

## Scope

Variables declared with `VAR` inside a function or method are local to that block. Variables declared at the top level (outside any function) are global.

```osl
VAR globalVar = "global"

FUNCTION MAIN()
    VAR localVar = "local"
    PRINT globalVar  ' OK
    PRINT localVar   ' OK
END FUNCTION

' PRINT localVar     ' ERROR: not accessible here
```

## Related Topics

- [Syntax Basics](/src/docs/oslang/guide/syntax-basics.md)
- [Operators](/src/docs/oslang/guide/operators.md)
- [Functions](/src/docs/oslang/guide/functions.md)
- [Classes and Objects](/src/docs/oslang/guide/classes-and-objects.md)
