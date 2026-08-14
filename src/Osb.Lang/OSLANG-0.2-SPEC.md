# OSLANG 0.2 Specification

**Language:** OSLANG  
**Version:** 0.2  
**File extension:** `.osl`  
**Paradigm:** Imperative, procedural, and object-oriented  
**Execution model:** Interpreted  
**Typing:** Dynamic with stable variable types  
**Case sensitivity:** Case-insensitive  
**Entry point:** `MAIN()`

---

## 1. Overview

OSLANG 0.2 extends OSLANG 0.1 with object-oriented programming while preserving the language's simplicity, readability, and BASIC-inspired syntax.

New object-oriented features:

- Classes
- Single inheritance
- Multiple interfaces
- Properties
- Methods
- Constructors
- Encapsulation
- `PUBLIC`
- `PROTECTED`
- `PRIVATE`
- `ME`
- Interface contracts
- Interface implementation validation
- Method overriding

OSLANG supports **single class inheritance** and **multiple interface implementation**.

---

## 2. Design Philosophy

1. Classes should be simple to declare.
2. Methods should not require unnecessary syntax.
3. Visibility should be explicit when needed.
4. Members are `PUBLIC` by default.
5. A class can inherit from only one class.
6. A class can implement multiple interfaces.
7. Interfaces define contracts, not implementations.
8. A class implementing an interface must implement all required members.
9. There is no multiple class inheritance.
10. There is no method overloading in 0.2.
11. There is no operator overloading in 0.2.
12. Object-oriented features must not make simple programs unnecessarily verbose.

---

## 3. New Keywords

OSLANG 0.2 adds:

```text
CLASS
CONSTRUCTOR
INTERFACE
ME
PRIVATE
PROTECTED
PUBLIC
```

The complete reserved keyword list is:

```text
AND
BOOLEAN
BOOL
BREAK
CATCH
CEIL
CLEAR
CONTINUE
COUNT
DO
ELIF
ELSE
END
FALSE
FLOOR
FOR
FUNCTION
GLOBAL
IF
INPUT
INTERFACE
ME
NOT
NULL
NUMBER
OR
POW
PRINT
PRIVATE
PROTECTED
PUBLIC
RETURN
SQRT
STEP
STRING
STR
THEN
TO
TRUE
TRY
TYPEOF
VAR
WHILE
CLASS
CONSTRUCTOR
```

Keywords are conventionally written in uppercase.

---

## 4. Classes

Classes are declared using:

```osl
CLASS ClassName

    members

END
```

Example:

```osl
CLASS Person

    VAR Name String
    VAR Age Number

END
```

A class may contain:

- properties;
- methods;
- constructors.

---

## 5. Properties

Properties are declared using `VAR`:

```osl
CLASS Person

    VAR Name String
    VAR Age Number

END
```

Properties belong to an instance of the class.

Class properties are subject to the same dynamic typing and stable type rules as ordinary variables.

---

## 6. Default Visibility

The default visibility of class members is:

```text
PUBLIC
```

Therefore:

```osl
CLASS Person

    VAR Name String

    GetName()

        RETURN ME.Name

    END

END
```

is equivalent to:

```osl
CLASS Person

    PUBLIC VAR Name String

    PUBLIC GetName()

        RETURN ME.Name

    END

END
```

---

## 7. Visibility

OSLANG 0.2 has three visibility levels:

```text
PUBLIC
PROTECTED
PRIVATE
```

Visibility applies to:

- properties;
- methods.

---

## 8. PUBLIC

`PUBLIC` members can be accessed from any valid external context.

Example:

```osl
CLASS Person

    PUBLIC VAR Name String

    PUBLIC GetName()

        RETURN ME.Name

    END

END
```

External code can access:

```osl
Person.Name
Person.GetName()
```

---

## 9. PRIVATE

`PRIVATE` members can only be accessed from within the class that declares them.

Example:

```osl
CLASS Person

    PRIVATE VAR Name String

    PUBLIC GetName()

        RETURN ME.Name

    END

END
```

External code cannot directly access:

```osl
Person.Name
```

A derived class also cannot access a private member.

---

## 10. PROTECTED

`PROTECTED` members can be accessed by:

- the class that declares them;
- classes derived from that class.

Example:

