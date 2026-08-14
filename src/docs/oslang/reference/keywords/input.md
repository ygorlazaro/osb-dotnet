# INPUT

Reads input from the user.

## Syntax

```osl
INPUT variable
```

or

```osl
INPUT "prompt", variable
```

## Description

`INPUT` reads a line of text from the console and stores it in `variable`. If a prompt string is provided, it is displayed before reading input.

## Example

```osl
INPUT "Enter your name: ", name
PRINT "Hello, " + name
```

## Related

- [PRINT](/src/docs/oslang/reference/keywords/print.md)
- [Variables and Types](/src/docs/oslang/guide/variables-and-types.md)
