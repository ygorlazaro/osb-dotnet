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

OSLANG provides the following built-in types:

| Type | Description | Literal Example |
|------|-------------|-----------------|
| `NUMBER` | Double-precision floating-point number | `42`, `3.14`, `-0.5` |
| `STRING` | Text string | `"Hello"`, `"World"` |
| `BOOLEAN` | Boolean value | `TRUE`, `FALSE` |
| `DATE` | Calendar date | `DATE.NOW()`, `DATE.NEW(2026, 8, 15)` |
| `TIME` | Time of day | `TIME.NOW()`, `TIME.NEW(13, 30, 45)` |
| `ARRAY` | List of values | `[1, 2, 3]`, `["a", "b"]` |
| `OBJECT` | Base type for all class instances | (see [Classes and Objects](/src/docs/oslang/guide/classes-and-objects.md)) |
| `NULL` | Absence of a value | `NULL` |

## Nested Arrays

OSLANG 0.51 supports arrays containing arrays:

```osl
Matrix = [[1, 2], [3, 4], [5, 6]]
PRINT Matrix[0][1]   ' 2
PRINT Matrix[2][0]   ' 5
```

Nested arrays follow the same homogeneous-type rule: all elements at each level must be the same type.

## TYPEOF

`TYPEOF` returns the runtime type name of a value as a `STRING`.

```osl
PRINT TYPEOF(10)        ' "NUMBER"
PRINT TYPEOF("Hello")   ' "STRING"
PRINT TYPEOF(TRUE)      ' "BOOLEAN"
PRINT TYPEOF([1, 2, 3]) ' "ARRAY"
PRINT TYPEOF(NULL)      ' "NULL"
```

For class instances, `TYPEOF` returns the class name.

```osl
CLASS Color
END CLASS

Value = NEW Color()
PRINT TYPEOF(Value)  ' "Color"
```

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

## Primitive Methods

OSLANG 0.4 adds methods directly on primitive values.

### String Methods

OSLANG 0.4 string methods:

```osl
Name = "Ygor"
PRINT Name.TOUPPER()        ' "YGOR"
PRINT Name.TOLOWER()        ' "ygor"
PRINT "  hi  ".TRIM()       ' "hi"
PRINT Name.LENGTH()         ' 4
PRINT Name.SUBSTR(0, 2)     ' "Yg"
PRINT Name.CONTAINS("go")   ' TRUE
PRINT Name.REVERSE()        ' "rogY"
PRINT "Açúcar".NORMALIZE()  ' "ACUCAR"
```

OSLANG 0.51 adds:

```osl
PRINT "42".PADSTART(5, "0")   ' "00042"
PRINT "42".PADEND(5, "0")     ' "42000"
PRINT "OS".REPEAT(3)          ' "OSOSOS"
```

### Number Methods

```osl
Value = 3.141592
PRINT Value.TRUNC(2)    ' 3.14
PRINT Value.TRUNC()     ' 3
```

### Array Methods

OSLANG 0.4 array methods:

```osl
Numbers = [3, 1, 2]
PRINT Numbers.COUNT()       ' 3
PRINT Numbers.FIRST()       ' 3
PRINT Numbers.LAST()        ' 2
PRINT Numbers.SORT()        ' [1, 2, 3]
PRINT Numbers.JOIN(", ")    ' "3, 1, 2"
```

OSLANG 0.51 adds:

```osl
Numbers = [1, 2, 3, 4]

' Search
PRINT Numbers.CONTAINS(3)           ' TRUE
PRINT Numbers.INDEXOF(3)            ' 1
PRINT Numbers.FINDINDEX(X => X > 2) ' 2

' Mutation
Numbers.PUSH(5)           ' [1, 2, 3, 4, 5]
Last = Numbers.POP()      ' 5, Numbers is now [1, 2, 3, 4]
Numbers.REMOVE(2)         ' removes first 2

' Transformation
Rev = Numbers.REVERSE()   ' [4, 3, 2, 1]
Sorted = Numbers.SORT()   ' [1, 2, 3, 4]
Str = Numbers.JOIN(", ")  ' "4, 3, 2, 1"

' Nested arrays
Matrix = [[1, 2], [3, 4]]
Flat = Matrix.FLAT()      ' [1, 2, 3, 4]
Result = Matrix.FLATMAP(X => [X, X * 10])  ' [[1,2],[3,4]] -> [1, 2, 10, 3, 4, 20]

' Side-effect iteration
Sum = 0
Numbers.FOREACH(X => Sum = Sum + X)  ' Sum = 10
```

### Callback Support

Array functional methods accept function references:

```osl
FUNCTION IS_EVEN(x)
    RETURN x MOD 2 = 0
END FUNCTION

Even = [1, 2, 3, 4].FILTER(IS_EVEN)  ' [2, 4]
```

See [Namespaces](/src/docs/oslang/guide/namespaces.md) for the full list of primitive methods.

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
