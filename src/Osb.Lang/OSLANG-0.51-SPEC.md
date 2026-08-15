# OSLANG 0.51 Specification

**Language:** OSLANG  
**Version:** 0.51  
**Base Version:** 0.5  
**File Extension:** `.osl`  
**Release Type:** Additive / Non-Breaking  
**Breaking Changes:** None

## 1. Overview

OSLANG 0.51 is an additive release over OSLANG 0.5.

Goals:

- Add non-breaking console output with `SHOW`.
- Introduce arrow functions.
- Add `MOD`, `**`, postfix `++`/`--`, and `+=`.
- Formalize `TYPEOF`.
- Support nested arrays.
- Expand array methods.
- Expand `MATH` with `PI` and trigonometric functions.
- Add decimal-place truncation.
- Expand string manipulation.

No existing OSLANG 0.5 feature is removed or changes its meaning.

## 2. SHOW

`SHOW` outputs a value without automatically adding a line break.

```osl
SHOW "Hello "
SHOW "World"
PRINT ""
```

Output:

```text
Hello World
```

`PRINT` retains its existing newline behavior.

Typical use:

```osl
SHOW "Enter your name: "
INPUT Name
```

## 3. Arrow Functions

Arrow functions are anonymous, first-class callable values using `=>`.

### Single parameter

```osl
X => X * 2
```

### Multiple parameters

```osl
(A, B) => A + B
```

### Zero parameters

```osl
() => MATH.RANDOM()
```

### Assignment

```osl
DOUBLE = X => X * 2

PRINT DOUBLE(10)
```

### As arguments

```osl
Numbers.FILTER(X => X MOD 2 = 0)
```

### Block form

```osl
DOUBLE = X =>

    Result = X * 2
    RETURN Result

END
```

### Closures

Arrow functions capture variables from their surrounding scope.

```osl
Multiplier = 10
Multiply = X => X * Multiplier

PRINT Multiply(5)
```

Arrow functions must be usable as variables, arguments, return values, and callable expressions.

## 4. MOD

`MOD` returns the remainder of a division.

```osl
Result = 10 MOD 3
PRINT Result
```

Result:

```text
1
```

Example:

```osl
IF Number MOD 2 = 0 THEN
    PRINT "Even"
ELIF Number MOD 2 = 1 THEN
    PRINT "Odd"
END
```

## 5. Exponentiation

`**` performs exponentiation.

```osl
Result = 2 ** 8
```

Equivalent to:

```osl
MATH.POW(2, 8)
```

Exponentiation has higher precedence than multiplication, division, and `MOD`, and is right-associative.

```osl
2 + 3 ** 2
```

means:

```text
2 + (3 ** 2)
```

and:

```osl
2 ** 3 ** 2
```

means:

```text
2 ** (3 ** 2)
```

## 6. Postfix Increment and Decrement

`++` and `--` are postfix-only.

Valid:

```osl
Counter++
Counter--
```

Invalid:

```osl
++Counter
--Counter
```

The expression returns the original value and then changes the variable.

```osl
Counter = 10
Value = Counter++

PRINT Value
PRINT Counter
```

Output:

```text
10
11
```

## 7. Compound Assignment

`+=` is equivalent to assigning the result of addition.

```osl
Total = 10
Total += 5
```

Equivalent to:

```osl
Total = Total + 5
```

Version 0.51 adds only `+=`.

## 8. TYPEOF

`TYPEOF` returns a `STRING` containing the runtime type name.

```osl
PRINT TYPEOF(10)
PRINT TYPEOF("Hello")
PRINT TYPEOF(TRUE)
PRINT TYPEOF([1, 2, 3])
PRINT TYPEOF(NULL)
```

Expected values:

```text
NUMBER
STRING
BOOLEAN
ARRAY
NULL
```

For class instances, it returns the class name.

```osl
CLASS Color
END

Value = NEW Color()
PRINT TYPEOF(Value)
```

Result:

```text
Color
```

## 9. Nested Arrays

Arrays may contain arrays.

```osl
Matrix = [
    [1, 2, 3],
    [4, 5, 6],
    [7, 8, 9]
]

PRINT Matrix[0][1]
```

Result:

```text
2
```

The existing homogeneous-array rule remains in force. An array cannot mix element types.

Conceptually:

```text
ARRAY<NUMBER>
ARRAY<STRING>
ARRAY<BOOLEAN>
ARRAY<ARRAY<NUMBER>>
ARRAY<ARRAY<STRING>>
```

