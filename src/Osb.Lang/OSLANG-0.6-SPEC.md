# OSLANG 0.6 Specification

**Language:** OSLANG  
**Version:** 0.6  
**Standard Library namespace:** `OSL`  
**First standard library:** `OSL.I18N`  
**Resource extension:** `.I18N`

## 1. Overview

OSLANG 0.6 introduces the **Standard Library** concept. Standard libraries live under the reserved top-level namespace `OSL` and are explicitly imported with `USING`.

The first standard library is internationalization:

```osl
USING OSL.I18N
```

The goal is to provide a small, host-friendly localization API capable of loading `.I18N` resource files, selecting languages, resolving translated strings, applying positional parameters, and falling back to another language when a translation is unavailable.

OSLANG 0.6 retains the functionality of OSLANG 0.51 unless explicitly superseded by this specification.

---

# 2. Standard Library Architecture

The standard library is exposed through the reserved namespace:

```text
OSL
```

Modules are addressed by fully-qualified names:

```text
OSL.I18N
```

Future standard libraries may include modules such as `OSL.JSON`, `OSL.TEXT`, or `OSL.NET`; those names are illustrative only and are not part of 0.6.

The `OSL` namespace is reserved and user code MUST NOT declare a conflicting top-level namespace named `OSL`.

Standard libraries are separate from OSB-specific extensions. An API that exists only because of the OSB host should not automatically become part of the OSLANG Standard Library.

---

# 3. USING

`USING` imports a standard-library module for the current source file.

Syntax:

```osl
USING OSL.I18N
```

A `USING` declaration MUST appear before executable declarations/statements in the file.

Multiple modules are allowed:

```osl
USING OSL.I18N
USING OSL.TEXT
```

Repeated imports of the same module have no additional effect.

An unknown module is a load/compile-time error.

The imported short name may be used normally:

```osl
USING OSL.I18N

PRINT I18N.GET("boot.welcome")
```

The fully qualified module name may also be used where qualified access is supported:

```osl
PRINT OSL.I18N.GET("boot.welcome")
```

---

# 4. Resource Files

The standard extension for OSLANG internationalization resources is:

```text
.I18N
```

Examples:

```text
EN-US.I18N
PT-BR.I18N
ES-ES.I18N
```

The extension is case-insensitive.

Resource files are UTF-8 by default.

The resource format is intentionally simple and compatible with the existing OSB resource files.

---

# 5. I18N Resource Format

The canonical syntax is:

```text
key=value
```

For example:

```text
boot.starting=Starting OSB boot process
boot.reading_config=Reading {0}
boot.user_created=Initial user '{0}' created successfully.
```

The supplied OSB English resource follows this format, with grouped keys such as `boot.*`, `auth.*`, `config.*`, `commands.*`, `fs.*`, `apps.*`, `osl.*`, and `calendar.*`. fileciteturn0file0L10-L22 fileciteturn0file0L48-L66 fileciteturn0file0L102-L129

## 5.1 Comments

Lines beginning with `#` are comments.

Blank lines are ignored.

Example:

```text
# OSB Shell I18N - English (US)

boot.starting=Starting OSB boot process
```

## 5.2 Key/value separator

Only the first `=` separates the key from the value.

Therefore:

```text
example=A=B=C
```

means:

```text
key   = example
value = A=B=C
```

## 5.3 Empty values

Empty values are valid:

```text
message=
```

`I18N.GET("message")` returns an empty string.

An empty value is different from a missing key.

## 5.4 Duplicate keys

A resource MUST NOT contain duplicate keys. The implementation SHOULD reject duplicate keys while loading the resource rather than silently selecting one of them.

---

# 6. Resource Keys

Resource keys are strings and SHOULD follow the convention:

```text
lowercase.words.separated.by.dots
```

Examples from the OSB resource include:

```text
boot.starting
boot.reading_config
user.registered_users
fs.file_not_found
calendar.month_january
```

The `.` character is part of the resource key; it does not create an OSLANG namespace.

Keys are compared case-insensitively unless a future version explicitly introduces case-sensitive resource keys.

---

# 7. Language Identifiers

OSLANG uses the conventional language/region form:

```text
LANGUAGE-REGION
```

Examples:

```text
EN-US
PT-BR
ES-ES
```

Language identifiers are case-insensitive. Implementations SHOULD expose them canonically in uppercase.

The runtime does not require a fixed list of supported languages; applications may use additional locale identifiers.

---

# 8. Resource Naming Convention

The recommended resource filename is:

```text
<LANGUAGE>.I18N
```

Examples:

```text
EN-US.I18N
PT-BR.I18N
```

When a resource is loaded with `LOAD`, the runtime SHOULD infer its language from this filename when possible.

---