```osl
CLASS Person

    PROTECTED VAR Name String

END
```

A derived class may access:

```osl
ME.Name
```

Unrelated classes cannot access the protected member.

---

## 11. Visibility Syntax

Visibility precedes the member declaration.

Property:

```osl
PRIVATE VAR Color String
```

Method:

```osl
PUBLIC Get()

    RETURN ME.Color

END
```

Protected method:

```osl
PROTECTED Set(Color String)

    ME.Color = Color

END
```

---

## 12. Methods

Methods are functions belonging to a class.

Unlike global functions, class methods do not use the `FUNCTION` keyword.

Example:

```osl
CLASS Calculator

    Add(A Number, B Number)

        RETURN A + B

    END

END
```

Methods may have parameters and return values.

Methods may be `PUBLIC`, `PROTECTED`, or `PRIVATE`.

---

## 13. Global Functions vs Methods

Global functions continue to use `FUNCTION`:

```osl
FUNCTION SUM(A, B)

    RETURN A + B

END FUNCTION
```

Methods do not:

```osl
CLASS Calculator

    SUM(A, B)

        RETURN A + B

    END

END
```

This distinction is intentional.

---

## 14. ME

`ME` refers to the current object instance.

It is conceptually similar to `this` in C# or Java.

Example:

```osl
CLASS Person

    VAR Name String

    GetName()

        RETURN ME.Name

    END

END
```

`ME` can access:

- properties;
- methods.

Examples:

```osl
ME.Name
ME.GetName()
```

---

## 15. Implicit ME

Inside a class, members may be accessed without explicitly writing `ME`.

Therefore:

```osl
Name
```

may refer to:

```osl
ME.Name
```

and:

```osl
GetName()
```

may refer to:

```osl
ME.GetName()
```

`ME` is useful when explicitly resolving ambiguity.

Example:

```osl
Set(Name String)

    ME.Name = Name

END
```

The parameter `Name` shadows the property `Name`, so `ME.Name` explicitly identifies the property.

---

## 16. Constructors

Constructors initialize an object.

Syntax:

```osl
CONSTRUCTOR()

    statements

END
```

Example:

```osl
CLASS Color

    VAR Color String

    CONSTRUCTOR()

        ME.Color = "Blue"

    END

END
```

---

## 17. Constructor Parameters

Constructors may receive parameters:

```osl
CLASS Color

    VAR Color String

    CONSTRUCTOR(Color String)

        ME.Color = Color

    END

END
```

Object creation:

```osl
Color = NEW Color("Red")
```

---

## 18. Constructor Name

Constructors are declared using the keyword:

```text
CONSTRUCTOR
```

They do not use the class name.

Preferred:

```osl
CONSTRUCTOR()

END
```

Not:

```osl
Color()

END
```

This keeps parsing and semantics unambiguous.

---

## 19. Default Constructor

If a class has no explicit constructor, the runtime provides an implicit parameterless constructor.

Example:

```osl
CLASS Person

    VAR Name String

END
```

can be instantiated with:

```osl
Person = NEW Person()
```

The implicit constructor performs normal property initialization.

---

## 20. Constructor Inheritance

Constructors are not inherited.

A derived class may define its own constructor.

Parent initialization occurs automatically before the child constructor.

Example:

```osl
CLASS Color

    VAR Name String

    CONSTRUCTOR()

        ME.Name = "Blue"

    END

END


CLASS Item: Color

    VAR Value Number

    CONSTRUCTOR()

        ME.Value = 10

    END

END
```

Creating:

```osl
Item = NEW Item()
```

initializes the parent portion before executing the child constructor.

Explicit parent constructor invocation is **not part of OSLANG 0.2**.

---

## 21. Object Creation

Objects are created using `NEW`.

Example:

```osl
Color = NEW Color()
```

With parameters:

```osl
Color = NEW Color("Red")
```

`NEW` creates an instance and executes its constructor.

---

## 22. Object Properties

Properties are accessed using `.`:

```osl
Person.Name
```

Assignment:

```osl
Person.Name = "Ygor"
```

Reading:

```osl
Name = Person.Name
```

Visibility rules are enforced.

---

## 23. Object Methods

Methods are called using `.`:

```osl
Person.GetName()
```

With parameters:

