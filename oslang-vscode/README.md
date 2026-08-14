# OSLANG 0.3 - VS Code Extension

Syntax highlighting and language support for **OSLANG 0.3**, the scripting language of **OSB 3.0 Lince**.

## Features

- Syntax highlighting for `.osl` files
- OSLANG 0.3 keywords: `CLASS`, `INTERFACE`, `CONSTRUCTOR`, `ME`, `NEW`, `BASE`, `PUBLIC`, `PRIVATE`, `PROTECTED`, `VIRTUAL`, `OVERRIDE`, `SWITCH`, `CASE`, `DEFAULT`, `USING`, `EVENT`, `ON`, `RAISE`
- Standard library functions: `STR`, `NUMBER`, `BOOL`, `SQRT`, `ABS`, `POW`, `FLOOR`, `CEIL`, `COUNT`, `TYPEOF`
- Language constructs: `FUNCTION`, `IF`, `ELIF`, `ELSE`, `FOR`, `WHILE`, `DO`, `TRY`, `CATCH`, `PRINT`, `INPUT`, `SWITCH`
- Types: `NUMBER`, `STRING`, `BOOLEAN`, `ARRAY`, `NULL`, `OBJECT`
- Comments: `'` and `REM`
- Bracket matching and auto-closing

## Installation

1. Copy the `oslang-vscode` folder to your VS Code extensions directory:
   - Linux/macOS: `~/.vscode/extensions/oslang-vscode/`
   - Windows: `%USERPROFILE%\.vscode\extensions\oslang-vscode\`

2. Or install from VSIX:
   ```bash
   code --install-extension oslang-vscode/oslang-0.3.vsix
   ```

## Manual Testing

1. Open VS Code
2. Create or open a file with `.osl` extension
3. The syntax highlighting should activate automatically

Example OSLANG 0.3 code:

```osl
USING Person

FUNCTION MAIN()

    P = NEW PERSON("Ygor")

    PRINT P.GREET()

END FUNCTION
```

## Samples

Multi-file OSLANG 0.3 examples are available in the `Samples/03-MultiFile/` directory of the OSB repository:

- `Main.osl` - Entry point demonstrating USING and SWITCH expression
- `Person.osl` - Class with constructor and methods
- `Math.osl` - Utility functions across modules

## Language Reference

For the complete OSLANG 0.3 specification, see:
- `src/Osb.Lang/OSLANG-0.3-SPEC.md` in the OSB repository
- Or use `HELP OSL` in OSB Shell
- Or `OSL /?` to open the spec in KISS

## Repository

https://github.com/ygorlazaro/osb-dotnet
