# OBJECT

Base type for all class instances.

## Syntax

```osl
VAR obj OBJECT
```

## Description

`OBJECT` is the base type for all objects in OSLANG. Every class instance is also an `OBJECT`. You can use `OBJECT` as a type annotation for variables that should hold any class instance.

## Example

```osl
VAR obj OBJECT
obj = NEW Person("Alice")
obj = NEW Button()
```

## Related

- [CLASS](/src/docs/oslang/reference/keywords/class.md)
- [NEW](/src/docs/oslang/reference/keywords/new.md)
- [Classes and Objects](/src/docs/oslang/guide/classes-and-objects.md)
