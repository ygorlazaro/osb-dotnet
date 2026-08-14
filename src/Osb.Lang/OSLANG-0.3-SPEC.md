# OSLANG 0.3 Specification

**Language:** OSLANG  
**Version:** 0.3  
**File extension:** `.osl`  
**Paradigm:** Imperative, procedural, and object-oriented  
**Execution model:** Interpreted  
**Typing:** Dynamic with stable variable types  
**Case sensitivity:** Case-insensitive  
**Entry point:** `MAIN()`

---

## 1. Overview

OSLANG 0.3 extends OSLANG 0.2 with:

- Multi-file programs via `USING`
- Order-independent declarations and symbol resolution
- `SWITCH` / `CASE` / `DEFAULT` statements
- Switch expressions
- Events (`EVENT`, `ON`, `RAISE`)
- Generics
- Function and method overloading
- `VIRTUAL` and `OVERRIDE` methods

OSLANG 0.3 preserves the BASIC-inspired simplicity and readability of previous versions.

---

## 2. Design Philosophy

1. The language must remain simple and readable.
2. BASIC-inspired syntax is preferred over C#-style syntax when both are equally expressive.
3. Explicit behavior is preferred over hidden magic.
4. Declaration order must not matter. The compiler must discover all declarations before resolving references.
5. Multi-phase compilation:
   - Lexing
   - Parsing
   - Symbol discovery / declaration indexing
   - Type and symbol resolution
   - Semantic validation
   - Execution / code generation
6. All valid OSLANG 0.1 and 0.2 programs must remain valid in 0.3 unless explicitly incompatible.

---

## 3. Case Insensitivity

OSLANG is completely case-insensitive.

Identifiers:

```osl
Person
PERSON
person
PeRsOn
```

Keywords:

```osl
SWITCH
Switch
switch
```

The implementation normalizes identifiers internally. Documentation uses uppercase keywords by convention.

---

## 4. Source Files

The file extension remains `.osl`.

A program may consist of multiple source files:

```
main.osl
person.osl
math.osl
shell.osl
```

The main file is the file containing `FUNCTION MAIN()`. There is no requirement that the file be named `MAIN.OSL`.

There must be exactly one `MAIN` entry point in a program.

---

## 5. USING

`USING` establishes a dependency between source files/modules.

Syntax:

```osl
USING PERSON
USING MATH
USING OSB.SHELL
```

`USING` statements must appear at the beginning of the file, before any declarations. `USING` may appear after comments but before executable/declarative content.

Invalid:

```osl
FUNCTION MAIN()

END FUNCTION

USING PERSON
```

The compiler rejects `USING` statements that occur after declarations.

---

## 6. USING Semantics

`USING` causes the compiler to:

1. Load the referenced source file/module.
2. Parse it.
3. Discover its declarations.
4. Make those declarations available for symbol resolution.

Circular `USING` dependencies are detected and rejected:

```osl
' A.OSL
USING B

' B.OSL
USING A
```

The compiler avoids recursively parsing the same module multiple times. Source files are parsed once and cached.

---

## 7. MAIN File

The main file is determined by the presence of `FUNCTION MAIN()`. There is no requirement that the file be named `MAIN.OSL`.

A program without `MAIN()` fails compilation. Multiple `MAIN()` functions fail compilation.

---

## 8. Declaration Order

Declaration order is irrelevant.

All of the following are valid:

```osl
FUNCTION MAIN()
    P = NEW PERSON()
    P.SAYHELLO()
END FUNCTION

CLASS PERSON
    FUNCTION SAYHELLO()
        PRINT "Hello"
    END FUNCTION
END CLASS
```

```osl
FUNCTION MAIN()
    PRINT SUM(10, 20)
END FUNCTION

FUNCTION SUM(A, B)
    RETURN A + B
END FUNCTION
```

```osl
CLASS STUDENT EXTENDS PERSON
END CLASS

CLASS PERSON
END CLASS
```

```osl
FUNCTION MAIN()
    B = NEW BOX<NUMBER>()
END FUNCTION

CLASS BOX<T>
    VALUE T
END CLASS
```

---

## 9. Symbol Discovery

The compiler performs a declaration-discovery phase before resolving references.

During this phase, the compiler collects:

