# Built-in Functions Reference

OSLANG includes the following built-in functions and namespaces. They are available everywhere without importing any module.

## Global Functions

| Function | Description | Example |
|----------|-------------|---------|
| `STR(value)` | Converts a value to a string. | `STR(42)` → `"42"` |
| `NUMBER(value)` | Converts a value to a number. | `NUMBER("3.14")` → `3.14` |
| `BOOL(value)` | Converts a value to a boolean. | `BOOL(0)` → `FALSE` |
| `COUNT(value)` | Returns the number of items in an array or string. | `COUNT("abc")` → `3` |
| `TYPEOF(value)` | Returns the type name as a string. | `TYPEOF(42)` → `"NUMBER"` |
| `ARGS` | Array of command-line argument strings passed to `MAIN(Args)`. | `ARGS[0]` → first argument |

## MATH Namespace

| Function | Description | Example |
|----------|-------------|---------|
| `MATH.SQRT(x)` | Square root. | `MATH.SQRT(16)` → `4` |
| `MATH.ABS(x)` | Absolute value. | `MATH.ABS(-5)` → `5` |
| `MATH.POW(base, exp)` | Power. | `MATH.POW(2, 3)` → `8` |
| `MATH.FLOOR(x)` | Round down. | `MATH.FLOOR(3.7)` → `3` |
| `MATH.CEIL(x)` | Round up. | `MATH.CEIL(3.2)` → `4` |
| `MATH.MIN(a, b)` | Minimum of two numbers. | `MATH.MIN(3, 7)` → `3` |
| `MATH.MAX(a, b)` | Maximum of two numbers. | `MATH.MAX(3, 7)` → `7` |
| `MATH.CLAMP(x, min, max)` | Clamp value to range. | `MATH.CLAMP(15, 0, 10)` → `10` |
| `MATH.SIGN(x)` | Sign of number. | `MATH.SIGN(-5)` → `-1` |
| `MATH.ROUND(x)` | Round to nearest integer. | `MATH.ROUND(3.5)` → `4` |
| `MATH.TRUNC(x)` | Truncate to integer. | `MATH.TRUNC(3.9)` → `3` |
| `MATH.MOD(a, b)` | Modulo. | `MATH.MOD(10, 3)` → `1` |
| `MATH.RANDOM()` | Random double between 0 and 1. | `MATH.RANDOM()` → `0.42` |
| `MATH.RANDOM(min, max)` | Random integer in range. | `MATH.RANDOM(1, 10)` → `7` |
| `MATH.SIN(x)` | Sine. | `MATH.SIN(0)` → `0` |
| `MATH.COS(x)` | Cosine. | `MATH.COS(0)` → `1` |
| `MATH.TAN(x)` | Tangent. | `MATH.TAN(0)` → `0` |
| `MATH.LOG(x)` | Natural logarithm. | `MATH.LOG(1)` → `0` |
| `MATH.EXP(x)` | e raised to x. | `MATH.EXP(1)` → `2.718` |
| `MATH.PI` | Constant PI. | `MATH.PI` → `3.14159` |
| `MATH.E` | Constant E. | `MATH.E` → `2.718` |

## FILE Namespace

| Function | Description | Example |
|----------|-------------|---------|
| `FILE.EXISTS(path)` | Check if file exists. | `FILE.EXISTS("data.txt")` |
| `FILE.READ(path)` | Read file as array of strings. | `FILE.READ("data.txt")` |
| `FILE.READTEXT(path)` | Read file as single string. | `FILE.READTEXT("data.txt")` |
| `FILE.WRITE(path, text)` | Write text to file. | `FILE.WRITE("out.txt", "hello")` |
| `FILE.APPEND(path, text)` | Append text to file. | `FILE.APPEND("log.txt", "msg")` |
| `FILE.CREATE(path)` | Create empty file. | `FILE.CREATE("new.txt")` |
| `FILE.DELETE(path)` | Delete file. | `FILE.DELETE("old.txt")` |
| `FILE.COPY(src, dst)` | Copy file. | `FILE.COPY("a.txt", "b.txt")` |
| `FILE.MOVE(src, dst)` | Move file. | `FILE.MOVE("a.txt", "b.txt")` |
| `FILE.SIZE(path)` | File size in bytes. | `FILE.SIZE("data.txt")` → `1024` |
| `FILE.EXTENSION(path)` | File extension. | `FILE.EXTENSION("doc.txt")` → `".txt"` |
| `FILE.NAME(path)` | File name. | `FILE.NAME("C:\\dir\\file.txt")` → `"file.txt"` |
| `FILE.DIR(path)` | Directory portion of path. | `FILE.DIR("C:\\dir\\file.txt")` → `"C:\\dir"` |

## DIR Namespace

| Function | Description | Example |
|----------|-------------|---------|
| `DIR.EXISTS(path)` | Check if directory exists. | `DIR.EXISTS("mydir")` |
| `DIR.CREATE(path)` | Create directory. | `DIR.CREATE("newdir")` |
| `DIR.DELETE(path)` | Delete directory. | `DIR.DELETE("olddir")` |
| `DIR.LIST(path)` | List all entries. | `DIR.LIST(".")` |
| `DIR.FILES(path)` | List files only. | `DIR.FILES(".")` |
| `DIR.DIRS(path)` | List directories only. | `DIR.DIRS(".")` |
| `DIR.CURRENT()` | Current directory. | `DIR.CURRENT()` |
| `DIR.CHANGE(path)` | Change current directory. | `DIR.CHANGE("mydir")` |
| `DIR.RENAME(src, dst)` | Rename directory. | `DIR.RENAME("old", "new")` |
| `DIR.COPY(src, dst)` | Copy directory recursively. | `DIR.COPY("src", "dst")` |

