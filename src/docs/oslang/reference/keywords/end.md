# END

Ends a block.

## Syntax

```osl
END
END FUNCTION
END CLASS
END IF
END WHILE
END FOR
END TRY
END SWITCH
END ON
```

## Description

`END` closes a block started by a corresponding keyword. Some block structures support an optional extended form:

- `END FUNCTION` — optional after a function body.
- `END CLASS` — optional after a class body.
- `END IF` — optional after an `IF` block.
- `END CONSTRUCTOR` — optional after a constructor body.

## Example

```osl
IF x > 0 THEN
    PRINT "Positive"
END
```

```osl
CLASS Person
    VAR Name STRING
END CLASS
```

## Related

- [IF](/src/docs/oslang/reference/keywords/if.md)
- [FOR](/src/docs/oslang/reference/keywords/for.md)
- [WHILE](/src/docs/oslang/reference/keywords/while.md)
- [FUNCTION](/src/docs/oslang/reference/keywords/function.md)
- [CLASS](/src/docs/oslang/reference/keywords/class.md)
- [TRY](/src/docs/oslang/reference/keywords/try.md)