# 9. I18N API

After:

```osl
USING OSL.I18N
```

the following API is available.

## Required operations

```text
I18N.GET(Key, ...)
I18N.HAS(Key)
I18N.KEYS()

I18N.LANGUAGE()
I18N.SETLANGUAGE(Language)
I18N.LANGUAGES()

I18N.LOAD(Path)
I18N.LOADLANGUAGE(Language, Path)
I18N.RELOAD(Language?)
I18N.UNLOAD(Language)

I18N.DEFAULT()
I18N.SETDEFAULT(Language)

I18N.SETFALLBACK(Language)
```

---

# 10. I18N.GET

`GET` resolves a resource key using the active language and returns a `STRING`.

```osl
Message = I18N.GET("boot.starting")
PRINT Message
```

For:

```text
boot.starting=Starting OSB boot process
```

the result is:

```text
Starting OSB boot process
```

The resource supplied with OSB demonstrates both plain messages and parameterized messages such as `boot.reading_config=Reading {0}` and `boot.user_created=Initial user '{0}' created successfully.` fileciteturn0file0L14-L36

---

# 11. Parameterized Messages

Resource values support zero-based positional placeholders:

```text
user.registered=User '{0}' registered successfully.
user.files={0} has {1} files.
```

OSLANG:

```osl
PRINT I18N.GET("user.registered", "Ygor")
PRINT I18N.GET("user.files", "Ygor", 12)
```

Result:

```text
User 'Ygor' registered successfully.
Ygor has 12 files.
```

The placeholder syntax is:

```text
{0}
{1}
{2}
...
```

Arguments are converted to their string representation before substitution.

---

# 12. Missing and Extra Parameters

If a placeholder has no corresponding argument, the placeholder remains unchanged.

```text
message=Hello {0}, welcome to {1}.
```

```osl
I18N.GET("message", "Ygor")
```

returns:

```text
Hello Ygor, welcome to {1}.
```

Extra arguments that are not referenced by the resource are ignored.

---

# 13. I18N.HAS

Checks whether a key exists in the current resolved language context.

```osl
IF I18N.HAS("boot.welcome") THEN
    PRINT I18N.GET("boot.welcome")
END
```

Returns `BOOLEAN`.

By default, `HAS` refers to the active language only. A future version may expose an explicit `HASRESOLVED` operation for fallback-aware checks.

---

# 14. I18N.KEYS

Returns the resource keys available in the active language:

```osl
Keys = I18N.KEYS()
```

Result type:

```text
ARRAY of STRING
```

Ordering is implementation-defined.

---

# 15. Language Selection

## 15.1 LANGUAGE

Returns the active language:

```osl
Language = I18N.LANGUAGE()
```

Example:

```text
EN-US
```

## 15.2 SETLANGUAGE

Selects the active language:

```osl
I18N.SETLANGUAGE("PT-BR")
```

The language does not need to be loaded before selection if the host supports automatic resource discovery; otherwise a subsequent lookup will use the configured fallback or report a missing resource.

## 15.3 LANGUAGES

Returns the languages currently available to the I18N provider:

```osl
Languages = I18N.LANGUAGES()
```

Example:

```text
["EN-US", "PT-BR", "ES-ES"]
```

---

# 16. Resource Loading

## 16.1 LOAD

Loads an `.I18N` file:

```osl
I18N.LOAD("I18N/EN-US.I18N")
```

The runtime SHOULD infer `EN-US` from the filename.

## 16.2 LOADLANGUAGE

Explicitly associates a resource file with a language:

```osl
I18N.LOADLANGUAGE("EN-US", "I18N/EN-US.I18N")
```

This form is preferred when the filename does not follow the standard convention.

## 16.3 RELOAD

Reloads a previously loaded language:

```osl
I18N.RELOAD("EN-US")
```

If no language is supplied, the current language is reloaded:

```osl
I18N.RELOAD()
```

## 16.4 UNLOAD

Removes a loaded language:

```osl
I18N.UNLOAD("EN-US")
```

Unloading the active language is permitted. Subsequent lookups resolve through the configured fallback when available.

---

# 17. Default Language

## DEFAULT

Returns the configured default language:

```osl
Language = I18N.DEFAULT()
```

## SETDEFAULT

Sets the default language:

```osl
I18N.SETDEFAULT("EN-US")
```

The host SHOULD configure a sensible default based on its environment.

---

# 18. Fallback Language

I18N supports one explicit fallback language.

```osl
I18N.SETFALLBACK("EN-US")
```

A fallback can be cleared:

```osl
I18N.SETFALLBACK(NULL)
```

When a key is absent from the active language, `GET` attempts the fallback language before returning the key itself.

Example resources:

### EN-US.I18N

