# INTERFACE

Defines an interface.

## Syntax

```osl
INTERFACE IName
    ' method signatures
END [INTERFACE]
```

## Description

An interface declares a contract of method signatures without implementations. Classes implement interfaces by listing them after the class name.

Interface members are implicitly `PUBLIC` and have no bodies.

## Example

```osl
INTERFACE IShape
    FUNCTION AREA()
    FUNCTION PERIMETER()
END

CLASS Circle: IShape
    VAR Radius NUMBER

    FUNCTION AREA()
        RETURN 3.14159 * ME.Radius * ME.Radius
    END FUNCTION

    FUNCTION PERIMETER()
        RETURN 2 * 3.14159 * ME.Radius
    END FUNCTION
END CLASS
```

## Related

- [CLASS](/src/docs/oslang/reference/keywords/class.md)
- [Interfaces](/src/docs/oslang/guide/interfaces.md)