- functions and their overload sets
- methods
- classes and their members
- fields/properties
- events
- generic type definitions
- generic methods
- constants
- module declarations
- `USING` dependencies

The discovery phase establishes the existence and basic signatures of declarations. A later semantic phase resolves references.

---

## 10. Duplicate Symbols

The compiler detects duplicate declarations:

- Two classes with the same name in the same module scope.
- Two functions with the same signature.
- Two fields with the same name in the same class.
- Two events with the same name in the same class.

Overloaded functions are allowed only when their signatures differ according to OSLANG overload rules.

---

## 11. Classes

Syntax:

```osl
CLASS PERSON

    NAME STRING
    AGE NUMBER

    FUNCTION SAYHELLO()

        PRINT "Hello, " + NAME

    END FUNCTION

END CLASS
```

Classes may contain:

- fields/properties
- methods
- events
- constructors
- inherited members

---

## 12. Object Creation

Objects are created using `NEW`:

```osl
P = NEW PERSON()
```

Constructor arguments:

```osl
P = NEW PERSON("Ygor", 40)
```

The implementation supports constructor overloads.

---

## 13. Instance Members

Instance members are accessed using `.`:

```osl
P.NAME
P.AGE
P.SAYHELLO()
```

---

## 14. THIS

Inside an instance method, the current object is available through `THIS`:

```osl
CLASS PERSON

    NAME STRING

    FUNCTION SETNAME(NewName)

        THIS.NAME = NewName

    END FUNCTION

END CLASS
```

---

## 15. Constructors

Classes may define constructors using `CONSTRUCTOR`:

```osl
CLASS PERSON

    NAME STRING

    CONSTRUCTOR(NewName)

        NAME = NewName

    END CONSTRUCTOR

END CLASS
```

Constructor overloads are allowed.

A class without an explicit constructor receives a default parameterless constructor.

---

## 16. Inheritance

OSLANG 0.3 supports single class inheritance.

Syntax:

```osl
CLASS STUDENT EXTENDS PERSON

    ...

END CLASS
```

`STUDENT` inherits accessible members from `PERSON`. Only single class inheritance is supported in 0.3. Multiple class inheritance is not supported.

---

## 17. OVERRIDE

A derived class can override an inherited method using `OVERRIDE`:

```osl
CLASS PERSON

    FUNCTION GREET()

        PRINT "Hello"

    END FUNCTION

END CLASS


CLASS STUDENT EXTENDS PERSON

    OVERRIDE FUNCTION GREET()

        PRINT "Hello, student"

    END FUNCTION

END CLASS
```

`OVERRIDE` is only valid when a compatible inherited method exists. An `OVERRIDE` without a corresponding inherited method is a compilation error.

---

## 18. VIRTUAL Methods

Methods intended to be overridden must be declared `VIRTUAL`:

```osl
CLASS PERSON

    VIRTUAL FUNCTION GREET()

        PRINT "Hello"

    END FUNCTION

END CLASS
```

A method without `VIRTUAL` cannot be overridden. This makes the override contract explicit.

---

## 19. Method Dispatch

Virtual methods use dynamic dispatch.

Example:

```osl
P = NEW STUDENT()
P.GREET()
```

executes `STUDENT.GREET()` when `GREET` is virtual and overridden.

---

## 20. Function and Method Overloading

The same function name may exist with different parameter signatures:

```osl
FUNCTION PRINTVALUE(Value STRING)

    PRINT Value

END FUNCTION


FUNCTION PRINTVALUE(Value NUMBER)

    PRINT Value

END FUNCTION
```

The compiler/runtime resolves the correct overload based on argument types.

---

## 21. Overload Rules

Overloads must differ by parameter signature. Parameter names alone do not distinguish overloads.

Invalid:

```osl
FUNCTION SUM(A NUMBER, B NUMBER)
    ...
END FUNCTION

FUNCTION SUM(X NUMBER, Y NUMBER)
    ...
END FUNCTION
```

These are the same signature.

Valid:

```osl
FUNCTION SUM(A NUMBER, B NUMBER)
    ...
END FUNCTION

FUNCTION SUM(A NUMBER, B NUMBER, C NUMBER)
    ...
END FUNCTION
```

---

## 22. Ambiguous Overloads

If multiple overloads are equally valid, compilation fails with an ambiguity error. The error identifies the candidate functions. The compiler does not randomly select an overload.

