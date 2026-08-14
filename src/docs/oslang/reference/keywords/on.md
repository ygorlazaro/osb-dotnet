# ON

Attaches an event handler.

## Syntax

```osl
ON instance.EventName
    ' handler statements
END ON
```

## Description

`ON` registers a block of code to execute when the specified event is raised on the given instance.

## Example

```osl
FUNCTION MAIN()
    VAR btn = NEW Button()

    ON btn.CLICKED
        PRINT "Button clicked!"
    END ON

    btn.PRESS()
END FUNCTION
```

## Related

- [EVENT](/src/docs/oslang/reference/keywords/event.md)
- [RAISE](/src/docs/oslang/reference/keywords/raise.md)
- [Events](/src/docs/oslang/guide/events.md)