Generic syntax is not exposed to programmers.

## 10. FINDINDEX

`FINDINDEX` searches using a predicate and returns the index of the first match.

```osl
Numbers = [5, 8, 13, 21]

Index = Numbers.FINDINDEX(X => X > 10)

PRINT Index
```

Result:

```text
2
```

If there is no match, it returns `-1`.

It must short-circuit after the first match.

`INDEXOF` remains the value-based search operation:

```osl
Numbers.INDEXOF(13)
```

## 11. FOREACH

`FOREACH` executes a function for every array element.

```osl
Numbers = [1, 2, 3]

Numbers.FOREACH(X => PRINT X)
```

It is intended for side effects and does not create a new array.

## 12. CONTAINS

`CONTAINS` tests whether an array contains a value.

```osl
Numbers = [1, 2, 3]

IF Numbers.CONTAINS(2) THEN
    PRINT "Found"
END
```

It returns `BOOLEAN`.

Nested arrays use value equality.

## 13. JOIN

`JOIN` converts an array into a string using a separator.

```osl
Names = ["Ygor", "Lara", "Dante"]

Text = Names.JOIN(", ")

PRINT Text
```

Result:

```text
Ygor, Lara, Dante
```

Elements are converted to their string representation as necessary.

## 14. PUSH

`PUSH` adds an element to the end of an array.

```osl
Numbers = [1, 2, 3]
Numbers.PUSH(4)
```

Result:

```text
[1, 2, 3, 4]
```

`PUSH` mutates the array.

Existing `ADD()` remains available.

## 15. POP

`POP` removes and returns the last element.

```osl
Numbers = [1, 2, 3]
Last = Numbers.POP()
```

Afterward:

```text
Numbers = [1, 2]
Last = 3
```

Calling `POP()` on an empty array produces a runtime error.

## 16. SORT

`SORT` returns a sorted array without modifying the original.

```osl
Numbers = [4, 1, 3, 2]
Sorted = Numbers.SORT()
```

Result:

```text
[1, 2, 3, 4]
```

## 17. FLAT

`FLAT` removes exactly one level of nesting.

```osl
Values = [
    [1, 2],
    [3, 4],
    [5, 6]
]

Result = Values.FLAT()
```

Result:

```text
[1, 2, 3, 4, 5, 6]
```

It does not recursively flatten every level.

## 18. FLATMAP

`FLATMAP` combines `MAP` with one-level `FLAT`.

```osl
Numbers = [1, 2, 3]

Result = Numbers.FLATMAP(X => [X, X * 10])
```

Result:

```text
[1, 10, 2, 20, 3, 30]
```

Conceptually:

```osl
Numbers.MAP(X => [X, X * 10]).FLAT()
```

## 19. Array API

The 0.51 array API includes:

```text
COUNT()
FIRST()
LAST()

CONTAINS(Value)
INDEXOF(Value)
FINDINDEX(Function)

ADD(Value)
PUSH(Value)
POP()
REMOVE(Value)
CLEAR()

REVERSE()
SORT()

JOIN(Separator)

MAP(Function)
FILTER(Function)
ANY(Function)
SOME(Function)
ALL(Function)
REDUCE(Function, Initial)
FOREACH(Function)

FLAT()
FLATMAP(Function)
```

All methods from 0.5 remain available.

## 20. MATH.PI

```osl
PRINT MATH.PI
```

`MATH.PI` is the mathematical constant pi.

## 21. Trigonometric Functions

The `MATH` namespace includes:

```text
MATH.SIN(Value)
MATH.COS(Value)
MATH.TAN(Value)

MATH.ASIN(Value)
MATH.ACOS(Value)
MATH.ATAN(Value)
MATH.ATAN2(Y, X)
```

Angles are expressed in radians.

Example:

```osl
Angle = MATH.PI / 2

PRINT MATH.SIN(Angle)
PRINT MATH.COS(Angle)
```

## 22. NUMBER.TRUNC

Numbers support truncation to a specified number of decimal places.

```osl
Value = 3.141592

PRINT Value.TRUNC(2)
```

Result:

```text
3.14
```

`TRUNC` truncates rather than rounds.

```osl
PRINT 3.149.TRUNC(2)
PRINT 3.199.TRUNC(2)
```

Results:

```text
3.14
3.19
```

