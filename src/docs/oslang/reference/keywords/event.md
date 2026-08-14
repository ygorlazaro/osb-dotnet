# EVENT

Declares an event inside a class.

## Syntax

```osl
EVENT EventName([parameters])
```

## Description

`EVENT` declares an event that can be raised from within the class and handled externally using `ON`.

Events can have parameters to pass data to handlers.

## Example

```osl
CLASS Button
    EVENT CLICKED()

    FUNCTION PRESS()
        RAISE CLICKED()
    END FUNCTION
END CLASS
```

## Related

- [RAISE](/src/docs/oslang/reference/keywords/raise.md)
- [ON](/src/docs/oslang/reference/keywords/on.md)
- [Events](/src/docs/oslang/guide/events.md)
