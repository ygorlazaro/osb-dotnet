# Getting Started with OSLANG

This guide will help you install OSLANG, set up your development environment, and run your first OSLANG program.

## What is OSLANG?

OSLANG is a BASIC-inspired, interpreted programming language. It is designed to be:

- **Simple** — Minimal syntax, easy to read and write.
- **Case-insensitive** — Keywords, variable names, and function names are not case-sensitive.
- **Order-independent** — Declarations can appear in any order; forward references are allowed.
- **Multi-file** — Split programs across files with `USING`.
- **Object-oriented** — Classes, interfaces, inheritance, and virtual/override methods.

## Installing OSLANG

OSLANG ships as part of the [OSB](https://github.com/ygorlazaro/osb-dotnet) project. To run OSLANG programs, you need the OSB Shell.

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Building OSB

Clone the repository and build:

```bash
git clone https://github.com/ygorlazaro/osb-dotnet.git
cd osb-dotnet
dotnet build
```

The OSB Shell executable is produced at `src/Osb.Shell/bin/Debug/net10.0/Osb.Shell`.

### Running an OSLANG File

Use the `OSL` command inside OSB Shell, or run the shell directly with a script argument:

```bash
./Osb.Shell OSL path/to/your/program.osl
```

## Your First Program

Create a file named `hello.osl`:

```osl
FUNCTION MAIN()
    PRINT "Hello, OSLANG!"
END FUNCTION
```

Run it:

```bash
./Osb.Shell OSL hello.osl
```

You should see:

```
Hello, OSLANG!
```

## Next Steps

- Learn the [syntax basics](/src/docs/oslang/guide/syntax-basics.md).
- Explore [variables and types](/src/docs/oslang/guide/variables-and-types.md).
- Write your first [function](/src/docs/oslang/guide/functions.md).
- Organize code with [modules](/src/docs/oslang/guide/modules.md).

## Need Help?

- Full specification: [OSLANG 0.4 SPEC](/src/Osb.Lang/OSLANG-0.4-SPEC.md)
- Report issues: [GitHub Issues](https://github.com/ygorlazaro/osb-dotnet/issues)
