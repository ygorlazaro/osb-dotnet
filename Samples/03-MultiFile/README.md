# OSLANG 0.61 Samples

Multi-file examples showcasing OSLANG 0.61 features.

## Running

From OSB Shell:

```
OSL Main.osl
```

Or from the project root:

```
dotnet run --project src/Osb.Shell/Osb.Shell.csproj -- OSL Main.osl
```

## Structure

```
03-MultiFile/
├── Main.osl    - Entry point (contains FUNCTION MAIN)
├── Person.osl  - Person class module
├── Math.osl    - Math utilities module
└── README.md   - This file
```

## Features Demonstrated

- **USING** - Cross-module dependencies
- **Forward references** - PERSON is declared after MAIN uses it
- **CLASS** - Object-oriented programming
- **METHODS** - Instance methods with ME
- **CONSTRUCTOR** - Object initialization
- **SWITCH expression** - Returns a value based on a condition
- **IF** - Conditional logic
- **Functions across modules** - Math functions called from Main

## OSLANG 0.61 Features

These samples demonstrate core OSLANG 0.61 concepts. For the full language specification, see `src/Osb.Lang/OSLANG-0.61-SPEC.md`.

Additional examples can be found in `src/Osb.Shell/`:
- `Animal.osl` - Class inheritance with BASE and ME
- `HelloWorld.osl` - Basic program with I18N integration
- `ShellExtensions.osl` - FILE, DIR, DATE, and I18N usage
- `Fibonacci.osl` - Recursive function example