---

## 23. Generics

Generics are simple and readable.

Example:

```osl
CLASS BOX<T>

    VALUE T

END CLASS
```

Usage:

```osl
NUMBERBOX = NEW BOX<NUMBER>()
STRINGBOX = NEW BOX<STRING>()
```

---

## 24. Generic Classes

Syntax:

```osl
CLASS BOX<T>

    VALUE T

END CLASS
```

Multiple type parameters are allowed:

```osl
CLASS PAIR<T, U>

    FIRST T
    SECOND U

END CLASS
```

---

## 25. Generic Functions

Functions may have generic type parameters:

```osl
FUNCTION IDENTITY<T>(VALUE T)

    RETURN VALUE

END FUNCTION
```

Usage:

```osl
A = IDENTITY<NUMBER>(10)
B = IDENTITY<STRING>("Hello")
```

The compiler may infer generic arguments when unambiguous:

```osl
A = IDENTITY(10)
```

infers `T = NUMBER`.

If type inference cannot determine `T`, compilation produces an error.

---

## 26. Generic Constraints

Generic constraints are not required for the initial 0.3 implementation. Future versions may introduce `WHERE` clauses, constraints, interfaces, `new()`, etc.

---

## 27. SWITCH Statement

Syntax:

```osl
SWITCH expression

    CASE value

        statements

    CASE value

        statements

    DEFAULT

        statements

END
```

Example:

```osl
SWITCH Day

    CASE 1
        PRINT "Monday"

    CASE 2
        PRINT "Tuesday"

    CASE 3
        PRINT "Wednesday"

    DEFAULT
        PRINT "Unknown"

END
```

---

## 28. CASE

`CASE` values are compared against the `SWITCH` expression.

Multiple `CASE` values may be supported:

```osl
CASE 1, 2, 3

    PRINT "Small number"
```

This is equivalent to matching any of the listed values.

---

## 29. DEFAULT

`DEFAULT` is optional. If no `CASE` matches and `DEFAULT` exists, `DEFAULT` executes. Only one `DEFAULT` is allowed. Duplicate `DEFAULT` declarations are a compilation error.

---

## 30. SWITCH Break Behavior

`SWITCH` cases do not fall through by default. After a matching `CASE` finishes, execution leaves the `SWITCH`. `BREAK` may still be used inside loops.

---

## 31. SWITCH Expression / Switch Operator

OSLANG 0.3 introduces a switch expression for returning a value:

```osl
Result = SWITCH Day

    CASE 1 => "Monday"
    CASE 2 => "Tuesday"
    CASE 3 => "Wednesday"
    DEFAULT => "Unknown"
```

The switch expression must produce exactly one value. It is an expression, not a statement.

All branches must produce compatible values. `NULL` may be used as a result.

---

## 32. Events

Events provide a simple mechanism for one object or component to notify interested code that something happened.

Declaration:

```osl
CLASS BUTTON

    EVENT CLICKED

    EVENT CLICKED(X NUMBER, Y NUMBER)

END CLASS
```

An event may optionally define parameters.

---

## 33. Event Subscription

Use `ON` to subscribe to an event:

```osl
ON Button.CLICKED

    PRINT "Button clicked"

END ON
```

The handler is associated with the event.

---

## 34. Event Emission

Use `RAISE` to trigger an event:

```osl
RAISE CLICKED
```

or with parameters:

```osl
RAISE CLICKED(X, Y)
```

Only the owning object should normally raise its own event. The semantic analyzer enforces visibility rules.

---

## 35. Event Handlers

An event handler receives the event parameters:

```osl
ON Button.CLICKED(X, Y)

    PRINT "Clicked at:"
    PRINT X
    PRINT Y

END ON
```

The handler executes when the event is raised.

---

## 36. Event Design Principle

Events are language-level abstractions rather than direct exposure of .NET delegates/events. The runtime provides a clean bridge between OSB events and OSLANG event handlers.

---

## 37. Event Lifetime

Event subscriptions are managed by the runtime. When an object is destroyed or becomes unreachable, its subscriptions must not keep it alive unnecessarily.

---

## 38. Access Modifiers

OSLANG 0.3 supports basic member visibility:

- `PUBLIC`
- `PRIVATE`
- `PROTECTED`

