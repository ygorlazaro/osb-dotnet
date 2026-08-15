# OSLANG 0.5 Specification

**Language:** OSLANG  
**Version:** 0.5  
**File extension:** `.osl`  
**Paradigm:** Imperative, procedural, and object-oriented  
**Typing:** Dynamic with stable variable types  
**Case sensitivity:** Case-insensitive  
**Entry point:** `MAIN()`

---

## 1. Overview

OSLANG 0.5 introduces native temporal values and expands the standard library with `DATE` and `TIME`.

This version is based on OSLANG 0.41 and retains its `ARGS` support.

The main additions are:

- native `DATE` type;
- native `TIME` type;
- `DATE` namespace;
- `TIME` namespace;
- date/time construction;
- date/time component access;
- date/time formatting;
- conversion between `DATE` and Unix Epoch numbers;
- conversion between `TIME` and seconds since midnight;
- structural `DATETIME` representation;
- temporal comparisons;
- temporal support in `TYPEOF()`.

`DATETIME` is intentionally **not** a third primitive type. A datetime is represented as an array containing exactly two elements:

```text
[DATE, TIME]
```

---

# 2. Type System

OSLANG 0.5 supports:

```text
NULL
STRING
NUMBER
BOOLEAN
DATE
TIME
ARRAY
OBJECT
```

`OBJECT` represents instances of OSLANG classes.

`DATETIME` is a structural convention:

```text
DATETIME = [DATE, TIME]
```

Therefore:

```osl
Moment = [Data, Time]

TYPEOF(Moment)
```

returns:

```text
ARRAY
```

---

# 3. Existing Type Rules

OSLANG is dynamically typed with stable variable types.

Examples:

```osl
Name = "Ygor"
Age = 40
Active = TRUE
```

Once a non-`NULL` type has been established, assigning an incompatible non-`NULL` value is an error.

```osl
Age = 40
Age = "Ygor"
```

is invalid.

`NULL` can be assigned to any variable without changing its established type:

```osl
Age = 40
Age = NULL
Age = 41
```

---

# 4. NULL

`NULL` represents the absence of a value.

`NULL` is falsy.

`NULL` compares equal only to `NULL`.

```osl
IF Value = NULL THEN
    PRINT "No value"
END
```

The following comparisons evaluate to `FALSE`:

```text
NULL = 0
NULL = ""
NULL = FALSE
```

---

# 5. DATE

A `DATE` represents a calendar date only.

It contains:

```text
YEAR
MONTH
DAY
```

It does not contain:

- hour;
- minute;
- second;
- timezone;
- time-of-day information.

Example:

```text
2026-08-15
```

is a `DATE`.

---

# 6. TIME

A `TIME` represents a time of day only.

It contains:

```text
HOUR
MINUTE
SECOND
```

The runtime may preserve millisecond precision.

It does not contain:

- year;
- month;
- day;
- timezone;
- calendar date.

Example:

```text
13:30:45
```

is a `TIME`.

---

# 7. DATE Namespace

`DATE` is both a runtime type and a standard-library namespace.

Required functions:

```text
DATE.NOW()
DATE.NEW(YEAR, MONTH, DAY)
DATE.FROMNUMBER(Value)
```

---

## 7.1 DATE.NOW

Returns the current local date according to the execution environment.

```osl
Today = DATE.NOW()
```

The result contains no time-of-day information.

---

## 7.2 DATE.NEW

Creates a date:

```osl
Birthday = DATE.NEW(1986, 4, 12)
```

Invalid calendar dates must produce a runtime error.

For example:

```osl
DATE.NEW(2026, 2, 31)
```

is invalid.

---

## 7.3 DATE.FROMNUMBER

Creates a `DATE` from a Unix Epoch value.

```osl
Data = DATE.FROMNUMBER(0)
```

The Epoch is:

```text
1970-01-01 00:00:00 UTC
```

The numeric value represents Unix Epoch seconds.

Because `DATE` contains no time-of-day, conversion uses the execution environment's date interpretation.

---

# 8. DATE Methods

Required methods:

```text
YEAR()
MONTH()
DAY()
DAYOFWEEK()
DAYOFYEAR()
MONTHNAME()
DAYNAME()
ISLEAPYEAR()
DAYSINMONTH()
TOSTRING()
FORMAT(Pattern)
TONUMBER()
```

---

## 8.1 YEAR

```osl
Data = DATE.NEW(2026, 8, 15)

PRINT Data.YEAR()
```

Returns:

```text
2026
```

---

## 8.2 MONTH

Returns a number from `1` through `12`.

```osl
Data.MONTH()
```

---