```text
welcome=Welcome
exit=Exit
```

### PT-BR.I18N

```text
welcome=Bem-vindo
```

Program:

```osl
I18N.SETLANGUAGE("PT-BR")
I18N.SETFALLBACK("EN-US")

PRINT I18N.GET("welcome")
PRINT I18N.GET("exit")
```

Output:

```text
Bem-vindo
Exit
```

Automatic parent-locale fallback such as `PT-BR -> PT` is not required in 0.6. Only the explicitly configured fallback is guaranteed.

---

# 19. Missing Translation Behavior

If a key cannot be found in the active or fallback language, `GET` returns the key itself:

```osl
Message = I18N.GET("unknown.key")
```

Result:

```text
unknown.key
```

This is intentional: missing translations remain visible without terminating normal application execution.

A strict missing-key mode is reserved for a future version.

---

# 20. Automatic Resource Discovery

A host MAY automatically discover `.I18N` files from configured resource directories.

A conventional layout is:

```text
I18N/
    EN-US.I18N
    PT-BR.I18N
    ES-ES.I18N
```

The exact directory is host-defined.

`OSLANG` itself does not require a fixed filesystem location.

---

# 21. OSB Resource Compatibility

The existing OSB resource is a valid example of the intended format. It contains comments, grouped sections, plain messages, parameterized messages, and values that include punctuation and whitespace. For example, boot prompts, filesystem errors, command help, and calendar labels are all represented as `key=value` entries. fileciteturn0file0L10-L47 fileciteturn0file0L141-L190 fileciteturn0file0L243-L264

This means the standard library should be capable of consuming the resource files already used by OSB without requiring a second resource format.

---

# 22. Resource Parsing Rules

An implementation MUST:

1. read UTF-8 resources;
2. ignore blank lines;
3. ignore comment lines beginning with `#`;
4. split each entry at the first `=`;
5. preserve the remainder of the line as the value;
6. reject empty keys;
7. detect duplicate keys;
8. preserve Unicode characters;
9. support `{N}` positional placeholders.

Leading/trailing whitespace around the key SHOULD be trimmed. Resource values SHOULD preserve intentional leading/trailing whitespace because existing OSB resources contain prompt strings whose spacing is meaningful. For example, `boot.fore_color_prompt`, `boot.back_color_prompt`, and `misc.fore_color_prompt` contain intentional trailing spaces. fileciteturn0file0L32-L34 fileciteturn0file0L229-L231

---

# 23. Error Handling

I18N integrates with OSLANG `TRY/CATCH`.

```osl
TRY

    I18N.LOAD("I18N/PT-BR.I18N")

CATCH

    PRINT ERR

END
```

Errors include:

- unreadable resource files;
- invalid resource syntax;
- empty resource keys;
- duplicate keys;
- invalid API arguments;
- filesystem failures while loading resources.

Missing translation keys are not errors under the default behavior described in section 19.

---

# 24. Name Resolution and User Classes

`OSL` is reserved, but a user may technically declare a short name that matches an imported module:

```osl
USING OSL.I18N

CLASS I18N
    ...
END
```

This creates a potential ambiguity and SHOULD be discouraged.

The fully-qualified standard-library identity remains:

```text
OSL.I18N
```

The implementation MUST guarantee that a user-defined class cannot replace or redefine the reserved `OSL` namespace.

---

# 25. Standard Library vs OSB Extensions

OSLANG 0.6 establishes a distinction:

### OSLANG Core

The language grammar, runtime types, control flow, functions, classes, operators, and other intrinsic features.

### OSL Standard Library

Portable functionality under:

```text
OSL.*
```

The first member is:

```text
OSL.I18N
```

### OSB Extensions

Functionality supplied specifically by the OSB host, such as shell commands or OSB-specific services.

OSB extensions SHOULD NOT be treated as part of the portable OSLANG Standard Library unless explicitly promoted in a future language specification.

---

# 26. Recommended Application Layout

A typical application may use:

```text
MyApp/
    main.osl
    I18N/
        EN-US.I18N
        PT-BR.I18N
```

`main.osl`:

```osl
USING OSL.I18N

FUNCTION MAIN()

    I18N.LOADLANGUAGE("EN-US", "I18N/EN-US.I18N")
    I18N.LOADLANGUAGE("PT-BR", "I18N/PT-BR.I18N")

    I18N.SETDEFAULT("EN-US")
    I18N.SETFALLBACK("EN-US")
    I18N.SETLANGUAGE("PT-BR")

    PRINT I18N.GET("app.welcome")

END
```

---

# 27. Complete Example

Resource `EN-US.I18N`:

```text
app.welcome=Welcome to my application.
app.hello=Hello, {0}!
app.files={0} has {1} files.
```

