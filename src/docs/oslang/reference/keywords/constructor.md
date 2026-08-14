# CONSTRUCTOR

Defines a constructor for a class.

## Syntax

```osl
CONSTRUCTOR([parameter1 TYPE, parameter2 TYPE, ...])
    ' initialization statements
END [CONSTRUCTOR]
```

## Description

A constructor runs when a new instance is created with `NEW`. Use `ME` to access the current instance and initialize its properties.

## Example

```osl
CLASS Person
    VAR Name STRING
    VAR Age NUMBER

    CONSTRUCTOR(Name STRING, Age NUMBER)
        ME.Name = Name
        ME.Age = Age
    END CONSTRUCTOR
END CLASS
```

## Related

- [CLASS](/src/docs/oslang/reference/keywords/class.md)
- [NEW](/src/docs/oslang/reference/keywords/new.md)
- [ME](/src/docs/oslang/reference/keywords/me.md)
- [Classes and Objects](/src/docs/oslang/guide/classes-and-objects.md)
