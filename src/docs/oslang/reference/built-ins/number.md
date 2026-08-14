# NUMBER

Converts a value to a number.

## Syntax

```osl
NUMBER(value)
```

## Description

`NUMBER()` converts strings and other values to numbers.

- `"42"` → `42`
- `"3.14"` → `3.14`
- `"0"` → `0`
- Non-numeric strings produce a runtime error.

## Example

```osl
VAR n = NUMBER("42")     ' 42
VAR pi = NUMBER("3.14")  ' 3.14
```

## Related

- [STR](/src/docs/oslang/reference/built-ins/str.md)
- [BOOL](/src/docs/oslang/reference/built-ins/bool.md)
- [Variables and Types](/src/docs/oslang/guide/variables-and-types.md)