```osl
Person.SetName("Ygor")
```

---

## 24. Interfaces

Interfaces define contracts that classes must implement.

Syntax:

```osl
INTERFACE InterfaceName

    members

END
```

Example:

```osl
INTERFACE IColor

    GET()

    SET(Color String)

END
```

Interfaces contain declarations only and do not contain implementations.

---

## 25. Interface Methods

Interface methods define their signatures:

```osl
INTERFACE IColor

    GET()

    SET(Color String)

END
```

An implementing class must provide compatible implementations.

---

## 26. Interface Properties

Interfaces may define required properties:

```osl
INTERFACE IEntity

    VAR Id Number

    GET()

    SET(Value String)

END
```

An implementing class must provide the required property and methods.

Interface properties use the same simple property model as class properties.

---

## 27. Interface Implementation

A class implements an interface using `:`:

```osl
CLASS Color: IColor

    ...

END
```

The class must satisfy the complete interface contract.

---

## 28. Interface Contract Enforcement

If a class declares:

```osl
CLASS Color: IColor

END
```

but does not implement all required members, the program must fail validation.

Example error:

```text
OSLANG ERROR
Class 'Color' does not implement interface 'IColor'.
Missing method: SET(Color STRING)
```

This should preferably be detected during semantic/class validation before execution.

---

## 29. Multiple Interfaces

A class can implement multiple interfaces:

```osl
CLASS Item: IColor, IPrintable, ISerializable

    ...

END
```

All interface contracts must be satisfied.

---

## 30. Single Class Inheritance

A class can inherit from only one class.

Example:

```osl
CLASS Color

    ...

END


CLASS Item: Color

    ...

END
```

Multiple class inheritance is not allowed.

---

## 31. Class Inheritance and Interfaces

The same `:` syntax declares a parent class and interfaces.

The first referenced type is interpreted as the parent class when it is a class.

Additional referenced types are interfaces.

Example:

```osl
CLASS Item: Color, IPrintable, ISerializable

    ...

END
```

means:

```text
Item
 |
 +-- inherits Color
 |
 +-- implements IPrintable
 |
 +-- implements ISerializable
```

If there is no parent class:

```osl
CLASS Item: IPrintable, ISerializable

    ...

END
```

all referenced types are interfaces.

---

## 32. Inherited Members

A derived class inherits accessible members from its parent.

Example:

```osl
CLASS Color

    PROTECTED VAR Color String

    PUBLIC GET()

        RETURN ME.Color

    END

END


CLASS Item: Color

    PUBLIC SHOW()

        PRINT ME.Color

    END

END
```

This is valid because `Color` is protected.

---

## 33. Private Members and Inheritance

Private members are not accessible from derived classes.

Example:

```osl
CLASS Color

    PRIVATE VAR Color String

END


CLASS Item: Color

    PUBLIC SHOW()

        PRINT ME.Color

    END

END
```

This is invalid.

---

## 34. Protected Members and Inheritance

Protected members are accessible from derived classes:

```osl
CLASS Color

    PROTECTED VAR Color String

END


CLASS Item: Color

    PUBLIC SHOW()

        PRINT ME.Color

    END

END
```

This is valid.

---

## 35. Public Members and Inheritance

Public members are inherited and accessible normally.

Example:

```osl
CLASS Color

    PUBLIC GET()

        RETURN "Blue"

    END

END


CLASS Item: Color

END
```

Then:

```osl
Item = NEW Item()

PRINT Item.GET()
```

is valid.

---

## 36. Method Overriding

A derived class may replace an inherited method with another implementation having the same signature.

Example:

```osl
CLASS Color

    GET()

        RETURN "Blue"

    END

END


CLASS RedColor: Color

    GET()

        RETURN "Red"

    END

END
```

Then:

```osl
Color = NEW RedColor()

PRINT Color.GET()
```

returns:

```text
Red
```

This is method overriding.

---

## 37. Method Overloading

Method overloading is not supported in OSLANG 0.2.

These cannot coexist:

```osl
GET()

END


GET(Name String)

END
```

A method name and compatible signature must be unique within the applicable class hierarchy.

---

## 38. Interface Implementation Through Inheritance

If a parent class implements an interface, a derived class inherits that implementation.

Example:

