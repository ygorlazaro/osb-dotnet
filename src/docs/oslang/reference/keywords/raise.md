# RAISE

Raises an event.

## Syntax

```osl
RAISE EventName([arguments])
```

## Description

`RAISE` triggers an event, executing all attached handlers. Events must be declared with the `EVENT` keyword inside a class.

## Example

```osl
CLASS Button
    EVENT CLICKED()

    FUNCTION PRESS()
        RAISE CLICKED()
    END FUNCTION
END CLASS

FUNCTION MAIN()
    VAR btn = NEW Button()
    ON btn.CLICKED
        PRINT "Clicked!"
    END ON
    btn.PRESS()
END FUNCTION
```

## Related

- [EVENT](/src/docs/oslang/reference/keywords/event.md)
- [ON](/src/docs/oslang/reference/keywords/on.md)
- [Events](/src/docs/oslang/guide/events.md)
