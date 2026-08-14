# OSLANG 0.3 Samples

Multi-file examples showcasing OSLANG 0.3 features.

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