```osl
CLASS Color: IColor

    GET()

        RETURN "Blue"

    END

    SET(Value String)

        ...

    END

END


CLASS Item: Color

END
```

`Item` is considered to satisfy `IColor` through its inherited implementation.

---

## 39. Interface Requirements Across Inheritance

A derived class may introduce additional interfaces:

```osl
CLASS Item: Color, IPrintable

    PRINT_ITEM()

        ...

    END

END
```

The complete class hierarchy must satisfy every required interface.

---

## 40. Interface Visibility

Interface members are implicitly `PUBLIC`.

An implementing class cannot satisfy a public interface contract with a private or protected member.

Invalid:

```osl
INTERFACE IColor

    GET()

END


CLASS Color: IColor

    PRIVATE GET()

        RETURN "Blue"

    END

END
```

The implementation must reject this.

---

## 41. Constructors and Interfaces

Interfaces do not define constructors.

A class implementing an interface is responsible for its own initialization.

---

## 42. Abstract Classes

Abstract classes are not part of OSLANG 0.2.

Use interfaces when a pure contract is required.

---

## 43. Static Members

Static properties and methods are not part of OSLANG 0.2.

Class members are instance members.

Global state continues to use:

```osl
GLOBAL
```

---

## 44. Method Resolution

For:

```osl
Object.GET()
```

the runtime searches:

1. concrete class;
2. parent class;
3. next ancestor;
4. etc.

The first matching implementation is used.

This provides method overriding.

---

## 45. Property Resolution

For:

```osl
Object.Color
```

the runtime searches:

1. concrete class;
2. parent class;
3. ancestors.

Visibility must be validated against the access context.

---

## 46. Type System

OSLANG remains dynamically typed with stable variable types.

Objects introduce runtime class types.

Example:

```osl
Color = NEW Color()
```

The concrete runtime type is:

```text
Color
```

`TYPEOF()` returns the concrete runtime class name for objects.

Example:

```osl
PRINT TYPEOF(Color)
```

returns:

```text
Color
```

---

## 47. NULL and Objects

Objects may be assigned `NULL`:

```osl
Color = NEW Color()
Color = NULL
```

Testing for null:

```osl
IF Color = NULL THEN

    PRINT "No color"

END
```

Accessing a member through `NULL` produces a runtime null-reference error.

---

## 48. Object Truthiness

Valid object references are truthy.

`NULL` is falsy.

Example:

```osl
Color = NEW Color()

IF Color THEN

    PRINT "Object exists"

END
```

---

## 49. Object Equality

Object equality uses reference identity in 0.2.

Two separately created objects are not equal:

```osl
A = NEW Color()
B = NEW Color()

PRINT A = B
```

returns `FALSE`.

If two variables reference the same object:

```osl
A = NEW Color()
B = A

PRINT A = B
```

returns `TRUE`.

---

## 50. Complete Example

```osl
INTERFACE IColor

    GET()

    SET(Color String)

END


CLASS Color: IColor

    PRIVATE VAR Color String

    CONSTRUCTOR()

        ME.Color = "Blue"

    END

    PUBLIC GET()

        RETURN ME.Color

    END

    PUBLIC SET(Color String)

        ME.Color = Color

    END

END


CLASS Item: Color

    PRIVATE VAR Name String

    CONSTRUCTOR()

        ME.Name = "Item"

    END

    PUBLIC GET_NAME()

        RETURN ME.Name

    END

    PUBLIC DESCRIBE()

        PRINT ME.Name + ": " + ME.GET()

    END

END


FUNCTION MAIN()

    Item = NEW Item()

    Item.DESCRIBE()

    Item.SET("Red")

    Item.DESCRIBE()

END FUNCTION
```

Output:

```text
Item: Blue
Item: Red
```

---

## 51. Encapsulation Example

```osl
CLASS BankAccount

    PRIVATE VAR Balance Number

    CONSTRUCTOR()

        ME.Balance = 0

    END

    PUBLIC DEPOSIT(Value Number)

        IF Value > 0 THEN

            ME.Balance = ME.Balance + Value

        END

    END

    PUBLIC GET_BALANCE()

        RETURN ME.Balance

    END

END
```

Usage:

```osl
Account = NEW BankAccount()

Account.DEPOSIT(100)

PRINT Account.GET_BALANCE()
```

