# Events

This guide explains how to declare events, attach handlers, and raise events in OSLANG.

## Declaring an Event

Use the `EVENT` keyword inside a class:

```osl
CLASS Button
    EVENT CLICKED()
END CLASS
```

Events can have parameters:

```osl
CLASS Form
    EVENT SUBMIT(Data STRING)
END CLASS
```

## Raising an Event

Use `RAISE` to trigger an event:

```osl
CLASS Button
    EVENT CLICKED()

    FUNCTION PRESS()
        RAISE CLICKED()
    END FUNCTION
END CLASS
```

With parameters:

```osl
CLASS Form
    EVENT SUBMIT(Data STRING)

    FUNCTION ON_SUBMIT_BUTTON()
        RAISE SUBMIT("user input")
    END FUNCTION
END CLASS
```

## Handling Events

Use `ON` to attach a handler:

```osl
FUNCTION MAIN()
    VAR btn = NEW Button()
    ON btn.CLICKED
        PRINT "Button was clicked!"
    END ON

    btn.PRESS()
END FUNCTION
```

## Complete Example

```osl
CLASS Door
    EVENT OPEN()
    EVENT CLOSE()

    FUNCTION OPEN_DOOR()
        RAISE OPEN()
    END FUNCTION

    FUNCTION CLOSE_DOOR()
        RAISE CLOSE()
    END FUNCTION
END CLASS

FUNCTION MAIN()
    VAR d = NEW Door()

    ON d.OPEN
        PRINT "Door opened"
    END ON

    ON d.CLOSE
        PRINT "Door closed"
    END ON

    d.OPEN_DOOR()
    d.CLOSE_DOOR()
END FUNCTION
```

## Related Topics

- [Classes and Objects](/src/docs/oslang/guide/classes-and-objects.md)
- [Functions](/src/docs/oslang/guide/functions.md)
