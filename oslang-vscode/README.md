# OSLANG 0.62 - VS Code Extension

Syntax highlighting, intellisense, and language support for **OSLANG 0.62**, the scripting language of **OSB 3.0 Lince**.

## Features

- **Syntax Highlighting** for `.osl` and `.oslang` files with:
  - Keywords, types, numbers, strings, comments
  - Operators (arithmetic, comparison, logical, arrow `=>`)
  - Method calls (MATH, STRING, ARRAY, I18N, FILE, DIR, DATE builtins)
  - Punctuation `()`, `[]`, `{}`, `,`, `.`, `:`
  - String interpolation `${...}` with nested syntax highlighting
  - Enum sets with `|`
- **IntelliSense** — context-aware autocompletion:
  - All OSLANG keywords and built-in functions
  - Classes, interfaces, functions, methods, variables, and enums from the current document
  - Context-aware method suggestions (`MATH.`, `STRING.`, `ARRAY.`, `I18N.`, `FILE.`, `DIR.`, `DATE.`)
- **Hover** — inline documentation for 60+ keywords and built-in functions
- **Signature Help** — parameter hints for functions with multiple overloads
- **Document Symbols** — outline view showing classes, interfaces, functions, enums, methods, and properties
- **Code Folding** — fold blocks for `CLASS`, `INTERFACE`, `FUNCTION`, `CONSTRUCTOR`, `TRY`, `IF`, `FOR`, `WHILE`, `DO`, `SWITCH`, `ENUM`
- **Snippets** — 40+ code snippets including:
  - `enum` — ENUM declaration
  - `enumset` — EnumSet with `|`
  - `arrow` / `arrowblock` — Arrow functions
  - `foreach` / `foreachmethod` — For-each loops
  - `dowhile` — Do-while loop
  - `elif` — Else-if block
  - `interp` — String interpolation
  - `math` — MATH function call
  - `i18nget` / `i18nlang` — I18N helpers
  - `fileread` / `dirlist` / `datenow` — FILE/DIR/DATE helpers
  - `tryraise` — Try-catch with error printing
  - `import` — USING statement
  - `global` — GLOBAL variable
  - `arr` / `arrforeach` — Array literal and foreach
- **Bracket Matching** and **Auto-Closing** for `()`, `[]`, `{}`, `""`, `''`
- **Indentation Rules** — automatic indentation for OSLANG blocks

## OSLANG 0.62 Keywords

- **Data/Network Namespaces:** `JSON`, `CSV`, `XML`, `CNF`, `OSB`, `NET`
- **Control Flow:** `AND`, `BREAK`, `CONTINUE`, `DO`, `ELIF`, `ELSE`, `END`, `FOR`, `IF`, `NOT`, `OR`, `RETURN`, `STEP`, `THEN`, `TO`, `WHILE`, `TRY`, `CATCH`, `SWITCH`, `CASE`, `DEFAULT`, `ON`, `RAISE`
- **Declarations:** `CONSTRUCTOR`, `FUNCTION`, `CLASS`, `INTERFACE`, `VAR`, `GLOBAL`, `USING`, `EVENT`, `ENUM`
- **Other:** `BASE`, `ME`, `NEW`, `PRINT`, `INPUT`, `CLEAR`, `CLS`, `VIRTUAL`, `OVERRIDE`, `KISS`, `TYPE`, `MATH`, `FILE`, `DIR`, `DATE`, `TIME`, `SHOW`, `MOD`, `TYPEOF`, `SQRT`, `POW`, `FLOOR`, `CEIL`, `COUNT`, `STR`, `BOOL`, `NUMBER`, `STRING`, `BOOLEAN`, `ARRAY`, `OBJECT`, `NULL`, `TRUE`, `FALSE`, `OSL`, `OSB`
- **Modifiers:** `PUBLIC`, `PRIVATE`, `PROTECTED`, `STATIC`

## Built-in Functions

