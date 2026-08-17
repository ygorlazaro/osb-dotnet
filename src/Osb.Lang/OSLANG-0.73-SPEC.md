# **OSLANG 0.73 Specification**  
**Base:** OSLANG 0.72  
 **Version:** 0.73  
 **Type:** Breaking Change  
 **Primary goal:** Resolve identifier collisions between keyboard enum and common variable names; clarify enum type registration; fix implicit ME assignment shadowing; unify keyboard constant API.
# **1. Overview**  
OSLANG 0.73 corrects architectural issues identified in 0.72 regarding identifier resolution, enum registration, and implicit member assignment.

The main changes are:
-  keyboard enum renamed from KEY to KEYCODE to avoid collision with the most common variable name for keyboard events;   
-  enum types no longer registered as global variables, eliminating silent assignment hijacking;   
-  legacy OSL.CONSOLE keyboard constants unified with the new KEYCODE enum;   
-  implicit ME assignment respects local variable and parameter shadowing;   
-  deterministic identifier resolution order formally specified and enforced.
This version contains **breaking changes** in keyboard handling, enum usage, and assignment semantics.
# **2. ** **KEYCODE** ** Enum**  
OSLANG 0.73 renames the keyboard enum from KEY to KEYCODE.

The reason is fundamental: OSLANG identifier comparison is case-insensitive. The variable name `Key` (commonly used to store keyboard events) and the enum name `KEY` are the same identifier. This caused the variable to shadow the enum type, or vice-versa, depending on registration order.

The new preferred form is:

IF Key.KEY = KEYCODE.ENTER THEN  
    SAVE()  
END

The old form using string comparisons is removed:

IF Key.KEY = "ENTER" THEN  
    ' ERROR: string comparison against enum value is not allowed  
END
# **3. ** **KEYCODE** ** Enum Members**  
The standard keyboard enum is:

ENUM KEYCODE

    UNKNOWN

    ENTER
    ESC
    TAB
    BACKSPACE
    DELETE
    INSERT
    SPACE

    UP
    DOWN
    LEFT
    RIGHT

    HOME
    END
    PAGEUP
    PAGEDOWN

    F1
    F2
    F3
    F4
    F5
    F6
    F7
    F8
    F9
    F10
    F11
    F12

END

The exact numeric values are implementation-defined unless explicitly specified by the runtime. Programs must use the enum members rather than relying on numeric values.
# **4. Keyboard Event**  
OSL.CONSOLE.GETKEY() returns a keyboard event.

Example:

Key = OSL.CONSOLE.GETKEY()

The event provides:

KEY  
CHAR  
CTRL  
ALT  
SHIFT

KEY is of enum type KEYCODE.
# **5. Keyboard Validation**  
Keyboard validation must compare against KEYCODE enum members.

IF Key.KEY = KEYCODE.ESC THEN  
    EXIT()  
END

IF Key.KEY = KEYCODE.UP THEN  
    MOVEUP()  
END
# **6. Enum Values Must Be Returned**  
Keyboard APIs must return enum values instead of strings.

For example:

Key.KEY

returns:

KEYCODE.ENTER

rather than:

"ENTER"

This provides type safety and eliminates magic strings.
# **7. Keyboard Modifiers**  
Modifiers remain separate properties:

Key.CTRL  
Key.ALT  
 Key.SHIFT

Example:

IF Key.CTRL AND Key.KEY = KEYCODE.S THEN  
    SAVE()  
END

KEY represents the physical/logical key.  
CTRL, ALT, SHIFT represent modifiers.
# **8. Printable Characters**  
For printable characters, Key.CHAR contains the character.

Example:

IF Key.CHAR <> NULL THEN  
    INSERT(Key.CHAR)  
END

The KEY field may contain KEYCODE.UNKNOWN for ordinary printable characters when there is no dedicated enum value. This allows the application to distinguish special key from printable character using:

