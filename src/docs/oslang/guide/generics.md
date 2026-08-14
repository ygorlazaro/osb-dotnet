# Generics

This guide explains how to write generic classes and functions in OSLANG.

## Generic Classes

Define a generic class by specifying type parameters in angle brackets after the class name:

```osl
CLASS Box<T>
    VAR Value T

    CONSTRUCTOR(Value T)
        ME.Value = Value
    END CONSTRUCTOR

    FUNCTION GET()
        RETURN ME.Value
    END FUNCTION
END CLASS
```

## Using Generic Classes

Instantiate a generic class by providing a concrete type argument:

```osl
VAR intBox = NEW Box<NUMBER>(42)
VAR strBox = NEW Box<STRING>("Hello")

PRINT intBox.GET()  ' 42
PRINT strBox.GET()  ' "Hello"
```

## Generic Functions

You can also write generic functions:

```osl
FUNCTION PRINT_VALUE<T>(Value T)
    PRINT STR(Value)
END FUNCTION

FUNCTION MAIN()
    PRINT_VALUE<NUMBER>(42)
    PRINT_VALUE<STRING>("Hello")
END FUNCTION
```

## Constraints

In OSLANG 0.3, generic type parameters have no explicit constraints. Any type can be substituted.

## Related Topics

- [Classes and Objects](/src/docs/oslang/guide/classes-and-objects.md)
- [Functions](/src/docs/oslang/guide/functions.md)