Default visibility is `PUBLIC` for class members.

---

## 39. Static Members

Static members are optional for the first 0.3 implementation. If implemented:

```osl
CLASS MATH

    STATIC FUNCTION MAX(A, B)

        ...

    END FUNCTION

END CLASS
```

If static members are not necessary for the first implementation, they are left out.

---

## 40. USING and Namespaces

`USING` is primarily a source/module dependency mechanism. OSLANG 0.3 does not require C#-style namespaces unless required by the existing OSB architecture.

---

## 41. Module Loading

`USING` resolution is handled by a module resolver abstraction.

Conceptually:

```csharp
public interface IModuleResolver
{
    Task<Module?> ResolveAsync(string moduleName, CancellationToken ct = default);
}
```

Responsibilities:

- Locating `.osl` files
- Loading source
- Normalizing paths
- Preventing duplicate loads
- Detecting circular dependencies

The parser does not hard-code filesystem access.

---

## 42. AST

The AST supports:

- classes, constructors, fields, methods
- inheritance, virtual methods, override methods
- generic declarations and generic type references
- overloaded methods
- events, event handlers
- `SWITCH` statements and expressions
- `USING` declarations
- member access
- `NEW` expressions

The AST preserves source locations.

---

## 43. Symbol Table

The symbol table supports:

- multiple scopes
- functions as overload sets
- classes, inheritance, fields, methods
- generic types and generic methods
- events, constructors
- modules and `USING` dependencies

A function name maps to an overload set, not a single function.

---

## 44. Type System

OSLANG remains dynamically typed at runtime. OSLANG 0.3 requires stronger compile-time semantic analysis because of classes, inheritance, overload resolution, generics, and override validation.

Dynamic runtime typing is distinct from absence of semantic/type analysis. The compiler/interpreter validates declarations and signatures wherever possible.

---

## 45. Type Resolution

A dedicated type resolver resolves:

- primitive types (`NUMBER`, `STRING`, `BOOLEAN`, `NULL`)
- arrays
- classes and inherited classes
- generic types and generic instances
- function parameter types, return types, method signatures

Type resolution is independent of source declaration order.

---

## 46. Inheritance Resolution

Inheritance relationships are resolved after all class declarations have been discovered.

Circular inheritance is rejected:

```osl
CLASS A EXTENDS B
END CLASS

CLASS B EXTENDS A
END CLASS
```

---

## 47. Override Validation

For `OVERRIDE FUNCTION GREET()`, the compiler verifies:

1. A parent class exists.
2. The parent contains a compatible method.
3. That method is `VIRTUAL`.
4. The signatures are compatible.

---

## 48. Method Lookup

Method lookup considers inheritance:

```osl
CLASS PERSON
    FUNCTION SAYHELLO()
        PRINT "Hello"
    END FUNCTION
END CLASS

CLASS STUDENT EXTENDS PERSON
END CLASS

S = NEW STUDENT()
S.SAYHELLO()
```

finds `PERSON.SAYHELLO()`.

---

## 49. Overload and Inheritance

Overload resolution considers inherited methods:

```osl
CLASS PERSON
    FUNCTION SAY(A STRING)
        ...
    END FUNCTION
END CLASS

CLASS STUDENT EXTENDS PERSON
    FUNCTION SAY(A NUMBER)
        ...
    END FUNCTION
END CLASS
```

The resulting method set contains both compatible overloads.

---

## 50. Overload and Override

`OVERRIDE` applies to a specific signature:

```osl
CLASS PERSON
    VIRTUAL FUNCTION SAY(A STRING)
        ...
    END FUNCTION

    VIRTUAL FUNCTION SAY(A NUMBER)
        ...
    END FUNCTION
END CLASS

CLASS STUDENT EXTENDS PERSON
    OVERRIDE FUNCTION SAY(A STRING)
        ...
    END FUNCTION
END CLASS
```

Only `SAY(STRING)` is overridden. `SAY(NUMBER)` remains inherited.

---

## 51. Generic Type Resolution

Generic types participate in symbol resolution:

```osl
CLASS BOX<T>
    VALUE T
END CLASS

FUNCTION MAIN()
    B = NEW BOX<NUMBER>()
    B.VALUE = 10
END FUNCTION
```

This resolves correctly even if `BOX` is declared later.