## 8.3 DAY

Returns the day of the month.

```osl
Data.DAY()
```

---

## 8.4 DAYOFWEEK

Returns:

```text
1 = Monday
2 = Tuesday
3 = Wednesday
4 = Thursday
5 = Friday
6 = Saturday
7 = Sunday
```

Example:

```osl
Data.DAYOFWEEK()
```

---

## 8.5 DAYOFYEAR

Returns the ordinal day of the year.

```text
January 1 = 1
```

A leap year may contain day `366`.

---

## 8.6 MONTHNAME

Returns the localized month name.

```osl
Data.MONTHNAME()
```

The language does not require English names. The runtime locale determines the result.

---

## 8.7 DAYNAME

Returns the localized weekday name.

```osl
Data.DAYNAME()
```

---

## 8.8 ISLEAPYEAR

Returns `BOOLEAN`.

```osl
Data = DATE.NEW(2024, 1, 1)

IF Data.ISLEAPYEAR() THEN
    PRINT "Leap year"
END
```

---

## 8.9 DAYSINMONTH

Returns the number of days in the date's month.

```osl
Data = DATE.NEW(2026, 2, 1)

PRINT Data.DAYSINMONTH()
```

Result:

```text
28
```

---

## 8.10 TOSTRING

The canonical representation is:

```text
YYYY-MM-DD
```

Example:

```osl
Data.TOSTRING()
```

Result:

```text
2026-08-15
```

---

## 8.11 FORMAT

Formats a date using an OSLANG pattern.

```osl
Data.FORMAT("DD/MM/YYYY")
```

Result:

```text
15/08/2026
```

Required tokens:

```text
YYYY  four-digit year
MM    two-digit month
DD    two-digit day
```

---

## 8.12 TONUMBER

Converts a `DATE` to Unix Epoch seconds.

```osl
Timestamp = Data.TONUMBER()
```

The conversion is intended to be compatible with:

```osl
DATE.FROMNUMBER(Timestamp)
```

---

# 9. TIME Namespace

`TIME` is both a runtime type and a standard-library namespace.

Required functions:

```text
TIME.NOW()
TIME.NEW(HOUR, MINUTE, SECOND)
TIME.FROMNUMBER(Value)
TIME.MIDNIGHT()
```

---

## 9.1 TIME.NOW

Returns the current local time.

```osl
CurrentTime = TIME.NOW()
```

The result contains no date.

---

## 9.2 TIME.NEW

Creates a time:

```osl
Time = TIME.NEW(13, 30, 45)
```

Result:

```text
13:30:45
```

Valid ranges:

```text
HOUR   0-23
MINUTE 0-59
SECOND 0-59
```

Invalid values produce a runtime error.

---

## 9.3 TIME.FROMNUMBER

Creates a time from seconds since midnight.

```osl
Time = TIME.FROMNUMBER(3600)
```

Result:

```text
01:00:00
```

The standard whole-second range is:

```text
0 <= seconds <= 86399
```

---

## 9.4 TIME.MIDNIGHT

Returns:

```text
00:00:00
```

Example:

```osl
Time = TIME.MIDNIGHT()
```

---

# 10. TIME Methods

Required methods:

```text
HOUR()
MINUTE()
SECOND()
MILLISECOND()
TOSTRING()
FORMAT(Pattern)
TONUMBER()
```

---

## 10.1 HOUR

Returns `0-23`.

```osl
Time.HOUR()
```

---

## 10.2 MINUTE

Returns `0-59`.

```osl
Time.MINUTE()
```

---

## 10.3 SECOND

Returns `0-59`.

```osl
Time.SECOND()
```

---

## 10.4 MILLISECOND

Returns the millisecond component.

If the runtime is operating with whole-second precision, this returns:

```text
0
```

---

## 10.5 TOSTRING

Canonical representation:

```text
HH:mm:ss
```

Example:

```osl
Time.TOSTRING()
```

Result:

```text
13:30:45
```

---

## 10.6 FORMAT

Required tokens:

```text
HH    two-digit hour
mm    two-digit minute
ss    two-digit second
```

Example:

```osl
Time.FORMAT("HH:mm:ss")
```

---

## 10.7 TONUMBER

Converts a `TIME` to seconds since midnight.

```osl
Time = TIME.NEW(1, 30, 0)

Seconds = Time.TONUMBER()
```

Result:

```text
5400
```

---

# 11. Temporal Numeric Conversion

## DATE

```osl
Number = Data.TONUMBER()
Data = DATE.FROMNUMBER(Number)
```

Representation:

```text
Unix Epoch seconds
```

## TIME

