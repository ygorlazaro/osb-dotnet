# Syntax Basics

This guide covers the foundational syntax rules of OSLANG, including case sensitivity, statement structure, comments, whitespace, and the overall program structure.

## Case Insensitivity

OSLANG is **case-insensitive**. Keywords, variable names, function names, and class names can be written in any case.

```osl
function main()
    print "Hello"
end function
```

is equivalent to:

```osl
FUNCTION MAIN()
    PRINT "Hello"
END FUNCTION
```

## Program Structure

An OSLANG program consists of:

1. Optional `USING` declarations (to import other modules).
2. Optional global declarations (`GLOBAL`).
3. Functions.
4. Classes and interfaces.
5. Events.
6. The entry point: `FUNCTION MAIN()` (or `FUNCTION MAIN(Args)` to receive command-line arguments as an ARRAY).

Example:

```osl
USING Math

GLOBAL PI = 3.14159

FUNCTION SQUARE(X)
    RETURN X * X
END FUNCTION

FUNCTION MAIN()
    PRINT SQUARE(5)
END FUNCTION
```

## Comments

OSLANG supports two comment styles:

- **REM** — Single-line comment. Everything after `REM` on the same line is ignored.

    ```osl
    REM This is a comment
    PRINT "Hello" REM inline comment
    ```

- **Single quote** — Single-line comment. Everything after `'` on the same line is ignored.

    ```osl
    ' This is also a comment
    PRINT "Hello" ' inline comment
    ```

## Statements and Newlines

Each statement is typically written on its own line. Newlines separate statements. Multiple statements on the same line are not supported unless inside a block where the parser can clearly distinguish them.

```osl
PRINT "Line 1"
PRINT "Line 2"
```

### SHOW Statement

OSLANG 0.6 adds `SHOW`, which outputs text without a trailing newline:

```osl
SHOW "Enter your name: "
INPUT Name
PRINT "Hello, " + Name
```

Multiple `SHOW` statements concatenate their output:

```osl
SHOW "Hello "
SHOW "World"
PRINT ""   ' outputs "Hello World" followed by a newline
```

## Blocks

Blocks are groups of statements enclosed by a start keyword and an `END` keyword.

```osl
IF condition THEN
    statement1
    statement2
END
```

Common block patterns:

```osl
IF condition THEN
    ...
END

FOR i = 1 TO 10
    ...
END

WHILE condition
    ...
END

TRY
    ...
CATCH err
    ...
END
```

## Identifiers

Identifiers are used for variable names, function names, class names, and property names.

- Must start with a letter (`A`-`Z`, `a`-`z`) or underscore (`_`).
- Can contain letters, digits (`0`-`9`), and underscores.
- Are case-insensitive.

Examples:

```osl
VAR x
VAR _private
VAR Age2
```

## Keywords

OSLANG reserves a set of keywords. You cannot use them as identifiers.

See the [Keywords Reference](/src/docs/oslang/reference/keywords/index.md) for the complete list.

## Related Topics

- [Variables and Types](/src/docs/oslang/guide/variables-and-types.md)
- [Control Flow](/src/docs/oslang/guide/control-flow.md)
- [Functions](/src/docs/oslang/guide/functions.md)
