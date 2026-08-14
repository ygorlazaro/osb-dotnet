# Classes and Objects

This guide explains how to define classes, create objects, access properties, and call methods in OSLANG.

## Defining a Class

Use the `CLASS` keyword:

```osl
CLASS ClassName
    ' properties and methods
END CLASS
```

## Properties

Declare properties with `VAR` inside a class body:

```osl
CLASS Person
    VAR Name STRING
    VAR Age NUMBER
END CLASS
```

## Constructors

Define a constructor with the `CONSTRUCTOR` keyword:

```osl
CLASS Person
    VAR Name STRING

    CONSTRUCTOR(Name STRING)
        ME.Name = Name
    END CONSTRUCTOR
END CLASS
```

If no constructor is defined, a default parameterless constructor is available.

## Creating Objects

Use the `NEW` keyword to create an instance:

```osl
VAR p = NEW Person("Alice")
```

Pass arguments to the constructor:

```osl
VAR p = NEW Person("Alice", 30)
```

## Accessing Members

Use `.` (dot notation) to access properties and methods:

```osl
VAR p = NEW Person("Alice")
PRINT p.Name
p.Age = 30
PRINT p.GREET()
```

### ME Keyword

Inside a method, use `ME` to refer to the current instance:

```osl
CLASS Person
    VAR Name STRING

    FUNCTION SET_NAME(newName STRING)
        ME.Name = newName
    END FUNCTION
END CLASS
```

## Methods

Define methods inside a class body:

```osl
CLASS Person
    VAR Name STRING

    FUNCTION GREET()
        RETURN "Hello, " + ME.Name
    END FUNCTION
END CLASS
```

## Visibility

Control access with visibility modifiers:

- `PUBLIC` — Accessible from anywhere.
- `PRIVATE` — Accessible only from within the same class.
- `PROTECTED` — Accessible from the same class and subclasses.

Default visibility is `PUBLIC`.

```osl
CLASS Person
    PUBLIC VAR Name STRING
    PRIVATE VAR id NUMBER

    PUBLIC FUNCTION GET_NAME()
        RETURN ME.Name
    END FUNCTION
END CLASS
```

## Complete Example

```osl
CLASS Person
    VAR Name STRING
    VAR Age NUMBER

    CONSTRUCTOR(Name STRING, Age NUMBER)
        ME.Name = Name
        ME.Age = Age
    END CONSTRUCTOR

    PUBLIC FUNCTION GREET()
        RETURN "Hello, I am " + ME.Name
    END FUNCTION

    PUBLIC FUNCTION IS_ADULT()
        RETURN ME.age >= 18
    END FUNCTION
END CLASS

FUNCTION MAIN()
    VAR p = NEW Person("Alice", 30)
    PRINT p.GREET()
    PRINT p.IS_ADULT()
END FUNCTION
```

## Related Topics

- [Functions](/src/docs/oslang/guide/functions.md)
- [Interfaces](/src/docs/oslang/guide/interfaces.md)
- [Inheritance](/src/docs/oslang/guide/inheritance.md)
- [Variables and Types](/src/docs/oslang/guide/variables-and-types.md)
