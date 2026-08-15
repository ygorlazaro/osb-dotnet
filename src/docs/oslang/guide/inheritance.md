# Inheritance

This guide explains how to extend classes using inheritance, `VIRTUAL` methods, and `OVERRIDE`.

## Extending a Class

Use the `EXTENDS` keyword after the class name:

```osl
CLASS Child EXTENDS Parent
    ' additional members
END CLASS
```

Only single class inheritance is supported. A class can implement multiple interfaces but extend only one class.

## Example

```osl
CLASS Animal
    VAR Name STRING

    CONSTRUCTOR(Name STRING)
        ME.Name = Name
    END CONSTRUCTOR

    PUBLIC FUNCTION SPEAK()
        RETURN "..."
    END FUNCTION
END CLASS

CLASS Dog EXTENDS Animal
    FUNCTION SPEAK()
        RETURN "Woof!"
    END FUNCTION
END CLASS

FUNCTION MAIN()
    VAR d = NEW Dog("Rex")
    PRINT d.SPEAK()  ' "Woof!"
END FUNCTION
```

## Virtual Methods

Use `VIRTUAL` to mark a method that can be overridden by subclasses:

```osl
CLASS Shape
    VIRTUAL FUNCTION AREA()
        RETURN 0
    END FUNCTION
END CLASS
```

## Override Methods

Use `OVERRIDE` in a subclass to replace a virtual method:

```osl
CLASS Rectangle EXTENDS Shape
    VAR Width NUMBER
    VAR Height NUMBER

    CONSTRUCTOR(Width NUMBER, Height NUMBER)
        ME.Width = Width
        ME.Height = Height
    END CONSTRUCTOR

    OVERRIDE FUNCTION AREA()
        RETURN ME.Width * ME.Height
    END FUNCTION
END CLASS
```

## Calling the Base Implementation

Use `BASE` to call the parent class implementation:

```osl
CLASS LoggedShape EXTENDS Shape
    OVERRIDE FUNCTION AREA()
        PRINT "Calculating area..."
        RETURN BASE.AREA()
    END FUNCTION
END CLASS
```

## Abstract Classes

While OSLANG 0.4 does not have a dedicated `ABSTRACT` keyword, you can simulate abstract classes by raising a runtime error in the base method:

```osl
CLASS Shape
    VIRTUAL FUNCTION AREA()
        ERROR "AREA() must be overridden"
    END FUNCTION
END CLASS
```

## Related Topics

- [Classes and Objects](/src/docs/oslang/guide/classes-and-objects.md)
- [Interfaces](/src/docs/oslang/guide/interfaces.md)
- [Virtual and Override Reference](/src/docs/oslang/reference/keywords/virtual.md)
- [Override Reference](/src/docs/oslang/reference/keywords/override.md)
