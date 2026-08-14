# NEW

Creates a new instance of a class.

## Syntax

```osl
NEW ClassName([arguments])
```

## Description

`NEW` allocates a new object of the specified class and calls its constructor with the given arguments.

## Example

```osl
VAR p = NEW Person("Alice", 30)
PRINT p.NAME
```

## Related

- [CLASS](/src/docs/oslang/reference/keywords/class.md)
- [CONSTRUCTOR](/src/docs/oslang/reference/keywords/constructor.md)
- [ME](/src/docs/oslang/reference/keywords/me.md)
- [Classes and Objects](/src/docs/oslang/guide/classes-and-objects.md)
