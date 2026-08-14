# TYPEOF

Returns the type name of a value.

## Syntax

```osl
TYPEOF(value)
```

## Description

`TYPEOF()` returns a string describing the runtime type of `value`.

Possible return values:

- `"NUMBER"`
- `"STRING"`
- `"BOOLEAN"`
- `"NULL"`
- `"OBJECT"` (for class instances)

## Example

```osl
PRINT TYPEOF(42)        ' "NUMBER"
PRINT TYPEOF("hello")   ' "STRING"
PRINT TYPEOF(TRUE)      ' "BOOLEAN"
PRINT TYPEOF(NULL)      ' "NULL"
```

## Related

- [Variables and Types](/src/docs/oslang/guide/variables-and-types.md)
- [Built-in Functions](/src/docs/oslang/reference/built-ins/index.md)
