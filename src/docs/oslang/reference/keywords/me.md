# ME

Refers to the current object instance.

## Syntax

```osl
ME.property
ME.method([arguments])
```

## Description

Inside a class method or constructor, `ME` refers to the instance whose method is currently executing. Use it to access properties and call other methods on the same object.

## Example

```osl
CLASS Person
    VAR Name STRING

    CONSTRUCTOR(Name STRING)
        ME.Name = Name
    END CONSTRUCTOR

    PUBLIC FUNCTION GREET()
        RETURN "Hello, " + ME.Name
    END FUNCTION
END CLASS
```

## Related

- [CLASS](/src/docs/oslang/reference/keywords/class.md)
- [NEW](/src/docs/oslang/reference/keywords/new.md)
- [Classes and Objects](/src/docs/oslang/guide/classes-and-objects.md)