`TRUNC(0)` removes the fractional portion.

The no-argument form remains available:

```osl
Value.TRUNC()
```

## 23. MATH.TRUNC

The existing one-argument form remains valid:

```osl
MATH.TRUNC(3.9)
```

returns:

```text
3
```

The decimal-place form is added:

```osl
MATH.TRUNC(3.14159, 2)
```

returns:

```text
3.14
```

## 24. STRING.PADSTART

`PADSTART` pads the beginning of a string until it reaches the requested length.

```osl
Value = "42"
Result = Value.PADSTART(5, "0")
```

Result:

```text
00042
```

If the padding value is omitted, spaces are used.

```osl
"42".PADSTART(5)
```

Result:

```text
   42
```

Strings already at or above the requested length are returned unchanged.

## 25. STRING.PADEND

`PADEND` pads the end of a string.

```osl
Value = "42"
Result = Value.PADEND(5, "0")
```

Result:

```text
42000
```

If the padding value is omitted, spaces are used.

## 26. STRING.REPEAT

`REPEAT` repeats a string.

```osl
Result = "-".REPEAT(10)
```

Result:

```text
----------
```

```osl
Result = "OS".REPEAT(3)
```

Result:

```text
OSOSOS
```

A count of zero returns an empty string. Negative counts produce a runtime error.

## 27. Integrated Examples

### Functional Collection Processing

```osl
FUNCTION MAIN()

    Numbers = [1, 2, 3, 4, 5, 6]

    Even = Numbers.FILTER(X => X MOD 2 = 0)
    Doubled = Even.MAP(X => X * 2)

    PRINT Doubled.JOIN(", ")

END
```

Output:

```text
4, 8, 12
```

### Matrix Processing

```osl
FUNCTION MAIN()

    Matrix = [
        [1, 2],
        [3, 4],
        [5, 6]
    ]

    Numbers = Matrix.FLAT()
    Sum = Numbers.REDUCE((A, B) => A + B, 0)

    PRINT "Sum = " + Sum.TOSTRING()

END
```

Output:

```text
Sum = 21
```

### FINDINDEX

```osl
FUNCTION MAIN()

    Names = ["Ana", "Carlos", "Ygor", "Lara"]

    Index = Names.FINDINDEX(X => X.TOLOWER() = "ygor")

    PRINT Index

END
```

Output:

```text
2
```

### FLATMAP

```osl
FUNCTION MAIN()

    Numbers = [1, 2, 3, 4]

    Result = Numbers.FLATMAP(X => [X, X ** 2])

    PRINT Result.JOIN(", ")

END
```

Output:

```text
1, 1, 2, 4, 3, 9, 4, 16
```

### String Formatting

```osl
FUNCTION MAIN()

    Number = 42
    Code = Number.TOSTRING().PADSTART(6, "0")

    PRINT Code

END
```

Output:

```text
000042
```

### SHOW

```osl
FUNCTION MAIN()

    FOR Number = 1 TO 10

        SHOW "Progress: "
        SHOW Number
        SHOW "/10"

        PRINT ""

    END

END
```

## 28. Grammar Additions

The lexer/parser must recognize:

```text
MOD
**
++
--
+=
=>
```

and:

```text
SHOW expression
```

### Arrow Function Grammar

Single parameter:

```text
identifier => expression
```

Multiple parameters:

```text
(parameter1, parameter2, ...) => expression
```

Zero parameters:

```text
() => expression
```

Block form:

```text
identifier =>

    statements

END
```

or:

```text
(parameter1, parameter2, ...) =>

    statements

END
```

## 29. Operator Precedence

Conceptual precedence, highest to lowest:

1. Member access and indexing
2. Postfix `++` / `--`
3. Exponentiation `**`
4. Unary operators
5. Multiplication / division / `MOD`
6. Addition / subtraction
7. Comparisons
8. Equality
9. Logical `AND`
10. Logical `OR`
11. Assignment / compound assignment

Parentheses override precedence.

## 30. Runtime Requirements

The runtime must support callable values for arrow functions.

Arrow functions must be capable of:

- being stored in variables;
- being passed as arguments;
- being returned from functions;
- being invoked;
- capturing surrounding variables.

Example:

```osl
FUNCTION APPLY(Value, Operation)

    RETURN Operation(Value)

END

Double = X => X * 2

PRINT APPLY(10, Double)
```

Output:

```text
20
```

