# PUBLIC

Public visibility modifier.

## Syntax

```osl
PUBLIC VAR propertyName TYPE
PUBLIC FUNCTION methodName()
```

## Description

`PUBLIC` makes a member accessible from any code that can see the class. It is the default visibility for class members if no modifier is specified.

## Example

```osl
CLASS Person
    PUBLIC VAR Name STRING

    PUBLIC FUNCTION GREET()
        RETURN "Hello, " + ME.Name
    END FUNCTION
END CLASS

FUNCTION MAIN()
    VAR p = NEW Person("Alice")
    PRINT p.GREET()
END FUNCTION
```

## Related

- [PRIVATE](/src/docs/oslang/reference/keywords/private.md)
- [PROTECTED](/src/docs/oslang/reference/keywords/protected.md)
- [Classes and Objects](/src/docs/oslang/guide/classes-and-objects.md)
