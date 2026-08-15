# Namespaces

OSLANG 0.4 introduces namespaces for organizing built-in functionality. Namespaces are accessed using dot notation.

## MATH Namespace

The `MATH` namespace provides mathematical functions and constants.

```osl
PRINT MATH.SQRT(25)      ' 5
PRINT MATH.ABS(-10)      ' 10
PRINT MATH.POW(2, 8)     ' 256
PRINT MATH.PI            ' 3.14159...
PRINT MATH.RANDOM(1, 100) ' random integer 1-100
```

Available functions: `SQRT`, `ABS`, `POW`, `FLOOR`, `CEIL`, `MIN`, `MAX`, `CLAMP`, `SIGN`, `ROUND`, `TRUNC`, `MOD`, `RANDOM`, `SIN`, `COS`, `TAN`, `LOG`, `EXP`.

Constants: `MATH.PI`, `MATH.E`.

## FILE Namespace

The `FILE` namespace provides file system operations.

```osl
IF FILE.EXISTS("data.txt") THEN
    Lines = FILE.READ("data.txt")
    Text = FILE.READTEXT("data.txt")
    FILE.WRITE("out.txt", "Hello")
    FILE.APPEND("log.txt", "msg")
    FILE.CREATE("new.txt")
    FILE.DELETE("old.txt")
    FILE.COPY("a.txt", "b.txt")
    FILE.MOVE("a.txt", "b.txt")
    Size = FILE.SIZE("data.txt")
    Ext = FILE.EXTENSION("doc.txt")
    Name = FILE.NAME("C:\\dir\\file.txt")
    Dir = FILE.DIR("C:\\dir\\file.txt")
END IF
```

## DIR Namespace

The `DIR` namespace provides directory operations.

```osl
IF DIR.EXISTS("mydir") THEN
    DIR.CREATE("newdir")
    DIR.DELETE("emptydir")
    All = DIR.LIST(".")
    Files = DIR.FILES(".")
    Dirs = DIR.DIRS(".")
    Current = DIR.CURRENT()
    DIR.CHANGE("mydir")
    DIR.RENAME("old", "new")
    DIR.COPY("src", "dst")
END IF
```

## DATE Namespace

The `DATE` namespace provides date values and operations.

```osl
Today = DATE.NOW()
Birthday = DATE.NEW(1986, 4, 12)
Epoch = DATE.FROMNUMBER(0)

Year = Today.YEAR()
Month = Today.MONTH()
Day = Today.DAY()
Weekday = Today.DAYOFWEEK()
Formatted = Today.FORMAT("DD/MM/YYYY")
```

## TIME Namespace

The `TIME` namespace provides time-of-day values and operations.

```osl
Now = TIME.NOW()
Alarm = TIME.NEW(8, 30, 0)
Midnight = TIME.MIDNIGHT()
FromSeconds = TIME.FROMNUMBER(3600)

Hour = Now.HOUR()
Minute = Now.MINUTE()
Second = Now.SECOND()
Formatted = Now.FORMAT("HH:mm:ss")
```

## Primitive Methods

OSLANG 0.4 adds methods directly on primitive values.

### String Methods

```osl
Name = "Ygor"
Upper = Name.TOUPPER()        ' "YGOR"
Lower = Name.TOLOWER()        ' "ygor"
Trimmed = "  hi  ".TRIM()     ' "hi"
Len = Name.LENGTH()           ' 4
Sub = Name.SUBSTR(0, 2)       ' "Yg"
Has = Name.CONTAINS("go")     ' TRUE
Idx = Name.INDEXOF("go")      ' 2
Rev = Name.REVERSE()          ' "rogY"
Norm = "Açúcar".NORMALIZE()   ' "ACUCAR"
```

### Array Methods

```osl
Numbers = [3, 1, 2]
Count = Numbers.COUNT()       ' 3
First = Numbers.FIRST()       ' 3
Last = Numbers.LAST()         ' 2
Sorted = Numbers.SORT()       ' [1, 2, 3]
Str = Numbers.JOIN(", ")      ' "3, 1, 2"
```

### Callback Support

Array methods `MAP`, `FILTER`, `ANY`, `SOME`, `ALL`, and `REDUCE` accept function references:

```osl
FUNCTION IS_EVEN(x)
    RETURN x MOD 2 = 0
END FUNCTION

Even = [1, 2, 3, 4].FILTER(IS_EVEN)  ' [2, 4]
```

## Related Topics

- [Built-in Functions Reference](/src/docs/oslang/reference/built-ins/index.md)
- [Functions](/src/docs/oslang/guide/functions.md)
- [Variables and Types](/src/docs/oslang/guide/variables-and-types.md)
