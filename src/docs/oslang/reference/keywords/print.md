# PRINT

Outputs text to the console.

## Syntax

```osl
PRINT expression1, expression2, ...
```

## Description

`PRINT` outputs one or more expressions to the console. Multiple expressions are concatenated with a space between them. Strings are output as-is; numbers are formatted as decimal strings.

## Example

```osl
PRINT "Hello, World!"
PRINT "Score:", 100
PRINT "Sum:", 2 + 3
```

Output:

```
Hello, World!
Score: 100
Sum: 5
```

## Related

- [INPUT](/src/docs/oslang/reference/keywords/input.md)
- [Variables and Types](/src/docs/oslang/guide/variables-and-types.md)
