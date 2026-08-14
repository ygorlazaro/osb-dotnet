# OVERRIDE

Overrides a virtual method from a parent class.

## Syntax

```osl
OVERRIDE FUNCTION methodName([parameters])
    ' statements
END FUNCTION
```

## Description

`OVERRIDE` replaces a `VIRTUAL` method inherited from a parent class. The overriding method must have the same name and compatible parameters.

## Example

```osl
CLASS Animal
    VIRTUAL FUNCTION SPEAK()
        RETURN "..."
    END FUNCTION
END CLASS

CLASS Dog EXTENDS Animal
    OVERRIDE FUNCTION SPEAK()
        RETURN "Woof!"
    END FUNCTION
END CLASS
```

## Related

- [VIRTUAL](/src/docs/oslang/reference/keywords/virtual.md)
- [BASE](/src/docs/oslang/reference/keywords/base.md)
- [Inheritance](/src/docs/oslang/guide/inheritance.md)
