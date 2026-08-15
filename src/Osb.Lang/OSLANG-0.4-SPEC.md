# OSLANG 0.4 Specification

**Language:** OSLANG  
**Version:** 0.4  
**File extension:** `.osl`  
**Paradigm:** Imperative, procedural, and object-oriented  
**Typing:** Dynamic with stable variable types  
**Case sensitivity:** Case-insensitive  
**Entry point:** `MAIN()`

## 1. Overview

OSLANG 0.4 extends the previous version with a practical standard library centered on primitive methods, collections, mathematics, files, and directories.

The goals are:

- make `STRING`, `NUMBER`, `BOOLEAN`, and `ARRAY` directly useful;
- provide common text and collection operations;
- support `MAP`, `FILTER`, `ANY`, `SOME`, `ALL`, and `REDUCE`;
- organize mathematical operations under `MATH`;
- provide controlled file operations under `FILE`;
- provide controlled directory operations under `DIR`;
- preserve the simple BASIC-inspired syntax.

## 2. Breaking Change: MATH

Mathematical global functions from previous versions are removed.

Old:

```osl
SQRT(25)
ABS(-10)
POW(2, 3)
FLOOR(3.7)
CEIL(3.2)
```

New:

```osl
MATH.SQRT(25)
MATH.ABS(-10)
MATH.POW(2, 3)
MATH.FLOOR(3.7)
MATH.CEIL(3.2)
```

This is an intentional breaking change.

## 3. New Keywords

```text
MATH
FILE
DIR
```

The language remains case-insensitive, so `MATH.SQRT`, `math.sqrt`, and `Math.Sqrt` are equivalent.

These are standard-library namespaces, not user-defined objects.

---

# 4. Primitive Methods

Primitive values expose built-in methods using the existing member-access syntax:

```osl
Text.TOUPPER()
Numbers.COUNT()
Value.TOSTRING()
```

The runtime should implement these through a primitive-method dispatcher rather than adding parser special cases.

---

# 5. STRING

Required methods:

### TOUPPER

```osl
Name = "Ygor"
PRINT Name.TOUPPER()
```

Returns an uppercase string.

### TOLOWER

```osl
Name = "YGOR"
PRINT Name.TOLOWER()
```

### TRIM

Removes leading and trailing whitespace.

```osl
Text = "   Hello   "
PRINT Text.TRIM()
```

### STARTSWITH

```osl
IF Name.STARTSWITH("OS") THEN
    PRINT "Yes"
END
```

Returns `BOOLEAN`.

### ENDSWITH

```osl
IF Name.ENDSWITH(".osl") THEN
    PRINT "OSLANG file"
END
```

### CONTAINS

```osl
IF Text.CONTAINS("World") THEN
    PRINT "Found"
END
```

### INDEXOF

Returns the zero-based index of a substring, or `-1` if absent.

```osl
Position = Text.INDEXOF("World")
```

### SUBSTR

Returns a substring.

```osl
Text = "Hello World"
Result = Text.SUBSTR(0, 5)
```

The first argument is the zero-based start position; the second is the number of characters.

### REPLACE

```osl
Result = Text.REPLACE("World", "OSLANG")
```

### SPLIT

Returns an array of strings.

```osl
Colors = "Red,Green,Blue".SPLIT(",")
```

Result:

```text
["Red", "Green", "Blue"]
```

### COUNT

Returns the number of characters.

```osl
Text.COUNT()
```

### ISEMPTY

Returns `TRUE` when the string has zero characters.

### REVERSE

Returns a reversed string.

```osl
"abc".REVERSE()
```

returns:

```text
"cba"
```

### TOSTRING

Returns the string itself.

---

# 6. NUMBER

Required methods:

```text
TOSTRING()
ABS()
FLOOR()
CEIL()
ISINTEGER()
BETWEEN(MIN, MAX)
```

Examples:

```osl
Value = 123
Text = Value.TOSTRING()

Absolute = (-10).ABS()

IF Value.BETWEEN(10, 100) THEN
    PRINT "In range"
END
```

`TOSTRING()` returns the textual representation of the number.

---

# 7. BOOLEAN

Required methods:

```text
TOSTRING()
TOGGLE()
```

Example:

```osl
Active = TRUE
Text = Active.TOSTRING()

Active = Active.TOGGLE()
```