---

## 52. Runtime Object Model

Objects contain:

- runtime class/type
- instance fields
- event subscriptions where applicable

Method invocation resolves through the class hierarchy. Virtual dispatch occurs at runtime.

---

## 53. NULL and Objects

`NULL` may be assigned to object references:

```osl
PERSON = NULL
```

Calling an instance method through `NULL` generates a runtime error. The runtime does not silently treat `NULL` as an empty object.

---

## 54. Equality

`NULL` only equals `NULL`.

Primitive equality follows the established OSLANG rules. For objects, equality is identity-based by default.

---

## 55. Compilation Model

A project is conceptually compiled as:

```
Project
 +-- Main.osl
 +-- Person.osl
 +-- Math.osl
 +-- Other.osl
```

The compiler builds a module graph, then:

1. Loads modules
2. Resolves `USING` dependencies
3. Discovers declarations
4. Builds global symbol tables
5. Resolves types
6. Resolves inheritance
7. Validates overloads
8. Validates overrides
9. Validates generics
10. Validates event declarations
11. Produces executable representation

---

## 56. Caching

A source file is not parsed repeatedly when imported by multiple modules. The compiler uses canonical module identities/paths.

---

## 57. Standard Library

All OSLANG 0.1/0.2 standard library features remain available:

- `PRINT`, `INPUT`, `CLEAR`
- `STR()`, `NUMBER()`, `BOOL()`
- `SQRT()`, `ABS()`, `POW()`, `FLOOR()`, `CEIL()`
- `COUNT()`, `TYPEOF()`

---

## 58. OSB Extensions

The existing OSLANG extension architecture remains. OSB-specific functionality stays outside the language core. OSB.Shell may expose functions and objects. Future OSB components may expose events.

The OSLANG runtime provides a clean bridge between OSB events and OSLANG event handlers.

---

## 59. Error Model

Errors are categorized as:

- lexical errors
- syntax errors
- semantic errors
- runtime errors

0.3 adds specialized semantic errors:

- duplicate declaration
- unknown symbol
- unknown type
- invalid inheritance
- circular inheritance
- invalid override
- missing virtual method
- ambiguous overload
- invalid generic arguments
- circular `USING`
- duplicate `MAIN`
- missing `MAIN`
- invalid event handler
- inaccessible member

Errors preserve:

- source file
- line
- column
- useful message

---

## 60. Critical Forward-Reference Tests

The following tests are mandatory.

### Test 1: Forward class reference

```osl
FUNCTION MAIN()
    P = NEW PERSON()
END FUNCTION

CLASS PERSON
END CLASS
```

Must compile.

### Test 2: Forward function reference

```osl
FUNCTION MAIN()
    PRINT SUM(10, 20)
END FUNCTION

FUNCTION SUM(A, B)
    RETURN A + B
END FUNCTION
```

Must compile.

### Test 3: Forward inheritance

```osl
CLASS STUDENT EXTENDS PERSON
END CLASS

CLASS PERSON
END CLASS
```

Must compile.

### Test 4: Forward generic reference

```osl
FUNCTION MAIN()
    B = NEW BOX<NUMBER>()
END FUNCTION

CLASS BOX<T>
    VALUE T
END CLASS
```

Must compile.

---

## 61. SWITCH Tests

### Statement form

```osl
FUNCTION MAIN()
    Value = 2

    SWITCH Value

        CASE 1
            PRINT "One"

        CASE 2
            PRINT "Two"

        DEFAULT
            PRINT "Other"

    END

END FUNCTION
```

### Expression form

```osl
FUNCTION MAIN()
    Value = 2

    Result = SWITCH Value

        CASE 1 => "One"
        CASE 2 => "Two"
        DEFAULT => "Other"

    PRINT Result

END FUNCTION
```

---

## 62. Class Tests

```osl
CLASS PERSON

    NAME STRING

    CONSTRUCTOR(NewName)

        NAME = NewName

    END CONSTRUCTOR

    VIRTUAL FUNCTION GREET()

        PRINT "Hello, " + NAME

    END FUNCTION

END CLASS


FUNCTION MAIN()

    P = NEW PERSON("Ygor")

    P.GREET()

END FUNCTION
```

---

## 63. Inheritance Tests

