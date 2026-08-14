# Hello World

This guide walks you through creating, running, and understanding a minimal OSLANG program.

## The Hello World Program

Create a file named `hello.osl` with the following content:

```osl
FUNCTION MAIN()
    PRINT "Hello, World!"
END FUNCTION
```

## Running the Program

From OSB Shell:

```osl
OSL hello.osl
```

From the command line:

```bash
./Osb.Shell OSL hello.osl
```

## Understanding the Code

| Line | Meaning |
|------|---------|
| `FUNCTION MAIN()` | Defines the program entry point. Every OSLANG program needs exactly one `FUNCTION MAIN()`. |
| `PRINT "Hello, World!"` | Outputs text to the console. |
| `END FUNCTION` | Closes the function definition. |

## What's Next?

- Learn about [variables and types](/src/docs/oslang/guide/variables-and-types.md).
- Explore [control flow](/src/docs/oslang/guide/control-flow.md).
- Write [functions with parameters](/src/docs/oslang/guide/functions.md).

## See Also

- [Getting Started](/src/docs/oslang/getting-started/index.md)
- [Syntax Basics](/src/docs/oslang/guide/syntax-basics.md)
