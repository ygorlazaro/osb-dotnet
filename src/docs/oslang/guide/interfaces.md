# Interfaces

This guide explains how to define and implement interfaces in OSLANG.

## Defining an Interface

Use the `INTERFACE` keyword:

```osl
INTERFACE IShape
    ' method signatures
END
```

Interface members are implicitly `PUBLIC`. They do not have implementations — only signatures.

## Example

```osl
INTERFACE IShape
    FUNCTION AREA()
    FUNCTION PERIMETER()
END
```

## Implementing an Interface

A class implements an interface by listing it after the class name, separated by a colon:

```osl
CLASS Circle: IShape
    VAR Radius NUMBER

    CONSTRUCTOR(Radius NUMBER)
        ME.Radius = Radius
    END CONSTRUCTOR

    FUNCTION AREA()
        RETURN 3.14159 * ME.Radius * ME.Radius
    END FUNCTION

    FUNCTION PERIMETER()
        RETURN 2 * 3.14159 * ME.Radius
    END FUNCTION
END CLASS
```

## Multiple Interfaces

A class can implement multiple interfaces:

```osl
INTERFACE IShape
    FUNCTION AREA()
END

INTERFACE IColor
    FUNCTION GET_COLOR()
END

CLASS ColoredCircle: IShape, IColor
    VAR Radius NUMBER
    VAR Color STRING

    FUNCTION AREA()
        RETURN 3.14159 * ME.Radius * ME.Radius
    END FUNCTION

    FUNCTION GET_COLOR()
        RETURN ME.Color
    END FUNCTION
END CLASS
```

## Using Interfaces

You can use an interface type to reference any implementing class:

```osl
FUNCTION PRINT_AREA(s IShape)
    PRINT "Area: " + STR(s.AREA())
END FUNCTION

FUNCTION MAIN()
    VAR c = NEW Circle(10)
    PRINT_AREA(c)
END FUNCTION
```

## Related Topics

- [Classes and Objects](/src/docs/oslang/guide/classes-and-objects.md)
- [Inheritance](/src/docs/oslang/guide/inheritance.md)