`TRUE.TOSTRING()` returns `"TRUE"` and `FALSE.TOSTRING()` returns `"FALSE"`.

---

# 8. ARRAY

Arrays remain homogeneous. An array cannot mix element types.

Valid:

```osl
[1, 2, 3]
["A", "B"]
[TRUE, FALSE]
```

Invalid:

```osl
[1, "A", TRUE]
```

Required methods:

```text
COUNT()
FIRST()
LAST()
CONTAINS(Value)
INDEXOF(Value)
ADD(Value)
REMOVE(Value)
CLEAR()
REVERSE()
SORT()
MAP(Function)
FILTER(Function)
ANY(Function)
SOME(Function)
ALL(Function)
REDUCE(Function, Initial)
JOIN(Separator)
```

## COUNT

```osl
Numbers.COUNT()
```

Returns the number of elements.

## FIRST

Returns the first element. Empty arrays produce a runtime error.

## LAST

Returns the last element. Empty arrays produce a runtime error.

## CONTAINS

```osl
Numbers.CONTAINS(2)
```

Returns `BOOLEAN`.

## INDEXOF

Returns the zero-based index or `-1`.

## ADD

Adds an element to the end.

```osl
Numbers.ADD(4)
```

The element must be compatible with the array type.

## REMOVE

Removes the first matching element.

## CLEAR

Removes all elements.

## REVERSE

Returns a reversed array.

## SORT

Returns a sorted array. Numeric arrays use numeric ordering; string arrays use standard string ordering.

## JOIN

Converts an array to a string separated by the supplied separator.

```osl
Names = ["Ygor", "Lara"]
Text = Names.JOIN(",")
```

Result:

```text
Ygor,Lara
```

---

# 9. Functional Array Operations

OSLANG 0.4 supports named functions as callbacks.

Lambdas and closures remain outside 0.4.

Example:

```osl
FUNCTION DOUBLE(Value)
    RETURN Value * 2
END
```

The function can be passed as:

```osl
Numbers.MAP(DOUBLE)
```

The runtime must therefore support callable function references as arguments without requiring a new lambda syntax.

## MAP

Transforms every element.

```osl
Result = Numbers.MAP(DOUBLE)
```

The result may have a different element type.

## FILTER

Keeps elements for which the predicate returns `TRUE`.

```osl
FUNCTION EVEN(Value)
    RETURN Value % 2 = 0
END

Result = Numbers.FILTER(EVEN)
```

## ANY

Returns `TRUE` when at least one element satisfies the predicate.

## SOME

Alias of `ANY`.

## ALL

Returns `TRUE` when every element satisfies the predicate.

## REDUCE

Combines elements into one value.

```osl
FUNCTION SUM(A, B)
    RETURN A + B
END

Result = Numbers.REDUCE(SUM, 0)
```

Conceptually:

```text
Accumulator = Initial
Accumulator = Function(Accumulator, Element)
```

for every element.

### Short-circuiting

`ANY` and `SOME` stop at the first `TRUE`.

`ALL` stops at the first `FALSE`.

### Empty arrays

```text
COUNT  -> 0
ANY    -> FALSE
SOME   -> FALSE
ALL    -> TRUE
FILTER -> []
MAP    -> []
```

`REDUCE` returns the supplied initial value on an empty array.

---

# 10. MATH

All mathematical functionality is exposed through `MATH`.

Required functions:

```text
SQRT(Value)
ABS(Value)
POW(Value, Exponent)
FLOOR(Value)
CEIL(Value)
RANDOM()
RANDOM(MIN, MAX)
MIN(A, B)
MAX(A, B)
CLAMP(Value, Min, Max)
SIGN(Value)
ROUND(Value)
ROUND(Value, Digits)
TRUNC(Value)
MOD(A, B)
SIN(Value)
COS(Value)
TAN(Value)
ASIN(Value)
ACOS(Value)
ATAN(Value)
ATAN2(Y, X)
LOG(Value)
LOG10(Value)
EXP(Value)
```

Required constants:

```text
MATH.PI
MATH.E
```

## RANDOM

```osl
MATH.RANDOM()
```

returns a decimal in:

```text
0 <= value < 1
```

The integer form:

```osl
MATH.RANDOM(1, 10)
```

returns an integer from `1` through `10`, inclusive.

## CLAMP

```osl
MATH.CLAMP(150, 0, 100)
```

