# CLASS

Defines a class.

## Syntax

```osl
CLASS ClassName [EXTENDS BaseClass] [, INTERFACE1, INTERFACE2, ...]
    ' properties, constructors, and methods
END CLASS
```

## Description

`CLASS` creates a new object type. Inside the class body you can declare:

- Properties with `VAR`
- Constructors with `CONSTRUCTOR`
- Methods with identifiers or `FUNCTION`

## Example

```osl
CLASS Person
    VAR Name STRING
    VAR Age NUMBER

    CONSTRUCTOR(Name STRING)
        ME.Name = Name
    END CONSTRUCTOR

    PUBLIC FUNCTION GREET()
        RETURN "Hello, " + ME.Name
    END FUNCTION
END CLASS
```

## Related

- [Classes and Objects](/src/docs/oslang/guide/classes-and-objects.md)
- [INTERFACE](/src/docs/oslang/reference/keywords/interface.md)
- [NEW](/src/docs/oslang/reference/keywords/new.md)
- [ME](/src/docs/oslang/reference/keywords/me.md)