Resource `PT-BR.I18N`:

```text
app.welcome=Bem-vindo ao meu aplicativo.
app.hello=Olá, {0}!
```

Program:

```osl
USING OSL.I18N

FUNCTION MAIN()

    I18N.LOADLANGUAGE("EN-US", "I18N/EN-US.I18N")
    I18N.LOADLANGUAGE("PT-BR", "I18N/PT-BR.I18N")

    I18N.SETDEFAULT("EN-US")
    I18N.SETFALLBACK("EN-US")
    I18N.SETLANGUAGE("PT-BR")

    Name = "Ygor"

    PRINT I18N.GET("app.welcome")
    PRINT I18N.GET("app.hello", Name)
    PRINT I18N.GET("app.files", Name, 12)

END
```

Expected output:

```text
Bem-vindo ao meu aplicativo.
Olá, Ygor!
Ygor has 12 files.
```

The third message falls back to `EN-US` because `app.files` does not exist in `PT-BR`.

---

# 28. Future I18N Extensions

The following are deliberately outside the mandatory 0.6 API:

```text
pluralization
gender-aware messages
locale-aware number formatting
locale-aware date/time formatting
currency formatting
nested resource structures
resource inheritance
ICU MessageFormat
compiled resource files
resource namespaces
runtime language-change events
automatic OS locale detection
```

The basic API is intentionally stable enough to support these later additions without changing the fundamental usage pattern:

```osl
USING OSL.I18N

I18N.GET("some.key", ...)
```

---

# 29. Compatibility

OSLANG 0.6 retains OSLANG 0.51 features, including:

- `NULL`, `STRING`, `NUMBER`, `BOOLEAN`, `ARRAY`, `DATE`, `TIME`;
- functions and `MAIN()`;
- local/global variables;
- arrays and nested arrays;
- classes, inheritance, interfaces, properties, methods, constructors;
- `PUBLIC`, `PROTECTED`, `PRIVATE`;
- closures and arrow functions;
- `ARGS`;
- `IF`, `ELIF`, `ELSE`, `FOR`, `WHILE`, `DO WHILE`;
- `TRY`, `CATCH` with `ERR`;
- `PRINT`, `SHOW`, `INPUT`, `CLEAR`;
- short-circuit evaluation;
- `MATH`, `FILE`, `DIR`;
- primitive methods and collection operations;
- `MOD`, `**`, `++`, `--`, `+=`, `TYPEOF`.

The principal language-level additions in 0.6 are:

```text
USING
OSL namespace
Standard Library model
OSL.I18N
.I18N resource files
```

---

# 30. Definition of Done

OSLANG 0.6 is complete when:

1. `USING` is recognized by the lexer/parser.
2. `OSL` is a reserved standard-library namespace.
3. `OSL.I18N` can be imported.
4. `.I18N` resources can be loaded.
5. UTF-8 resources are supported.
6. `key=value` parsing works.
7. comments and blank lines work.
8. values containing `=` are preserved after the first separator.
9. duplicate keys are detected.
10. positional parameters `{0}`, `{1}`, etc. work.
11. active-language selection works.
12. default language works.
13. explicit fallback works.
14. missing translations return their key by default.
15. `HAS()` works.
16. `KEYS()` works.
17. available languages can be queried.
18. resources can be loaded, reloaded, and unloaded.
19. existing OSB `.I18N` resources can be consumed without conversion.
20. existing OSLANG 0.51 tests continue to pass.
21. existing `.osl` applications remain compatible unless they depend on behavior explicitly changed by 0.6.

---

# 31. Design Principles

OSLANG 0.6 establishes the following Standard Library principles:

1. **Standard libraries live under `OSL`.**
2. **Standard libraries are opt-in through `USING`.**
3. **The core language remains small.**
4. **Standard-library APIs should prefer functions and methods over new keywords.**
5. **OSB-specific capabilities remain distinguishable from portable OSLANG capabilities.**
6. **Existing OSB resources should be reusable whenever practical.**
7. **Localization data belongs outside `.osl` source files.**
8. **Translation lookup should be deterministic and debuggable.**
9. **Fallback should be explicit in 0.6.**
10. **The API should leave room for richer internationalization in future versions.**

The fundamental model is:

```text
OSLANG
   |
   +-- Core Language
   |
   +-- Standard Library
   |      |
   |      +-- OSL
   |           |
   |           +-- I18N
   |
   +-- Host Extensions
          |
          +-- OSB-specific APIs
```

The fundamental usage pattern is:

```osl
USING OSL.I18N

PRINT I18N.GET("boot.welcome")
```

This establishes the Standard Library architecture for OSLANG 0.6 while keeping internationalization modular, explicit, and compatible with the resource files already used by OSB.