- **MATH:** `SQRT`, `POW`, `FLOOR`, `CEIL`, `ABS`, `SIN`, `COS`, `TAN`, `PI`, `RANDOM`, `MAX`, `MIN`
- **STRING:** `LENGTH`, `TOUPPER`, `TOLOWER`, `TRIM`, `SUBSTR`, `CONTAINS`, `REVERSE`, `NORMALIZE`, `REPEAT`, `PADSTART`, `PADEND`
- **ARRAY:** `COUNT`, `FIRST`, `LAST`, `SORT`, `JOIN`, `INDEXOF`, `REMOVE`, `FLAT`, `PUSH`, `POP`, `FINDINDEX`
- **I18N:** `GET`, `HAS`, `KEYS`, `LANGUAGE`, `SETLANGUAGE`, `LANGUAGES`, `LOAD`, `LOADLANGUAGE`, `RELOAD`, `UNLOAD`, `DEFAULT`, `SETDEFAULT`, `SETFALLBACK`
- **FILE:** `EXISTS`, `READ`, `WRITE`, `APPEND`, `DELETE`, `LIST`, `FILES`, `DIRS`, `CREATE`
- **DIR:** `EXISTS`, `LIST`, `FILES`, `DIRS`, `CREATE`, `DELETE`, `CURRENT`
- **DATE:** `NOW`, `YEAR`, `MONTH`, `DAY`, `HOUR`, `MINUTE`, `SECOND`, `WEEKDAY`, `FORMAT`
- **OSL.JSON:** `PARSE`, `STRINGIFY`, `PRETTY`, `READ`, `WRITE`
- **OSL.CSV:** `PARSE`, `STRINGIFY`, `READ`, `WRITE`
- **OSL.XML:** `PARSE`, `STRINGIFY`, `READ`, `WRITE`, `NAME`, `VALUE`, `ATTRIBUTES`, `CHILDREN`, `CHILD`, `HAS`
- **OSL.CNF:** `READ`, `WRITE`, `GET`, `SET`, `HAS`, `DELETE`, `KEYS`, `SAVE`
- **OSB.NET:** `PING`, `DOWN`
- **Conversion:** `STR`, `NUMBER`, `BOOL`, `TYPEOF`, `TRUNC`

## Installation

1. Copy the `oslang-vscode` folder to your VS Code extensions directory:
   - Linux/macOS: `~/.vscode/extensions/oslang-vscode/`
   - Windows: `%USERPROFILE%\.vscode\extensions\oslang-vscode\`

2. Or install from VSIX:
   ```bash
   code --install-extension oslang-vscode/oslang-0.61.vsix
   ```

## Manual Testing

1. Open VS Code
2. Create or open a file with `.osl` or `.oslang` extension
3. The syntax highlighting should activate automatically

Example OSLANG 0.61 code:

```osl
USING OSL.I18N

ENUM Color
    RED
    GREEN
    BLUE
END

FUNCTION MAIN()
    Favorite = Color.BLUE

    SWITCH Favorite
        CASE Color.RED
            PRINT "Red"
        CASE Color.BLUE
            PRINT "Blue"
        DEFAULT
            PRINT "Other"
    END

    Message = "Favorite color is ${Favorite.NAME()}"
    PRINT Message
END FUNCTION
```

## Samples

Multi-file OSLANG 0.61 examples are available in the `Samples/03-MultiFile/` directory of the OSB repository:

- `Main.osl` - Entry point demonstrating USING and SWITCH expression
- `Person.osl` - Class with constructor and methods
- `Math.osl` - Utility functions across modules

Additional OSLANG examples in `src/Osb.Shell/`:

- `Animal.osl` - Class inheritance with BASE and ME
- `HelloWorld.osl` - Basic program with I18N
- `ShellExtensions.osl` - FILE, DIR, DATE, I18N usage
- `Fibonacci.osl` - Recursive function example

## Language Reference

For the complete OSLANG 0.61 specification, see:
- `src/Osb.Lang/OSLANG-0.61-SPEC.md` in the OSB repository
- Or use `HELP OSL` in OSB Shell
- Or `OSL /?` to open the spec in KISS

## Repository

https://github.com/ygorlazaro/osb-dotnet