```osl
Number = Time.TONUMBER()
Time = TIME.FROMNUMBER(Number)
```

Representation:

```text
seconds since midnight
```

There is no implicit conversion between `DATE` and `TIME`.

---

# 12. DATETIME

OSLANG 0.5 does not introduce a native `DATETIME` primitive.

Instead:

```text
DATETIME = [DATE, TIME]
```

Example:

```osl
Data = DATE.NEW(2026, 8, 15)
Time = TIME.NEW(13, 45, 30)

Moment = [Data, Time]
```

The structure is:

```text
Moment[0] -> DATE
Moment[1] -> TIME
```

---

# 13. DATETIME Type Semantics

A valid structural `DATETIME` has exactly two elements:

```text
Index 0: DATE
Index 1: TIME
```

However:

```osl
TYPEOF(Moment)
```

returns:

```text
ARRAY
```

This is intentional.

---

# 14. DATETIME Construction

The recommended form is:

```osl
Moment = [
    DATE.NEW(2026, 8, 15),
    TIME.NEW(13, 45, 30)
]
```

No `DATETIME.NEW()` function is required in 0.5.

Current date/time can be combined with:

```osl
Moment = [
    DATE.NOW(),
    TIME.NOW()
]
```

---

# 15. DATETIME Extraction

Because `DATETIME` is an array:

```osl
Data = Moment[0]
Time = Moment[1]
```

Normal array functionality applies:

```osl
Moment.COUNT()
```

returns:

```text
2
```

---

# 16. DATE and TIME Comparisons

`DATE` values support:

```text
=
<>
<
>
<=
>=
```

Ordering is chronological.

Example:

```osl
A = DATE.NEW(2026, 1, 1)
B = DATE.NEW(2026, 12, 31)

IF A < B THEN
    PRINT "A comes first"
END
```

`TIME` values support the same comparison operators.

Ordering is chronological within a single day.

```osl
Morning = TIME.NEW(8, 0, 0)
Evening = TIME.NEW(18, 0, 0)

IF Morning < Evening THEN
    PRINT "Morning comes first"
END
```

---

# 17. Temporal Arithmetic

OSLANG 0.5 does not define implicit date/time arithmetic.

Therefore:

```osl
Data + 1
Time + 60
```

do not automatically mean:

```text
tomorrow
one minute later
```

Explicit temporal arithmetic is reserved for a future version.

Potential future methods include:

```text
ADDDAYS()
ADDMONTHS()
ADDYEARS()
ADDSECONDS()
ADDMINUTES()
ADDHOURS()
```

---

# 18. Locale and Time Zone

`DATE` and `TIME` are timezone-independent values.

`DATE` contains no timezone.

`TIME` contains no timezone.

`DATE.NOW()` and `TIME.NOW()` use the local environment of the OSLANG runtime.

Localized methods such as:

```text
MONTHNAME()
DAYNAME()
```

use the runtime's configured locale.

Explicit timezone support is outside OSLANG 0.5.

---

# 19. ARGS

OSLANG 0.41 introduced `ARGS`, retained in 0.5.

`ARGS` is an array of command-line argument strings.

Example:

```osl
FUNCTION MAIN()

    PRINT ARGS.COUNT()

    IF ARGS.COUNT() > 0 THEN
        PRINT ARGS[0]
    END

END FUNCTION
```

`ARGS` is read-only from OSLANG code.

---

# 20. Primitive Methods

OSLANG 0.5 retains the primitive methods from 0.4.

## STRING

```text
TOUPPER()
TOLOWER()
TRIM()
STARTSWITH(Value)
ENDSWITH(Value)
CONTAINS(Value)
INDEXOF(Value)
SUBSTR(Start, Length)
REPLACE(Old, New)
SPLIT(Separator)
COUNT()
ISEMPTY()
REVERSE()
TOSTRING()
```

## NUMBER

```text
TOSTRING()
ABS()
FLOOR()
CEIL()
ISINTEGER()
BETWEEN(Min, Max)
```

## BOOLEAN

```text
TOSTRING()
TOGGLE()
```

## ARRAY

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

---

# 21. Functional Array Operations

Named functions may be passed as callbacks.

Example:

```osl
FUNCTION DOUBLE(Value)
    RETURN Value * 2
END

Numbers = [1, 2, 3, 4]

Result = Numbers.MAP(DOUBLE)
```

`MAP`, `FILTER`, `ANY`, `SOME`, `ALL`, and `REDUCE` use named functions.

Lambda expressions and closures remain outside 0.5.

`ANY` and `SOME` short-circuit on the first `TRUE`.

`ALL` short-circuits on the first `FALSE`.

For an empty array:

