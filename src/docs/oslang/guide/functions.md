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

## MAIN Entry Point with Arguments

OSLANG 0.41 allows `FUNCTION MAIN` to receive command-line arguments:

```osl
FUNCTION MAIN(Args)
    PRINT "Args count: " + COUNT(Args)
    PRINT "First arg: " + Args[0]
END FUNCTION
```

`Args` is an ARRAY of STRING containing the arguments passed to the script. This is used by extensible OSB commands implemented in OSLANG.

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

## Function References as Callbacks

OSLANG 0.4 supports passing functions as arguments to array methods:

```osl
FUNCTION DOUBLE(x)
    RETURN x * 2
END FUNCTION

FUNCTION MAIN()
    Doubled = [1, 2, 3].MAP(DOUBLE)      ' [2, 4, 6]
    Even = [1, 2, 3, 4].FILTER(IS_EVEN)  ' [2, 4]
    Sum = [1, 2, 3].REDUCE(ADD, 0)        ' 6
    PRINT Doubled
END FUNCTION
```

Supported callback methods: `MAP`, `FILTER`, `ANY`, `SOME`, `ALL`, `REDUCE`.

## Related Topics

- [Syntax Basics](/src/docs/oslang/guide/syntax-basics.md)
- [Variables and Types](/src/docs/oslang/guide/variables-and-types.md)
- [Classes and Objects](/src/docs/oslang/guide/classes-and-objects.md)