IF Key.CHAR <> NULL THEN  
# **9. Legacy Keyboard Constants**  
OSLANG 0.73 preserves backward compatibility for legacy keyboard constants. OSL.CONSOLE.ESC, OSL.CONSOLE.UP, and similar constants now return KEYCODE enum values instead of strings.

Example:

IF Key.KEY = OSL.CONSOLE.ESC THEN  
    EXIT()  
END

This is equivalent to:

IF Key.KEY = KEYCODE.ESC THEN  
    EXIT()  
END

Legacy constants and KEYCODE enum members are fully interchangeable. The runtime must ensure that comparisons between them return TRUE.
# **10. Enum Type Registration**  
In OSLANG 0.72, enum types were registered as global variables alongside their type definitions. This caused a critical issue: a normal assignment to a variable with the same name as an enum would silently overwrite the enum type binding for the rest of the execution.

OSLANG 0.73 separates enum type registration from variable storage:

-  Enum types are registered in a dedicated enum type registry, not in the global variable table.  
-  Enum types do not participate in normal variable assignment.  
-  Enum types ARE still visible to identifier resolution (section 14), but they cannot be accidentally overwritten by assignment.

This means:

ENUM STATUS  
    READY  
    BUSY  
END

FUNCTION MAIN()  
    GLOBAL Status = "ready"  
    ' Status is now a global STRING variable.  
    ' STATUS.READY is no longer reachable through bare Status,  
    ' but STATUS.READY is still reachable through STATUS or STATUS.READY.  
END

This is standard shadowing behavior: a variable with the same name as an enum type hides the enum type in that scope, but does not destroy it globally.
# **11. Identifier Resolution Order**  
When an identifier is used without explicit scope, OSLANG resolves it using the following order:

1.  Current local scope (local variables and parameters)  
2.  Current class properties (implicit ME)  
3.  Global variables  
4.  Enum types  
5.  Functions  
6.  Classes and interfaces

The first matching identifier wins. Explicit references such as ME.NAME bypass this implicit search.
# **12. Current Local Scope**  
Local variables have the highest resolution priority.

CLASS TEST

    PUBLIC VALUE

    FUNCTION RUN()
        VALUE = 10  
        IF TRUE THEN  
            VALUE = 20  
            PRINT VALUE  
        END  
    END

END CLASS

The VALUE inside the current scope refers to the local variable.
# **13. Parameters**  
Parameters have priority over properties of the current class.

Example:

CLASS PERSON

    PUBLIC NAME

    FUNCTION GREET(NAME)
        PRINT NAME  
    END

END CLASS

Inside GREET, NAME refers to the parameter. The class property can still be explicitly accessed:

PRINT ME.NAME
# **14. Current Class (Implicit ME)**  
If an identifier is not found in the current local scope, OSLANG searches the current class.

CLASS PERSON

    PUBLIC NAME

    FUNCTION GREET()
        PRINT NAME  
    END

END CLASS

NAME resolves to the class property. This is the implicit ME feature introduced in 0.72.

**Important:** In OSLANG 0.73, local variables and parameters ALWAYS take precedence over class properties. This prevents the common pitfall where a parameter or local variable is unintentionally shadowed by a property with the same name.
# **15. Globals**  
If the identifier is not found in the current class, OSLANG searches the global scope.

FUNCTION MAIN()
    GLOBAL AppName = "OSB"
    PRINT AppName  
END
# **16. Enum Types**  
If the identifier is not found in the global scope, OSLANG searches registered enum types.

ENUM COLOR
    RED
    GREEN
    BLUE
END

FUNCTION MAIN()
    PRINT COLOR.RED  
END
# **17. Functions**  
If the identifier is not found in enum types, OSLANG searches registered functions.

FUNCTION MAIN()
    PRINT "Hello"
END
# **18. Classes and Interfaces**  
If the identifier is not found in functions, OSLANG searches registered classes and interfaces.

CLASS PERSON
    PUBLIC NAME
END

FUNCTION MAIN()
    P = NEW PERSON()
    P.NAME = "Alice"
END
# **19. Explicit Scope Still Wins**  
Explicit references bypass implicit resolution.

