# NULL

Represents the absence of a value.

## Syntax

```osl
NULL
```

## Description

`NULL` is a special literal that represents no value. It is the default value for uninitialized variables and can be used to test whether a variable holds a meaningful value.

## Example

```osl
VAR result = NULL
IF result = NULL THEN
    PRINT "No result yet"
END IF
```

## Related

- [TRUE](/src/docs/oslang/reference/keywords/true.md)
- [FALSE](/src/docs/oslang/reference/keywords/false.md)
- [Variables and Types](/src/docs/oslang/guide/variables-and-types.md)
