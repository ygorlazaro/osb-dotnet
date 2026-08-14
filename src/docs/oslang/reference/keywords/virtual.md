# VIRTUAL

Declares a method that can be overridden by subclasses.

## Syntax

```osl
VIRTUAL FUNCTION methodName([parameters])
    ' statements
END FUNCTION
```

## Description

`VIRTUAL` marks a method as overridable. Subclasses can use `OVERRIDE` to provide their own implementation.

## Example

```osl
CLASS Shape
    VIRTUAL FUNCTION AREA()
        RETURN 0
    END FUNCTION
END CLASS

CLASS Circle EXTENDS Shape
    VAR Radius NUMBER

    OVERRIDE FUNCTION AREA()
        RETURN 3.14159 * ME.Radius * ME.Radius
    END FUNCTION
END CLASS
```

## Related

- [OVERRIDE](/src/docs/oslang/reference/keywords/override.md)
- [BASE](/src/docs/oslang/reference/keywords/base.md)
- [Inheritance](/src/docs/oslang/guide/inheritance.md)
