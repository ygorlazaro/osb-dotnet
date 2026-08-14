# OSLANG 0.2 - VS Code Extension

Syntax highlighting and language support for **OSLANG 0.2**, the scripting language of **OSB 3.0 Lince**.

## Features

- Syntax highlighting for `.osl` files
- OSLANG 0.2 keywords: `CLASS`, `INTERFACE`, `CONSTRUCTOR`, `ME`, `NEW`, `BASE`, `PUBLIC`, `PRIVATE`, `PROTECTED`, etc.
- Standard library functions: `STR`, `NUMBER`, `BOOL`, `SQRT`, `ABS`, `POW`, `FLOOR`, `CEIL`, `COUNT`, `TYPEOF`
- Language constructs: `FUNCTION`, `IF`, `ELIF`, `ELSE`, `FOR`, `WHILE`, `DO`, `TRY`, `CATCH`, `PRINT`, `INPUT`
- Types: `NUMBER`, `STRING`, `BOOLEAN`, `ARRAY`, `NULL`
- Comments: `'` and `REM`
- Bracket matching and auto-closing

## Installation

1. Copy the `oslang-vscode` folder to your VS Code extensions directory:
   - Linux/macOS: `~/.vscode/extensions/oslang-vscode/`
   - Windows: `%USERPROFILE%\.vscode\extensions\oslang-vscode\`

2. Or install from VSIX:
   ```bash
   code --install-extension oslang-vscode/oslang-0.2.vsix
   ```

## Manual Testing

1. Open VS Code
2. Create or open a file with `.osl` extension
3. The syntax highlighting should activate automatically

Example OSLANG 0.2 code:

```osl
INTERFACE IShape
    GET_AREA()
    GET_NAME()
END

CLASS Shape: IShape
    VAR Name String
    
    CONSTRUCTOR(Name String)
        ME.Name = Name
    END
    
    PUBLIC GET_NAME()
        RETURN ME.Name
    END
    
    PUBLIC GET_AREA()
        RETURN 0
    END
END

CLASS Circle: Shape
    VAR Radius Number
    
    CONSTRUCTOR(Radius Number)
        BASE("Unnamed Circle")
        ME.Radius = Radius
    END
    
    PUBLIC GET_AREA()
        RETURN 3.14159 * ME.Radius * ME.Radius
    END
END

FUNCTION MAIN()
    C = NEW Circle(10)
    PRINT C.GET_NAME()
    PRINT C.GET_AREA()
END FUNCTION
```

## Language Reference

For the complete OSLANG 0.2 specification, see:
- `src/Osb.Lang/OSLANG-0.2-SPEC.md` in the OSB repository
- Or use `HELP OSL` in OSB Shell
- Or `OSL /?` to open the spec in KISS

## Repository

https://github.com/ygorlazaro/osb-dotnet
