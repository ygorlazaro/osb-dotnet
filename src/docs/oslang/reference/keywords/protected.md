# PROTECTED

Protected visibility modifier.

## Syntax

```osl
PROTECTED VAR propertyName TYPE
PROTECTED FUNCTION methodName()
```

## Description

`PROTECTED` restricts access to the declaring class and its subclasses. Protected members are not visible from external code.

## Example

```osl
CLASS Animal
    PROTECTED VAR Name STRING

    PUBLIC FUNCTION SPEAK()
        RETURN ME.MAKE_SOUND()
    END FUNCTION

    PROTECTED FUNCTION MAKE_SOUND()
        RETURN "..."
    END FUNCTION
END CLASS

CLASS Dog EXTENDS Animal
    OVERRIDE FUNCTION MAKE_SOUND()
        RETURN "Woof!"
    END FUNCTION
END CLASS
```

## Related

- [PUBLIC](/src/docs/oslang/reference/keywords/public.md)
- [PRIVATE](/src/docs/oslang/reference/keywords/private.md)
- [Inheritance](/src/docs/oslang/guide/inheritance.md)