```osl
CLASS PERSON

    NAME STRING

    VIRTUAL FUNCTION GREET()

        PRINT "Hello, " + NAME

    END FUNCTION

END CLASS


CLASS STUDENT EXTENDS PERSON

    OVERRIDE FUNCTION GREET()

        PRINT "Hello, student " + NAME

    END FUNCTION

END CLASS


FUNCTION MAIN()

    P = NEW STUDENT()

    P.GREET()

END FUNCTION
```

Runtime must dispatch to `STUDENT.GREET()`.

---

## 64. Generic Tests

```osl
CLASS BOX<T>

    VALUE T

END CLASS


FUNCTION IDENTITY<T>(VALUE T)

    RETURN VALUE

END FUNCTION


FUNCTION MAIN()

    B = NEW BOX<NUMBER>()

    B.VALUE = 42

    Result = IDENTITY<NUMBER>(B.VALUE)

    PRINT Result

END FUNCTION
```

---

## 65. Event Tests

```osl
CLASS BUTTON

    EVENT CLICKED

    FUNCTION CLICK()

        RAISE CLICKED

    END FUNCTION

END CLASS


FUNCTION MAIN()

    Button = NEW BUTTON()

    ON Button.CLICKED

        PRINT "Button clicked"

    END ON

    Button.CLICK()

END FUNCTION
```

---

## 66. Multi-File Tests

### PERSON.OSL

```osl
CLASS PERSON

    NAME STRING

    FUNCTION GREET()

        PRINT "Hello, " + NAME

    END FUNCTION

END CLASS
```

### MAIN.OSL

```osl
USING PERSON


FUNCTION MAIN()

    P = NEW PERSON()

    P.NAME = "Ygor"

    P.GREET()

END FUNCTION
```

The program must compile and execute.

---

## 67. Multi-File Forward Reference

MAIN.OSL:

```osl
USING PERSON


FUNCTION MAIN()

    P = NEW PERSON()

END FUNCTION
```

PERSON.OSL:

```osl
CLASS PERSON

END CLASS
```

Declaration order between files must not matter.

---

## 68. Implementation Architecture

```
OSLANG
 |
 +-- Lexer
 |
 +-- Parser
 |
 +-- AST
 |
 +-- Module System
 |
 +-- Symbol Discovery
 |
 +-- Symbol Table
 |
 +-- Type Resolver
 |
 +-- Semantic Analyzer
 |
 +-- Runtime
 |     |
 |     +-- Environment
 |     +-- Objects
 |     +-- Classes
 |     +-- Generics
 |     +-- Events
 |     +-- Functions
 |
 +-- Standard Library
 |
 +-- Extension API
 |
 +-- OSB Integration
```

---

## 69. Performance Guidelines

Correctness and maintainability are more important than raw performance.

Guidelines:

- Parse each source file once.
- Cache modules by canonical path.
- Avoid repeated type resolution.
- Avoid repeated overload searches when possible.
- Use canonical symbols.
- Separate compile-time representations from runtime values.

---

## 70. Backward Compatibility

All valid OSLANG 0.1 and 0.2 programs remain valid in OSLANG 0.3 unless a behavior is explicitly identified as incompatible.

Preserved features:

- variables, dynamic typing, `NULL`, arrays
- functions, `MAIN`
- `IF`, `ELIF`, `ELSE`, `FOR`, `WHILE`, `DO WHILE`
- `BREAK`, `CONTINUE`, `TRY/CATCH`
- `PRINT`, `INPUT`, `CLEAR`
- `STR`, `NUMBER`, `BOOL`, `SQRT`, `ABS`, `POW`, `FLOOR`, `CEIL`, `COUNT`, `TYPEOF`
- short-circuit `AND`/`OR`
- classes, interfaces, inheritance, constructors, `ME`, `BASE`

---

## 71. Features Not Required Yet

Do not implement the following unless required internally:

- Multiple inheritance
- Interfaces as language constructs beyond 0.2
- Abstract classes
- Operator overloading
- Properties with custom getters/setters
- Attributes/annotations
- Reflection
- Async/await
- Coroutines
- LINQ
- Pattern matching beyond `SWITCH`/`CASE`
- Namespaces with complex resolution
- Dependency injection
- Macros
- Metaprogramming
- Arbitrary .NET interoperability

These may belong to future versions.
