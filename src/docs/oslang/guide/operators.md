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

### String Concatenation

The `+` operator concatenates strings. If one operand is a string, the other is converted to a string automatically.

```osl
VAR greeting = "Hello, " + "World!"  ' "Hello, World!"
VAR message = "Score: " + 100         ' "Score: 100"
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

1. Unary `-` and `NOT`
2. `*`, `/`, `%`
3. `+`, `-` (and string concatenation)
4. Comparison operators (`=`, `<>`, `<`, `>`, `<=`, `>=`)
5. `AND`
6. `OR`

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