ME.NAME always refers to the current class.

An inherited member can be accessed through the appropriate inheritance mechanism defined by the class system. The language must not silently reinterpret an explicit ME reference.
# **20. Ambiguity and Shadowing**  
Because the resolution order is deterministic, ordinary name collisions do not produce ambiguous-reference errors.

Example:

CLASS TEST

    PUBLIC NAME

    FUNCTION RUN(NAME)
        PRINT NAME  
    END

END CLASS

Inside RUN, the parameter wins. To access the property explicitly:

PRINT ME.NAME

This is one of the main reasons ME remains available.

**New in 0.73:** Assignment to a bare identifier inside a class method now follows the same shadowing rules as reading. If a local variable or parameter exists with that name, the assignment updates the local/parameter. Only when no local or parameter exists does the assignment fall through to the class property.

Example:

CLASS PERSON

    PUBLIC NAME
    PRIVATE Age

    FUNCTION SETAGE(Age)
        Age = Age + 1  
        ' The parameter Age is incremented, not the property.  
        ' To update the property, use: ME.Age = Age + 1  
    END

    FUNCTION SETNAME(Name)
        Name = Name.TRIM()  
        ' The parameter Name is trimmed, not the property.  
    END

    FUNCTION INIT(Name)
        Name = Name.TRIM()  
        ' Here, Name is a parameter. The property ME.Name is NOT updated.  
    END

END CLASS
# **21. ME Outside an Object Context**  
ME is only valid when an object instance exists.

FUNCTION MAIN()
    PRINT ME.NAME  
END

is invalid unless MAIN is executing as an instance method of a class. The runtime must report an appropriate runtime or compile-time error.
# **22. Enum Combination Operator**  
Enum values may be combined using:

|

This creates a combined enum value.

Example:

Weekend = Weekday.Saturday | Weekday.Sunday

This is intended primarily for flag-style enums.
# **23. Enum Flags**  
Enums may be declared as flag-compatible.

Example:

ENUM PERMISSION

    READ
    WRITE
    EXECUTE

END

A combined value can then be created:

Permissions =
    PERMISSION.READ |
    PERMISSION.WRITE
# **24. Testing Combined Enums**  
A combined enum value can be compared against an enum member using the appropriate enum containment semantics.

For example:

IF Permissions | PERMISSION.READ THEN  
    PRINT "Can read"  
END

However, to avoid ambiguity with boolean expressions, the preferred 0.73 syntax for testing membership is:

IF Permissions.CONTAINS(PERMISSION.READ) THEN  
    PRINT "Can read"  
END

**Important:** The | operator is the combination operator. It does not represent logical OR. Logical OR remains OR.

Example:

IF A OR B THEN

while:

Permissions =
    PERMISSION.READ |
    PERMISSION.WRITE

combines enum values.
# **25. Keyboard Modifier Combinations**  
The enum system should also allow composite keyboard definitions where useful.

For example, the runtime may internally represent:

CTRL + S

as:

CTRL
+
KEYCODE.S

rather than introducing thousands of enum values such as:

CTRL_S  
CTRL_C  
CTRL_V  
...

This keeps KEYCODE focused on keys.
# **26. Recommended KISS Input Handling**  
The KISS editor can therefore use:

Key = OSL.CONSOLE.GETKEY()

IF Key.KEY = KEYCODE.ESC THEN  
    RUNNING = FALSE  
    RETURN  
END

IF Key.CTRL AND Key.KEY = KEYCODE.S THEN  
    SAVE()  
    RETURN  
END

IF Key.KEY = KEYCODE.UP THEN  
    MOVEUP()  
    RETURN  
END

IF Key.KEY = KEYCODE.DOWN THEN  
    MOVEDOWN()  
    RETURN  
END

IF Key.KEY = KEYCODE.LEFT THEN  
    MOVELEFT()  
    RETURN  
END

IF Key.KEY = KEYCODE.RIGHT THEN  
    MOVERIGHT()  
    RETURN  