```text
MAP    -> []
FILTER -> []
ANY    -> FALSE
SOME   -> FALSE
ALL    -> TRUE
```

`REDUCE` returns its initial value for an empty array.

---

# 22. MATH

Mathematics is exposed through the `MATH` namespace.

Required functions:

```text
MATH.SQRT(Value)
MATH.ABS(Value)
MATH.POW(Value, Exponent)
MATH.FLOOR(Value)
MATH.CEIL(Value)
MATH.RANDOM()
MATH.RANDOM(Min, Max)
MATH.MIN(A, B)
MATH.MAX(A, B)
MATH.CLAMP(Value, Min, Max)
MATH.SIGN(Value)
MATH.ROUND(Value)
MATH.ROUND(Value, Digits)
MATH.TRUNC(Value)
MATH.MOD(A, B)
MATH.SIN(Value)
MATH.COS(Value)
MATH.TAN(Value)
MATH.ASIN(Value)
MATH.ACOS(Value)
MATH.ATAN(Value)
MATH.ATAN2(Y, X)
MATH.LOG(Value)
MATH.LOG10(Value)
MATH.EXP(Value)
```

Constants:

```text
MATH.PI
MATH.E
```

The previous global mathematical functions are not part of 0.5.

---

# 23. FILE

The filesystem API introduced in 0.4 remains available.

```text
FILE.EXISTS(Path)
FILE.READ(Path)
FILE.READTEXT(Path)
FILE.WRITE(Path, Text)
FILE.APPEND(Path, Text)
FILE.CREATE(Path)
FILE.DELETE(Path)
FILE.DEL(Path)
FILE.COPY(Source, Destination)
FILE.MOVE(Source, Destination)
FILE.SIZE(Path)
FILE.EXTENSION(Path)
FILE.NAME(Path)
FILE.DIR(Path)
FILE.OPEN(Path)
```

`FILE.READ()` returns an array of text lines.

`FILE.READTEXT()` returns the entire file as a string.

Default text encoding is UTF-8.

---

# 24. FILE Streams

File streams provide:

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

The host runtime is responsible for releasing resources if execution terminates due to an error.

---

# 25. DIR

The directory API remains:

```text
DIR.EXISTS(Path)
DIR.CREATE(Path)
DIR.DELETE(Path)
DIR.LIST(Path)
DIR.FILES(Path)
DIR.DIRS(Path)
DIR.CURRENT()
DIR.CHANGE(Path)
DIR.RENAME(Source, Destination)
DIR.COPY(Source, Destination)
```

---

# 26. Filesystem Security

`FILE` and `DIR` are host-controlled capabilities.

The host application must be able to restrict access.

Possible host-level policies include:

```text
AllowedPaths
ReadAllowed
WriteAllowed
DeleteAllowed
DirectoryChangeAllowed
```

The OSLANG syntax does not expose the host policy configuration.

---

# 27. TRY/CATCH

Temporal and filesystem failures integrate with the existing `TRY/CATCH` syntax.

The catch variable is:

```text
ERR
```

Example:

```osl
TRY

    Data = DATE.NEW(2026, 2, 31)

CATCH

    PRINT ERR

END
```

---

# 28. TYPEOF

`TYPEOF()` recognizes:

```text
NULL
STRING
NUMBER
BOOLEAN
DATE
TIME
ARRAY
OBJECT
```

Examples:

```osl
TYPEOF(DATE.NOW())
```

returns:

```text
DATE
```

```osl
TYPEOF(TIME.NOW())
```

returns:

```text
TIME
```

```osl
TYPEOF([DATE.NOW(), TIME.NOW()])
```

returns:

```text
ARRAY
```

---

# 29. NULL and Temporal Values

Temporal variables may contain `NULL`.

Example:

```osl
Data = DATE.NOW()

Data = NULL

Data = DATE.NEW(2026, 8, 15)
```

This is valid.

Calling a temporal method on `NULL` produces a runtime null-reference error:

```osl
Data = NULL

Data.YEAR()
```

---

# 30. Method Chaining

Temporal values participate in normal member access and method chaining.

Example:

```osl
Result = DATE.NOW().FORMAT("DD/MM/YYYY")
```

```osl
Result = TIME.NOW().FORMAT("HH:mm:ss")
```

---

# 31. Complete Temporal Example