Direct access to:

```osl
Account.Balance
```

is invalid.

---

## 52. Runtime Architecture

The OSLANG runtime should extend the 0.1 architecture.

Recommended components:

```text
OSLANG Runtime
│
├── Lexer
├── Parser
├── AST
├── Semantic Analyzer
│   ├── Symbol Table
│   ├── Scope
│   ├── Class Registry
│   ├── Interface Registry
│   └── Type Validation
│
├── Runtime
│   ├── Environment
│   ├── RuntimeValue
│   ├── ObjectInstance
│   ├── ClassDefinition
│   ├── InterfaceDefinition
│   ├── MethodDefinition
│   ├── PropertyDefinition
│   └── ConstructorDefinition
│
└── Standard Library
```

---

## 53. Class Definition

Conceptually:

```text
ClassDefinition
    Name
    BaseClass?
    Interfaces[]
    Properties[]
    Methods[]
    Constructor?
```

---

## 54. Interface Definition

Conceptually:

```text
InterfaceDefinition
    Name
    Properties[]
    Methods[]
```

---

## 55. Object Instance

Conceptually:

```text
ObjectInstance
    ClassDefinition
    PropertyValues
```

Methods resolve against the object's class hierarchy.

---

## 56. Interface Validation

The semantic analyzer should validate interfaces before execution.

For:

```osl
CLASS Color: IColor

END
```

the analyzer should:

1. resolve `IColor`;
2. collect required members;
3. collect members implemented by `Color`;
4. collect inherited members;
5. verify every required member exists;
6. verify signatures;
7. verify visibility;
8. report a semantic error if the contract is incomplete.

---

## 57. Interface Compatibility

An implementation is valid when method name and parameter signature match the interface declaration.

Given:

```osl
INTERFACE IColor

    SET(Color String)

END
```

this is compatible:

```osl
SET(Color String)

    ...

END
```

These are not:

```osl
SET(Color Number)

    ...

END
```

or:

```osl
SET()

    ...

END
```

---

## 58. Access Control

Access violations should preferably be detected during semantic analysis.

Example:

```osl
CLASS Person

    PRIVATE VAR Name String

END


FUNCTION MAIN()

    P = NEW Person()

    PRINT P.Name

END FUNCTION
```

should produce an access error such as:

```text
OSLANG ERROR
Property 'Name' is PRIVATE in class 'Person'.
```

---

## 59. Compatibility with OSLANG 0.1

Everything defined in OSLANG 0.1 remains valid unless explicitly changed here.

This includes:

- variables;
- dynamic typing;
- stable variable types;
- `NULL`;
- arrays;
- functions;
- `MAIN`;
- `IF`;
- `ELIF`;
- `ELSE`;
- `FOR`;
- `WHILE`;
- `DO WHILE`;
- `BREAK`;
- `CONTINUE`;
- `TRY/CATCH`;
- `PRINT`;
- `INPUT`;
- `CLEAR`;
- `STR`;
- `NUMBER`;
- `BOOL`;
- `SQRT`;
- `ABS`;
- `POW`;
- `FLOOR`;
- `CEIL`;
- `COUNT`;
- `TYPEOF`;
- global variables;
- short-circuit evaluation.

---

## 60. Out of Scope for 0.2

The following remain outside the language:

```text
GOTO
GOSUB

multiple class inheritance

abstract classes
static members
static classes

method overloading
operator overloading

generics
templates

namespaces
IMPORT
modules

lambdas
closures

async/await
threads

events
delegates

reflection
arbitrary .NET access
arbitrary C# execution

dynamic member creation

anonymous classes

records
structs
enums

dictionaries/maps
```

These may be considered in future versions.

---

## 61. OSLANG 0.2 Principle

The introduction of object orientation must not turn OSLANG into a verbose enterprise language.

Simple things should remain simple.

A minimal class:

```osl
CLASS Person

    VAR Name String

END
```

A more complete class:

```osl
CLASS Person

    PRIVATE VAR Name String

    CONSTRUCTOR(Name String)

        ME.Name = Name

    END

    PUBLIC GET_NAME()

        RETURN ME.Name

    END

END
```

OSLANG should remain recognizably BASIC-inspired while providing a clean object-oriented model.
