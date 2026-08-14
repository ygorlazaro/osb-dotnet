# PRIVATE

Private visibility modifier.

## Syntax

```osl
PRIVATE VAR propertyName TYPE
PRIVATE FUNCTION methodName()
```

## Description

`PRIVATE` restricts access to the declaring class only. Private members are not visible from subclasses or external code.

## Example

```osl
CLASS Person
    PUBLIC VAR Name STRING
    PRIVATE VAR id NUMBER

    PUBLIC FUNCTION GET_ID()
        RETURN ME.id
    END FUNCTION
END CLASS
```

## Related

- [PUBLIC](/src/docs/oslang/reference/keywords/public.md)
- [PROTECTED](/src/docs/oslang/reference/keywords/protected.md)
- [Classes and Objects](/src/docs/oslang/guide/classes-and-objects.md)