## Primitive Methods

OSLANG 0.4 introduces methods on primitive values.

### STRING Methods

| Method | Description | Example |
|--------|-------------|---------|
| `TOUPPER()` | Convert to uppercase. | `"hello".TOUPPER()` → `"HELLO"` |
| `TOLOWER()` | Convert to lowercase. | `"HELLO".TOLOWER()` → `"hello"` |
| `TRIM()` | Remove leading/trailing spaces. | `"  hi  ".TRIM()` → `"hi"` |
| `LENGTH()` | String length. | `"abc".LENGTH()` → `3` |
| `SUBSTR(start, length)` | Extract substring. | `"abcdef".SUBSTR(1, 3)` → `"bcd"` |
| `CONTAINS(text)` | Check if contains text. | `"hello".CONTAINS("ell")` → `TRUE` |
| `INDEXOF(text)` | Position of text (-1 if not found). | `"hello".INDEXOF("ell")` → `1` |
| `STARTSWITH(text)` | Check if starts with text. | `"hello".STARTSWITH("he")` → `TRUE` |
| `ENDSWITH(text)` | Check if ends with text. | `"hello".ENDSWITH("lo")` → `TRUE` |
| `REPLACE(old, new)` | Replace text. | `"hello".REPLACE("ll", "LL")` → `"heLLo"` |
| `SPLIT(sep)` | Split into array. | `"a,b,c".SPLIT(",")` → `["a","b","c"]` |
| `REVERSE()` | Reverse string. | `"abc".REVERSE()` → `"cba"` |
| `ISEMPTY()` | Check if empty. | `"".ISEMPTY()` → `TRUE` |
| `NORMALIZE()` | Normalize accents (remove diacritics). | `"Açúcar".NORMALIZE()` → `"ACUCAR"` |

### NUMBER Methods

| Method | Description | Example |
|--------|-------------|---------|
| `TOSTRING()` | Convert to string. | `42.TOSTRING()` → `"42"` |
| `ABS()` | Absolute value. | `-5.ABS()` → `5` |
| `FLOOR()` | Round down. | `3.7.FLOOR()` → `3` |
| `CEIL()` | Round up. | `3.2.CEIL()` → `4` |
| `ISINTEGER()` | Check if integer. | `3.0.ISINTEGER()` → `TRUE` |
| `BETWEEN(min, max)` | Check if in range. | `5.BETWEEN(1, 10)` → `TRUE` |

### BOOLEAN Methods

| Method | Description | Example |
|--------|-------------|---------|
| `TOSTRING()` | Convert to string. | `TRUE.TOSTRING()` → `"TRUE"` |
| `TOGGLE()` | Invert boolean. | `TRUE.TOGGLE()` → `FALSE` |

### ARRAY Methods

| Method | Description | Example |
|--------|-------------|---------|
| `COUNT()` | Number of elements. | `[1,2,3].COUNT()` → `3` |
| `FIRST()` | First element. | `[1,2,3].FIRST()` → `1` |
| `LAST()` | Last element. | `[1,2,3].LAST()` → `3` |
| `CONTAINS(x)` | Check if contains value. | `[1,2,3].CONTAINS(2)` → `TRUE` |
| `INDEXOF(x)` | Index of value (-1 if not found). | `[1,2,3].INDEXOF(2)` → `1` |
| `ADD(x)` | Append element. | `arr.ADD(4)` |
| `REMOVE(x)` | Remove first occurrence. | `arr.REMOVE(2)` |
| `CLEAR()` | Remove all elements. | `arr.CLEAR()` |
| `REVERSE()` | Reverse order. | `[1,2,3].REVERSE()` → `[3,2,1]` |
| `SORT()` | Sort ascending. | `[3,1,2].SORT()` → `[1,2,3]` |
| `JOIN(sep)` | Join as string. | `[1,2,3].JOIN(",")` → `"1,2,3"` |
| `MAP(fn)` | Apply function to each element. | `[1,2,3].MAP(DOUBLE)` |
| `FILTER(fn)` | Filter by predicate. | `[1,2,3].FILTER(IS_EVEN)` |
| `ANY(fn)` | Any element matches. | `[1,2,3].ANY(IS_EVEN)` → `TRUE` |
| `SOME(fn)` | Alias for ANY. | `[1,2,3].SOME(IS_EVEN)` → `TRUE` |
| `ALL(fn)` | All elements match. | `[1,2,3].ALL(IS_POSITIVE)` → `TRUE` |
| `REDUCE(fn, init)` | Reduce to single value. | `[1,2,3].REDUCE(SUM, 0)` → `6` |

## Detailed Documentation

- [`STR()`](/src/docs/oslang/reference/built-ins/str.md)
- [`NUMBER()`](/src/docs/oslang/reference/built-ins/number.md)
- [`BOOL()`](/src/docs/oslang/reference/built-ins/bool.md)
- [`COUNT()`](/src/docs/oslang/reference/built-ins/count.md)
- [`TYPEOF()`](/src/docs/oslang/reference/built-ins/typeof.md)

## Related Topics

- [Variables and Types](/src/docs/oslang/guide/variables-and-types.md)
- [Operators](/src/docs/oslang/guide/operators.md)
- [Keywords Reference](/src/docs/oslang/reference/keywords/index.md)