returns `100`.

## SIGN

Returns `-1`, `0`, or `1`.

## MOD

```osl
MATH.MOD(10, 3)
```

returns `1`.

---

# 11. FILE

`FILE` provides controlled filesystem operations.

Required operations:

```text
EXISTS(Path)
READ(Path)
READTEXT(Path)
WRITE(Path, Text)
APPEND(Path, Text)
CREATE(Path)
DELETE(Path)
DEL(Path)
COPY(Source, Destination)
MOVE(Source, Destination)
SIZE(Path)
EXTENSION(Path)
NAME(Path)
DIR(Path)
OPEN(Path)
```

## EXISTS

```osl
IF FILE.EXISTS("config.txt") THEN
    PRINT "Exists"
END
```

## READ

Reads a text file and returns an array of lines.

```osl
Lines = FILE.READ("config.txt")
```

Line terminators are not included in individual strings.

Default encoding is UTF-8.

## READTEXT

Reads the entire file as a single string.

```osl
Content = FILE.READTEXT("config.txt")
```

## WRITE

Replaces the contents of a text file.

```osl
FILE.WRITE("output.txt", "Hello OSLANG")
```

## APPEND

Appends text.

```osl
FILE.APPEND("log.txt", "New entry")
```

## CREATE

Creates an empty file. If the file already exists, it reports an error instead of silently deleting its contents.

## DELETE / DEL

Deletes a file.

`DEL` is a convenience alias for `DELETE`.

## COPY

```osl
FILE.COPY("source.txt", "destination.txt")
```

## MOVE

```osl
FILE.MOVE("old.txt", "new.txt")
```

## SIZE

Returns the size in bytes.

```osl
Size = FILE.SIZE("data.bin")
```

## EXTENSION

```osl
FILE.EXTENSION("program.osl")
```

returns:

```text
.osl
```

## NAME

```osl
FILE.NAME("/tmp/program.osl")
```

returns:

```text
program.osl
```

## DIR

Returns the directory portion of a path.

## OPEN

Provides controlled streaming access:

```osl
Stream = FILE.OPEN("large.txt")
```

The returned value is an OSLANG runtime resource, not an arbitrary .NET `FileStream`.

---

# 12. FILE Streams

A file stream provides:

```text
READLINE()
READ()
WRITE(Text)
EOF()
CLOSE()
```

Example:

```osl
Stream = FILE.OPEN("large.txt")

WHILE NOT Stream.EOF()

    Line = Stream.READLINE()

    PRINT Line

END

Stream.CLOSE()
```

The runtime must release open resources even when execution terminates because of an error.

---

# 13. DIR

`DIR` provides controlled directory operations.

Required operations:

```text
EXISTS(Path)
CREATE(Path)
DELETE(Path)
LIST(Path)
FILES(Path)
DIRS(Path)
CURRENT()
CHANGE(Path)
RENAME(Source, Destination)
COPY(Source, Destination)
```

## EXISTS

```osl
DIR.EXISTS("data")
```

## CREATE

```osl
DIR.CREATE("data")
```

Missing parent directories may be created.

## DELETE

```osl
DIR.DELETE("data")
```

Deleting a non-empty directory fails by default.

Recursive deletion may be requested:

```osl
DIR.DELETE("data", TRUE)
```

## LIST

Returns an array of path strings:

```osl
Entries = DIR.LIST("data")
```

Example:

```text
["data/a.txt", "data/b.txt", "data/subdir"]
```

## FILES

Returns files only.

```osl
Files = DIR.FILES("data")
```

## DIRS

Returns subdirectories only.

```osl
Directories = DIR.DIRS("data")
```

## CURRENT

Returns the current working directory.

## CHANGE

Changes the OSLANG execution environment's current working directory.

```osl
DIR.CHANGE("/tmp")
```

## RENAME

Renames a directory.

## COPY

Copies a directory recursively.

---

# 14. FILE/DIR Errors

Filesystem failures integrate with existing `TRY/CATCH`:

```osl
TRY

    Content = FILE.READ("missing.txt")

CATCH

    PRINT ERR

END
```

Errors should identify the operation and path whenever possible.

---

# 15. Filesystem Security

The runtime must not accidentally expose unrestricted host filesystem access.

The host application should be able to configure a filesystem policy, including concepts such as:

