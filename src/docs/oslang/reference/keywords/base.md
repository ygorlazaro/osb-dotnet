# BASE

Calls the base class implementation of a method.

## Syntax

```osl
BASE.methodName([arguments])
```

## Description

Inside an `OVERRIDE` method, `BASE` calls the corresponding method in the parent class. This is useful when you want to extend behavior rather than completely replace it.

## Example

```osl
CLASS Animal
    VIRTUAL FUNCTION SPEAK()
        RETURN "..."
    END FUNCTION
END CLASS

CLASS Dog EXTENDS Animal
    OVERRIDE FUNCTION SPEAK()
        PRINT "Woof!"
        RETURN BASE.SPEAK()  ' Calls Animal.SPEAK()
    END FUNCTION
END CLASS
```

## Related

- [VIRTUAL](/src/docs/oslang/reference/keywords/virtual.md)
- [OVERRIDE](/src/docs/oslang/reference/keywords/override.md)
- [Inheritance](/src/docs/oslang/guide/inheritance.md)
