# Functions

This guide explains how to define and call functions in OSLANG, including parameters, return values, and recursion.

## Defining a Function

Use the `FUNCTION` keyword:

```osl
FUNCTION function_name([parameter1 TYPE, parameter2 TYPE, ...])
    ' statements
    RETURN value
END FUNCTION
```

## Simple Example

```osl
FUNCTION GREET()
    PRINT "Hello!"
END FUNCTION
```

Call it from `MAIN`:

```osl
FUNCTION MAIN()
    GREET()
END FUNCTION
```

## Parameters

Functions can accept parameters with optional type annotations:

```osl
FUNCTION GREET(Name STRING)
    PRINT "Hello, " + Name
END FUNCTION

FUNCTION MAIN()
    GREET("Alice")
END FUNCTION
```

Parameters without type annotations accept any value:

```osl
FUNCTION PRINT_VALUE(x)
    PRINT x
END FUNCTION
```

## Return Values

Use `RETURN` to send a value back to the caller:

```osl
FUNCTION ADD(A NUMBER, B NUMBER)
    RETURN A + B
END FUNCTION

FUNCTION MAIN()
    VAR sum = ADD(3, 4)
    PRINT sum  ' 7
END FUNCTION
```

A function without a `RETURN` statement returns `NULL`.

## Recursion

Functions can call themselves:

```osl
FUNCTION FACTORIAL(n NUMBER)
    IF n <= 1 THEN
        RETURN 1
    END IF
    RETURN n * FACTORIAL(n - 1)
END FUNCTION

FUNCTION MAIN()
    PRINT FACTORIAL(5)  ' 120
END FUNCTION
```

## Overloading

OSLANG supports function overloading. You can define multiple functions with the same name but different parameter types or counts.

```osl
FUNCTION LOG(x NUMBER)
    RETURN LN(x)
END FUNCTION

FUNCTION LOG(x STRING)
    RETURN "Logging: " + x
END FUNCTION

FUNCTION MAIN()
    PRINT LOG(10)       ' calls NUMBER version
    PRINT LOG("test")   ' calls STRING version
END FUNCTION
```

## Built-in Functions

OSLANG includes several built-in functions. See [Built-in Functions](/src/docs/oslang/reference/built-ins/index.md).

## Related Topics

- [Syntax Basics](/src/docs/oslang/guide/syntax-basics.md)
- [Variables and Types](/src/docs/oslang/guide/variables-and-types.md)
- [Classes and Objects](/src/docs/oslang/guide/classes-and-objects.md)