```text
AllowedPaths
ReadAllowed
WriteAllowed
DeleteAllowed
DirectoryChangeAllowed
```

A host may run OSLANG inside a restricted root directory.

The exact host configuration API is outside the OSL syntax.

---

# 16. Paths and Encoding

Relative and absolute paths are supported.

```osl
FILE.READ("config.txt")
FILE.READ("./config/config.txt")
```

The runtime handles normalization according to the host platform.

Text operations use UTF-8 by default.

Binary APIs are outside 0.4 and may be introduced later.

---

# 17. NULL

Primitive methods invoked on `NULL` produce a runtime null-reference error.

```osl
Name = NULL
Name.TOUPPER()
```

is invalid.

`NULL` remains falsy and remains the only value equal to `NULL`.

---

# 18. Method Chaining

Methods can be chained when return types support it.

```osl
Result = "  ygor lazaro  ".TRIM().TOUPPER()
```

Another example:

```osl
Result = "red,green,blue".SPLIT(",").MAP(TOUPPER)
```

---

# 19. Mutation Rules

Transformational operations return new values:

```text
TOUPPER
TOLOWER
TRIM
SUBSTR
REPLACE
SPLIT
REVERSE
MAP
FILTER
SORT
```

Explicit array mutation operations may modify the array:

```text
ADD
REMOVE
CLEAR
```

Strings and primitive values are not mutated in-place.

---

# 20. Compatibility

OSLANG 0.4 preserves OSLANG 0.3 except for the intentional mathematics breaking change.

Existing features remain:

- variables;
- dynamic typing with stable variable types;
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
- short-circuit evaluation;
- classes;
- inheritance;
- interfaces;
- properties;
- methods;
- constructors;
- `PUBLIC`;
- `PROTECTED`;
- `PRIVATE`;
- `ME`;
- `NEW`;
- method overriding.

---

# 21. Out of Scope

The following remain outside OSLANG 0.4:

```text
GOTO
GOSUB

multiple class inheritance
abstract classes
static classes
generics
templates
namespaces
modules
IMPORT
anonymous functions
closures
```

The callback API intentionally uses named functions:

```osl
FUNCTION DOUBLE(Value)
    RETURN Value * 2
END

Numbers.MAP(DOUBLE)
```

Lambda syntax and closures may be considered in a future version.

---

# 22. Implementation Architecture

The implementation should extend the existing runtime rather than adding parser special cases.

Recommended conceptual architecture:

```text
OSLANG Runtime
│
├── Primitive Method Dispatcher
│   ├── STRING
│   ├── NUMBER
│   ├── BOOLEAN
│   └── ARRAY
│
├── Standard Library Namespaces
│   ├── MATH
│   ├── FILE
│   └── DIR
│
└── Runtime Resources
    └── FileStream
```

`MATH`, `FILE`, and `DIR` should be implemented as standard-library capabilities.

---

# 23. Required Tests

Tests must cover every required primitive method, array operation, mathematical operation, file operation, directory operation, stream operation, error case, and security-policy behavior.

Regression tests must continue to pass for all previous OSLANG functionality.

The old global mathematical API must specifically be tested as removed:

```osl
SQRT(25)
ABS(-10)
POW(2, 3)
```

must no longer be accepted.

The new forms must work:

```osl
MATH.SQRT(25)
MATH.ABS(-10)
MATH.POW(2, 3)
```

---

# 24. Design Principle

OSLANG 0.4 should make common programming tasks concise without making the language large or complicated.

Primitive values should know how to work with themselves:

```osl
Text.TOUPPER()
Text.SPLIT(",")
Value.TOSTRING()
Numbers.COUNT()
```

Collections should provide useful operations:

```osl
Numbers.MAP(DOUBLE)
Numbers.FILTER(EVEN)
Numbers.ANY(BIG)
Numbers.ALL(POSITIVE)
Numbers.REDUCE(SUM, 0)
```

Mathematics should have a namespace:

```osl
MATH.SQRT()
MATH.POW()
MATH.RANDOM()
```

Files should have a namespace:

```osl
FILE.READ()
FILE.WRITE()
FILE.DELETE()
```

Directories should have a namespace:

```osl
DIR.LIST()
DIR.CREATE()
DIR.DELETE()
```

OSLANG should remain recognizable as a simple BASIC-inspired language while becoming practical enough for real OSB scripting.