END

IF Key.KEY = KEYCODE.BACKSPACE THEN  
    BACKSPACE()  
    RETURN  
END

IF Key.KEY = KEYCODE.DELETE THEN  
    DELETE()  
    RETURN  
END

IF Key.CHAR <> NULL THEN  
    INSERT(Key.CHAR)  
END

With implicit ME, this becomes considerably cleaner.
# **27. ME and Method Calls**  
Methods of the current class can be called without ME.

FUNCTION RUN()
    INITIALIZE()
    RENDER()
    SAVE()

END

This is equivalent to:

FUNCTION RUN()
    ME.INITIALIZE()
    ME.RENDER()
    ME.SAVE()

END
# **28. ME and Properties**  
Properties can also omit ME.

CURSORROW = 10  
CURSORCOLUMN = 20

equivalent to:

ME.CURSORROW = 10  
ME.CURSORCOLUMN = 20
# **29. ME and Parent Members**  
Inherited members are also available implicitly.

CLASS BASE
    PUBLIC NAME
END

CLASS CHILD: BASE
    FUNCTION SHOW()
        PRINT NAME  
    END
END CLASS

NAME resolves to:

CHILD  
  ↓  
BASE  
  ↓  
NAME
# **30. Breaking Changes**  
OSLANG 0.73 is explicitly a **breaking release**.

## **30.1 Keyboard enum name**  
Code using KEY.ENTER, KEY.ESC, etc. must be migrated to KEYCODE.ENTER, KEYCODE.ESC.

## **30.2 Keyboard API return type**  
Key.KEY changes from a string-like representation to ENUM KEYCODE. Applications relying on string comparisons must be updated.

## **30.3 Implicit member assignment**  
Assignment to bare identifiers inside class methods now respects local variable and parameter shadowing. Code that relied on implicit ME assignment overriding parameters will need to use explicit ME. references.

## **30.4 Enum type registration**  
Enum types are no longer registered as global variables. Code that assigned to a variable with the same name as an enum type will now create a normal variable shadowing the enum type, rather than silently destroying the enum binding.
# **31. Compatibility Recommendation**  
For applications being migrated from 0.72:

-  ME.X continues to be valid.  
-  OSL.CONSOLE.ESC and similar constants continue to work, but now return KEYCODE enum values.  
-  KEY.* must be renamed to KEYCODE.*.  
-  Bare identifiers inside class methods continue to resolve to class properties, EXCEPT when a local variable or parameter with the same name exists.
# **32. OSL.CONSOLE 0.73**  
The console API remains available:

OSL.CONSOLE.WIDTH()  
OSL.CONSOLE.HEIGHT()  
OSL.CONSOLE.SIZE()

OSL.CONSOLE.RESIZED()

OSL.CONSOLE.SETCURSOR()  
OSL.CONSOLE.GETCURSOR()

OSL.CONSOLE.HIDECURSOR()  
OSL.CONSOLE.SHOWCURSOR()

OSL.CONSOLE.CLEAR()  
OSL.CONSOLE.CLEARLINE()  
OSL.CONSOLE.CLEARAREA()

OSL.CONSOLE.WRITE()  
OSL.CONSOLE.COLOR()  
OSL.CONSOLE.RESETCOLOR()

OSL.CONSOLE.GETKEY()  
OSL.CONSOLE.READKEY()  
OSL.CONSOLE.KEYAVAILABLE()

OSL.CONSOLE.ENTER()  
OSL.CONSOLE.EXIT()

OSL.CONSOLE.ALTERNATE()

OSL.CONSOLE.BEGINFRAME()  
OSL.CONSOLE.ENDFRAME()  
OSL.CONSOLE.FLUSH()

OSL.CONSOLE.BEEP()

The principal changes are:
-  GETKEY() returns a KEYCODE enum value in the KEY property.
-  Legacy constants (ESC, UP, DOWN, etc.) now return KEYCODE enum values.
# **33. Example: Interactive Application**  
USING OSL.CONSOLE

