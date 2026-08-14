# USING

Imports another module.

## Syntax

```osl
USING ModuleName
```

## Description

`USING` imports all top-level functions and classes from `ModuleName.osl` into the current module's namespace. Imported symbols are accessed directly without a module prefix.

Module names are case-insensitive and map to `.osl` files in the same directory as the entry-point file.

## Example

`Math.osl`:

```osl
FUNCTION SQUARE(X NUMBER)
    RETURN X * X
END FUNCTION
```

`Main.osl`:

```osl
USING Math

FUNCTION MAIN()
    PRINT SQUARE(5)
END FUNCTION
```

## Related

- [Modules](/src/docs/oslang/guide/modules.md)
- [FUNCTION](/src/docs/oslang/reference/keywords/function.md)
- [CLASS](/src/docs/oslang/reference/keywords/class.md)