## 31. Nested Array Runtime Requirements

The runtime must support recursive array values.

Conceptually:

```text
ARRAY<NUMBER>
ARRAY<STRING>
ARRAY<BOOLEAN>
ARRAY<ARRAY<NUMBER>>
ARRAY<ARRAY<STRING>>
```

The existing homogeneous-array rule remains mandatory.

## 32. Compatibility Requirements

OSLANG 0.51 is a **non-breaking release**.

Mandatory compatibility rules:

- `PRINT` retains its existing behavior.
- `ADD()` remains valid.
- `INDEXOF()` remains valid.
- Existing `MAP()` remains valid.
- Existing `FILTER()` remains valid.
- Existing `ANY()` remains valid.
- Existing `SOME()` remains valid.
- Existing `ALL()` remains valid.
- Existing `REDUCE()` remains valid.
- Existing `MATH` functionality remains valid.
- `MATH.TRUNC(Value)` retains its existing behavior.
- Existing `FILE` functionality remains valid.
- Existing `DIR` functionality remains valid.
- Classes remain compatible.
- Interfaces remain compatible.
- Inheritance remains compatible.
- Properties remain compatible.
- Methods remain compatible.
- Constructors remain compatible.
- `PUBLIC`, `PRIVATE`, and `PROTECTED` semantics remain unchanged.
- No existing OSLANG 0.5 syntax is removed.

## 33. Required Test Coverage

The 0.51 implementation must include tests for:

### SHOW

- output without newline;
- consecutive `SHOW` operations;
- interaction with `PRINT`.

### Arrow Functions

- one parameter;
- multiple parameters;
- zero parameters;
- expression body;
- block body;
- variable assignment;
- function arguments;
- function return values;
- lexical scope;
- invocation.

### Operators

- `MOD`;
- `**`;
- postfix `++`;
- postfix `--`;
- `+=`;
- operator precedence;
- right-associative exponentiation;
- rejection of prefix `++` and `--`.

### TYPEOF

- `NUMBER`;
- `STRING`;
- `BOOLEAN`;
- `ARRAY`;
- `NULL`;
- class instances.

### Arrays

- nested arrays;
- nested indexing;
- homogeneous nested arrays;
- `FINDINDEX`;
- `FOREACH`;
- `CONTAINS`;
- `JOIN`;
- `PUSH`;
- `POP`;
- `SORT`;
- `FLAT`;
- `FLATMAP`.

### MATH

- `PI`;
- `SIN`;
- `COS`;
- `TAN`;
- `ASIN`;
- `ACOS`;
- `ATAN`;
- `ATAN2`;
- `TRUNC`.

### STRING

- `PADSTART`;
- `PADEND`;
- `REPEAT`.

### Regression

All OSLANG 0.5 tests must continue to pass without modification.

## 34. Design Philosophy

OSLANG 0.51 expands the language without changing its fundamental identity.

The additions reinforce existing concepts:

```text
PRINT       -> output with newline
SHOW        -> output without newline

FUNCTION    -> named callable
=>          -> inline callable

INDEXOF     -> search by value
FINDINDEX   -> search by predicate

ADD         -> existing array insertion
PUSH        -> append
POP         -> remove and return

MAP         -> transform
FILTER      -> select
FOREACH     -> execute
FLAT        -> flatten
FLATMAP     -> transform + flatten
```

The result is a more expressive scripting language while preserving the BASIC-inspired character of OSLANG.

## 35. Version Summary

### Output

```text
SHOW
```

### Functions

```text
Arrow functions
Lexical closures
```

### Operators

```text
MOD
**
++
--
+=
```

### Runtime

```text
TYPEOF
```

### Arrays

```text
Nested arrays
FINDINDEX
FOREACH
CONTAINS
JOIN
PUSH
POP
SORT
FLAT
FLATMAP
```

### MATH

```text
MATH.PI
MATH.SIN()
MATH.COS()
MATH.TAN()
MATH.ASIN()
MATH.ACOS()
MATH.ATAN()
MATH.ATAN2()
MATH.TRUNC()
```

### NUMBER

```text
TRUNC()
```

### STRING

```text
PADSTART()
PADEND()
REPEAT()
```

## 36. Release Constraint

**OSLANG 0.51 MUST be fully backward compatible with OSLANG 0.5.**

The version is strictly additive.

No feature introduced before 0.51 may be removed, renamed, or have its existing semantics changed as part of this release.
