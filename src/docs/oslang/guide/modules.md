# Modules

This guide explains how to split OSLANG programs across multiple files using `USING`.

## What is a Module?

A module is a single `.osl` file. Modules let you organize code into reusable files.

## Creating a Module

Create a file named `Math.osl`:

```osl
FUNCTION SQUARE(X NUMBER)
    RETURN X * X
END FUNCTION

FUNCTION ADD(A NUMBER, B NUMBER)
    RETURN A + B
END FUNCTION
```

Create another file named `Main.osl`:

```osl
USING Math

FUNCTION MAIN()
    PRINT SQUARE(5)
    PRINT ADD(2, 3)
END FUNCTION
```

## How USING Works

- `USING ModuleName` imports all top-level functions and classes from `ModuleName.osl` into the current module's namespace.
- Imported symbols are accessed directly, without a module prefix:

    ```osl
    USING Math
    PRINT SQUARE(5)  ' not Math.SQUARE(5)
    ```

- The module resolver looks for `.osl` files in the same directory as the entry-point file (and its subdirectories, depending on configuration).

## File Naming

Module names map to file names:

| USING declaration | File name |
|-------------------|-----------|
| `USING Math` | `Math.osl` |
| `USING Person` | `Person.osl` |

File name resolution is case-insensitive on case-insensitive filesystems.

## Circular USING

Circular dependencies are detected and reported as errors:

```osl
' File A.osl
USING B

' File B.osl
USING A
```

This produces a diagnostic: `Circular USING dependency detected`.

## Multi-file Project Structure

```
project/
  Main.osl
  Math.osl
  Person.osl
```

`Main.osl`:

```osl
USING Math
USING Person

FUNCTION MAIN()
    PRINT SQUARE(4)
    VAR p = NEW PERSON("Alice")
    PRINT p.GREET()
END FUNCTION
```

## Related Topics

- [Getting Started](/src/docs/oslang/getting-started/index.md)
- [Functions](/src/docs/oslang/guide/functions.md)
- [Classes and Objects](/src/docs/oslang/guide/classes-and-objects.md)
