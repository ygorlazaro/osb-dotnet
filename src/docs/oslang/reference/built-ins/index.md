# Built-in Functions Reference

OSLANG includes the following built-in functions. They are available everywhere without importing any module.

| Function | Description | Example |
|----------|-------------|---------|
| `STR(value)` | Converts a value to a string. | `STR(42)` → `"42"` |
| `NUMBER(value)` | Converts a value to a number. | `NUMBER("3.14")` → `3.14` |
| `BOOL(value)` | Converts a value to a boolean. | `BOOL(0)` → `FALSE` |
| `SQRT(value)` | Returns the square root. | `SQRT(16)` → `4` |
| `ABS(value)` | Returns the absolute value. | `ABS(-5)` → `5` |
| `POW(base, exp)` | Returns `base` raised to `exp`. | `POW(2, 3)` → `8` |
| `FLOOR(value)` | Rounds down to the nearest integer. | `FLOOR(3.7)` → `3` |
| `CEIL(value)` | Rounds up to the nearest integer. | `CEIL(3.2)` → `4` |
| `COUNT(value)` | Returns the number of items in an array or string. | `COUNT("abc")` → `3` |
| `TYPEOF(value)` | Returns the type name as a string. | `TYPEOF(42)` → `"NUMBER"` |

## Detailed Documentation

- [`STR()`](/src/docs/oslang/reference/built-ins/str.md)
- [`NUMBER()`](/src/docs/oslang/reference/built-ins/number.md)
- [`BOOL()`](/src/docs/oslang/reference/built-ins/bool.md)
- [`SQRT()`](/src/docs/oslang/reference/built-ins/sqrt.md)
- [`ABS()`](/src/docs/oslang/reference/built-ins/abs.md)
- [`POW()`](/src/docs/oslang/reference/built-ins/pow.md)
- [`FLOOR()`](/src/docs/oslang/reference/built-ins/floor.md)
- [`CEIL()`](/src/docs/oslang/reference/built-ins/ceil.md)
- [`COUNT()`](/src/docs/oslang/reference/built-ins/count.md)
- [`TYPEOF()`](/src/docs/oslang/reference/built-ins/typeof.md)

## Related Topics

- [Variables and Types](/src/docs/oslang/guide/variables-and-types.md)
- [Operators](/src/docs/oslang/guide/operators.md)
- [Keywords Reference](/src/docs/oslang/reference/keywords/index.md)
