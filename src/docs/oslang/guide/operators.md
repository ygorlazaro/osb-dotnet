# Operators

This guide covers the operators available in OSLANG, including arithmetic, comparison, logical, and string operators.

## Arithmetic Operators

| Operator | Description | Example |
|----------|-------------|---------|
| `+` | Addition or string concatenation | `5 + 3` → `8`, `"Hello" + "!"` → `"Hello!"` |
| `-` | Subtraction | `10 - 4` → `6` |
| `*` | Multiplication | `3 * 4` → `12` |
| `/` | Division | `10 / 4` → `2.5` |
| `%` | Modulo (remainder) | `10 % 3` → `1` |
| `MOD` | Modulo (keyword form) | `10 MOD 3` → `1` |
| `**` | Exponentiation | `2 ** 8` → `256` |

### Exponentiation

`**` raises a number to a power. It is right-associative and has higher precedence than multiplication, division, and `MOD`.

```osl
Result = 2 ** 8        ' 256
Result = 2 ** 3 ** 2   ' 512 (2^(3^2))
```

### String Concatenation

The `+` operator concatenates strings. If one operand is a string, the other is converted to a string automatically.

```osl
VAR greeting = "Hello, " + "World!"  ' "Hello, World!"
VAR message = "Score: " + 100         ' "Score: 100"
```

## Increment and Decrement Operators

OSLANG 0.6 adds postfix increment and decrement operators.

| Operator | Description | Example |
|----------|-------------|---------|
| `++` | Postfix increment | `Counter++` |
| `--` | Postfix decrement | `Counter--` |

These operators return the original value and then change the variable.

```osl
Counter = 10
Value = Counter++   ' Value = 10, Counter = 11
```

Prefix forms (`++Counter`, `--Counter`) are not allowed and produce a syntax error.

## Compound Assignment

| Operator | Description | Example |
|----------|-------------|---------|
| `+=` | Add and assign | `Total += 5` → `Total = Total + 5` |

```osl
Total = 10
Total += 5   ' Total is now 15
```

## Comparison Operators

| Operator | Description | Example |
|----------|-------------|---------|
| `=` | Equal | `5 = 5` → `TRUE` |
| `<>` | Not equal | `5 <> 3` → `TRUE` |
| `<` | Less than | `3 < 5` → `TRUE` |
| `>` | Greater than | `5 > 3` → `TRUE` |
| `<=` | Less than or equal | `3 <= 3` → `TRUE` |
| `>=` | Greater than or equal | `5 >= 5` → `TRUE` |

Comparison operators work on `NUMBER` and `STRING` values. Comparing incompatible types at runtime produces an error.

```osl
IF age >= 18 THEN
    PRINT "Adult"
END IF
```

## Logical Operators

| Operator | Description | Example |
|----------|-------------|---------|
| `AND` | Logical AND | `TRUE AND FALSE` → `FALSE` |
| `OR` | Logical OR | `TRUE OR FALSE` → `TRUE` |
| `NOT` | Logical NOT | `NOT TRUE` → `FALSE` |

Logical operators work on `BOOLEAN` values.

```osl
IF age >= 18 AND hasConsent = TRUE THEN
    PRINT "Allowed"
END IF
```

## Unary Operators

| Operator | Description | Example |
|----------|-------------|---------|
| `-` | Negation | `-5` → `-5` |
| `NOT` | Logical negation | `NOT TRUE` → `FALSE` |

## Operator Precedence

Operators are evaluated in the following order (highest precedence first):

1. Member access and indexing
2. Postfix `++` / `--`
3. Exponentiation `**`
4. Unary `-` and `NOT`
5. Multiplication / division / `MOD`
6. Addition / subtraction
7. Comparison operators (`=`, `<>`, `<`, `>`, `<=`, `>=`)
8. `AND`
9. `OR`
10. Assignment / compound assignment

Use parentheses to override precedence:

```osl
VAR result = (2 + 3) * 4  ' 20, not 14
```

## Type Coercion

OSLANG performs automatic type coercion in some contexts:

- `NUMBER` + `STRING` → `STRING` (concatenation)
- `STRING` + `NUMBER` → `STRING` (concatenation)
- `BOOLEAN` + `STRING` → `STRING` (concatenation)

Explicit conversion functions are available: `STR()`, `NUMBER()`, `BOOL()`. See [Variables and Types](/src/docs/oslang/guide/variables-and-types.md).

## Related Topics

- [Variables and Types](/src/docs/oslang/guide/variables-and-types.md)
- [Control Flow](/src/docs/oslang/guide/control-flow.md)
