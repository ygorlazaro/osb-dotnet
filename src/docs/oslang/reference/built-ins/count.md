# COUNT

Returns the number of elements in a string or array.

## Syntax

```osl
COUNT(expression)
```

## Description

`COUNT()` returns the length of a string or the number of elements in an array.

- For strings, it returns the number of characters.
- For arrays, it returns the number of elements.

## Example

```osl
VAR name = "Alice"
PRINT COUNT(name)  ' 5

VAR arr = [1, 2, 3, 4]
PRINT COUNT(arr)   ' 4
```

## Related

- [Variables and Types](/src/docs/oslang/guide/variables-and-types.md)
- [Built-in Functions](/src/docs/oslang/reference/built-ins/index.md)