```osl
FUNCTION MAIN()

    Today = DATE.NOW()
    CurrentTime = TIME.NOW()

    PRINT "Date: " + Today.FORMAT("DD/MM/YYYY")
    PRINT "Time: " + CurrentTime.FORMAT("HH:mm:ss")

    PRINT "Year: " + Today.YEAR()
    PRINT "Month: " + Today.MONTH()
    PRINT "Day: " + Today.DAY()

    PRINT "Hour: " + CurrentTime.HOUR()
    PRINT "Minute: " + CurrentTime.MINUTE()
    PRINT "Second: " + CurrentTime.SECOND()

END FUNCTION
```

---

# 32. Complete DATETIME Example

```osl
FUNCTION MAIN()

    Data = DATE.NOW()
    Time = TIME.NOW()

    Moment = [Data, Time]

    PRINT "Parts: " + Moment.COUNT()

    PRINT "Date: " + Moment[0].TOSTRING()
    PRINT "Time: " + Moment[1].TOSTRING()

END FUNCTION
```

---

# 33. Numeric Conversion Example

```osl
FUNCTION MAIN()

    Data = DATE.NEW(1970, 1, 1)

    Epoch = Data.TONUMBER()

    PRINT Epoch

    Time = TIME.NEW(1, 30, 0)

    Seconds = Time.TONUMBER()

    PRINT Seconds

END FUNCTION
```

Conceptual output:

```text
0
5400
```

---

# 34. OSLANG 0.5 Standard Namespaces

The standard namespaces are now:

```text
MATH
FILE
DIR
DATE
TIME
```

The distinction between namespaces and primitive types is intentional.

For example:

```osl
Today = DATE.NOW()
```

uses `DATE` as a namespace.

After evaluation, `Today` contains a native `DATE` value.

That value then exposes methods:

```osl
Today.YEAR()
Today.MONTH()
Today.FORMAT("DD/MM/YYYY")
```

---

# 35. Out of Scope

The following remain outside OSLANG 0.5:

```text
GOTO
GOSUB
multiple class inheritance
abstract classes
static classes
generics
templates
modules
IMPORT
lambda expressions
closures
timezone objects
UTC conversion APIs
duration types
calendar periods
date arithmetic
time arithmetic
datetime arithmetic
binary file APIs
filesystem watchers
file permission APIs
```

These may be considered for future versions.

---

# 36. Required Tests

The OSLANG 0.5 implementation must test:

## DATE

```text
DATE.NOW()
DATE.NEW()
DATE.FROMNUMBER()
YEAR()
MONTH()
DAY()
DAYOFWEEK()
DAYOFYEAR()
MONTHNAME()
DAYNAME()
ISLEAPYEAR()
DAYSINMONTH()
TOSTRING()
FORMAT()
TONUMBER()
```

## TIME

```text
TIME.NOW()
TIME.NEW()
TIME.FROMNUMBER()
TIME.MIDNIGHT()
HOUR()
MINUTE()
SECOND()
MILLISECOND()
TOSTRING()
FORMAT()
TONUMBER()
```

## DATETIME

```text
[DATE, TIME]
index 0
index 1
COUNT()
TYPEOF()
```

## Temporal conversion

```text
DATE -> NUMBER
NUMBER -> DATE
TIME -> NUMBER
NUMBER -> TIME
```

## Validation

Tests must cover:

- invalid dates;
- invalid times;
- leap years;
- month lengths;
- invalid Epoch conversions;
- invalid time ranges;
- temporal `NULL` behavior;
- temporal comparisons;
- `TYPEOF`;
- formatting;
- locale behavior where applicable.

## Regression

All OSLANG 0.41 tests must continue to pass except where explicitly superseded by this specification.

---

# 37. Version Compatibility

OSLANG 0.5 is based on OSLANG 0.41.

Retained features include:

- variables;
- dynamic typing with stable variable types;
- `NULL`;
- arrays;
- functions;
- `MAIN`;
- `ARGS`;
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
- method overriding;
- primitive methods;
- `MATH`;
- `FILE`;
- `DIR`.

---

# 38. Design Principles

OSLANG 0.5 follows these principles:

1. **Keep the core language small.**
2. **Prefer useful methods over large numbers of keywords.**
3. **Keep common operations discoverable through namespaces.**
4. **Treat dates and times as values.**
5. **Avoid introducing a special `DATETIME` object when an existing `ARRAY` can express the relationship.**
6. **Keep filesystem functionality under explicit namespaces.**
7. **Preserve the BASIC-inspired readability of the language.**
8. **Avoid implicit conversions that hide errors.**
9. **Prefer explicit temporal operations.**
10. **Remain suitable as the scripting language of OSB.**

The fundamental temporal model is:

```text
DATE = calendar date
TIME = time of day
DATETIME = [DATE, TIME]
```

This deliberately simple model leaves room for future temporal features without forcing unnecessary complexity into the OSLANG 0.5 core.