CLASS APP

    PRIVATE RUNNING

    PUBLIC FUNCTION RUN()

        RUNNING = TRUE

        OSL.CONSOLE.ENTER()
        OSL.CONSOLE.ALTERNATE(TRUE)
        OSL.CONSOLE.HIDECURSOR()

        TRY

            WHILE RUNNING

                RENDER()

                Key = OSL.CONSOLE.GETKEY()

                HANDLEKEY(Key)

            END

        CATCH ERR

            OSL.CONSOLE.SHOWCURSOR()
            OSL.CONSOLE.ALTERNATE(FALSE)
            OSL.CONSOLE.EXIT()

            PRINT ERR

            RETURN

        END

        OSL.CONSOLE.SHOWCURSOR()
        OSL.CONSOLE.ALTERNATE(FALSE)
        OSL.CONSOLE.EXIT()

    END

    PRIVATE FUNCTION HANDLEKEY(Key)
        IF Key.KEY = KEYCODE.ESC THEN
            RUNNING = FALSE
            RETURN
        END

        IF Key.CTRL AND Key.KEY = KEYCODE.S THEN
            SAVE()
            RETURN
        END

        IF Key.KEY = KEYCODE.UP THEN
            MOVEUP()
            RETURN
        END

        IF Key.KEY = KEYCODE.DOWN THEN
            MOVEDOWN()
            RETURN
        END

        IF Key.CHAR <> NULL THEN
            INSERT(Key.CHAR)
        END

    END

    PRIVATE FUNCTION RENDER()
        SIZE = OSL.CONSOLE.SIZE()

        OSL.CONSOLE.BEGINFRAME()
        OSL.CONSOLE.CLEAR()

        OSL.CONSOLE.WRITE(
            1,
            1,
            "OSLANG APPLICATION"
        )

        OSL.CONSOLE.WRITE(
            SIZE.HEIGHT,
            1,
            "ESC Exit"
        )

        OSL.CONSOLE.ENDFRAME()

    END

END CLASS
# **34. Final Resolution Model**  
The identifier resolver in OSLANG 0.73 must conceptually follow:

             Identifier  
                  │  
                  ▼  
          Current local scope?  
              │          │  
             YES         NO  
              │           │  
              ▼           ▼  
           RETURN     Parameter?  
                            │  
                      ┌────┴────┐  
                     YES       NO  
                      │         │  
                      ▼         ▼  
                   RETURN   Current class?  
                                 │  
                             ┌────┴────┐  
                            YES       NO  
                             │         │  
                             ▼         ▼  
                          RETURN    Global?  
                                           │  
                                    ┌────┴────┐  
                                   YES       NO  
                                    │         │  
                                    ▼         ▼  
                                 RETURN    Enum type?  
                                                  │  
                                             ┌────┴────┐  
                                            YES       NO  
                                             │         │  
                                             ▼         ▼  
                                          RETURN    Function?  
                                                             │  
                                                         ┌────┴────┐  
                                                         YES       NO  
                                                          │         │  
                                                          ▼         ▼  
                                                       RETURN    Class/Interface?  
                                                                     │  
                                                              ┌────┴────┐  
                                                             YES       NO  
                                                              │         │  
                                                              ▼         ▼  
                                                           RETURN    ERROR

Explicit references such as ME.NAME bypass this implicit search.
# **35. 0.73 Goals**  
The 0.73 release resolves the most critical architectural issues from 0.72 while preserving the ergonomic improvements. The key fixes are:

OSLANG 0.72  
   ↓  
identifier collisions, enum hijacking, assignment shadowing bugs  
   ↓  
OSLANG 0.73  
   ↓  
clean enum model  
   +  
predictable shadowing  
   +  
unified keyboard API  
   ↓  
KISS.OSL

The most important architectural rule remains:
**KISS-specific behavior belongs in KISS.OSL. Generic terminal capabilities belong in ** **OSL.CONSOLE** **.**

This keeps the KISS migration from turning the OSLANG runtime into a collection of editor-specific APIs.
