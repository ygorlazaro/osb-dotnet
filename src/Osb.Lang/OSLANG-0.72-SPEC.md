# **OSLANG 0.72 Specification**  
**Base:** OSLANG 0.7  
 **Version:** 0.72  
 **Type:** Breaking Change  
 **Primary goal:** Improve object-oriented programming, terminal applications and KISS migration.  
# **1. Overview**  
OSLANG 0.72 extends the object-oriented and terminal programming capabilities introduced in previous versions.  
The main additions are:  
-  implicit ME resolution;   
- deterministic identifier resolution order;  
- keyboard constants represented by an enum;  
- enum values returned by keyboard validation;  
-  enum flags using |;   
- improved terminal API semantics;  
- improved suitability for full-screen interactive applications such as KISS.  
This version contains a **breaking change** in keyboard handling and identifier resolution.  
# **2. ** **ME** ** Implicit Resolution**  
ME represents the current object instance.  
It remains valid explicitly:  
ME.NAME  
ME.SAVE()  
ME.CURSORROW  
However, ME may now be omitted when there is no ambiguity.  
Therefore:  
ME.NAME  
can be written as:  
NAME  
and:  
ME.SAVE()  
can be written as:  
SAVE()  
# **3. When ** **ME** ** Is Optional**  
ME is optional whenever the identifier can be resolved unambiguously.  
Example:  
CLASS PERSON  
   
    PUBLIC NAME  
   
    PUBLIC FUNCTION GREET()  
   
        PRINT "Hello " + NAME  
   
    END  
   
END CLASS  
This is equivalent to:  
CLASS PERSON  
   
    PUBLIC NAME  
   
    PUBLIC FUNCTION GREET()  
   
        PRINT "Hello " + ME.NAME  
   
    END  
   
END CLASS  
# **4. Explicit ** **ME**  
ME can always be used to make the intended scope explicit.  
ME.NAME  
ME.SAVE()  
ME.CURSORROW  
This is useful when readability is preferred or when an identifier exists in another scope.  
# **5. Identifier Resolution Order**  
When an identifier is used without an explicit scope, OSLANG resolves it using the following order:  
1. Current scope  
2. Function parameters  
3. Current class  
4. Parent class  
The first matching identifier wins.  
# **6. Current Scope**  
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
# **7. Parameters**  
Parameters have priority over members of the current class.  
Example:  
CLASS PERSON  
   
    PUBLIC NAME  
   
    FUNCTION GREET(NAME)  
   
        PRINT NAME  
   
    END  
   
END CLASS  
Inside GREET, NAME refers to the parameter.  
The class property can still be explicitly accessed:  
PRINT ME.NAME  
# **8. Current Class**  
If an identifier is not found in the current scope or parameters, OSLANG searches the current class.  
CLASS PERSON  
   
    PUBLIC NAME  
   
    FUNCTION GREET()  
   
        PRINT NAME  
   
    END  
   
END CLASS  
NAME resolves to the class property.  
# **9. Parent Class**  
If the identifier is not found in the current class, OSLANG searches the inherited parent class.  
CLASS ENTITY  
   
    PUBLIC ID  
   
END CLASS  
   
   
CLASS PERSON: ENTITY  
   
    PUBLIC NAME  
   
    FUNCTION SHOW()  
   
        PRINT NAME  
        PRINT ID  
   
    END  
   
END CLASS  
NAME resolves to PERSON.NAME.  
ID resolves to ENTITY.ID.  
# **10. Resolution Example**  
Consider:  
CLASS ENTITY  
   
    PUBLIC VALUE  
   
END CLASS  
   
   
CLASS TEST: ENTITY  
   
    PUBLIC VALUE  
   
    FUNCTION RUN(VALUE)  
   
        VAR VALUE  
   
        PRINT VALUE  
   
    END  
   
END CLASS  
The conceptual resolution is:  
local VALUE  
    ↓  
parameter VALUE  
    ↓  
TEST.VALUE  
    ↓  
ENTITY.VALUE  
The first valid scope is selected.  
# **11. Explicit Scope Still Wins**  
Explicit references bypass implicit resolution.  
ME.VALUE  
always refers to the current class.  
An inherited member can be accessed through the appropriate inheritance mechanism defined by the class system.  
The language must not silently reinterpret an explicit ME reference.  
# **12. Ambiguity**  
Because the resolution order is deterministic, ordinary name collisions do not produce ambiguous-reference errors.  
For example:  
CLASS TEST  
   
    PUBLIC NAME  
   
    FUNCTION RUN(NAME)  
   
        PRINT NAME  
   
    END  
   
END CLASS  
The parameter wins.  
To access the property:  
PRINT ME.NAME  
This is one of the main reasons ME remains available.  
# **13. ** **ME** ** Outside an Object Context**  
ME is only valid when an object instance exists.  
Therefore this is invalid:  
FUNCTION MAIN()  
   
    PRINT ME.NAME  
   
END  
unless MAIN is executing as an instance method of a class.  
The runtime must report an appropriate runtime or compile-time error.  
# **14. Keyboard API**  
OSLANG 0.72 changes the representation of keyboard keys.  
Keyboard keys are now represented by an enum.  
Instead of:  
IF Key.KEY = "ENTER" THEN  
the preferred form is:  
IF Key.KEY = KEY.ENTER THEN  
# **15. ** **KEY** ** Enum**  
The standard keyboard enum is:  
ENUM KEY  
   
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
The exact numeric values are implementation-defined unless explicitly specified by the runtime.  
Programs must use the enum members rather than relying on numeric values.  
# **16. Keyboard Event**  
OSL.CONSOLE.GETKEY() returns a keyboard event.  
Example:  
Key = OSL.CONSOLE.GETKEY()  
The event provides:  
KEY  
CHAR  
CTRL  
ALT  
SHIFT  
KEY is of enum type KEY.  
# **17. Keyboard Validation**  
Keyboard validation must compare against enum members.  
IF Key.KEY = KEY.ENTER THEN  
    SAVE()  
END  
IF Key.KEY = KEY.ESC THEN  
    EXIT()  
END  
IF Key.KEY = KEY.UP THEN  
    MOVEUP()  
END  
# **18. Enum Values Must Be Returned**  
Keyboard APIs must return enum values instead of strings.  
For example:  
Key.KEY  
returns:  
KEY.ENTER  
rather than:  
"ENTER"  
This provides type safety and eliminates magic strings.  
# **19. Keyboard Modifiers**  
Modifiers remain separate properties:  
Key.CTRL  
Key.ALT  
Key.SHIFT  
Example:  
IF Key.CTRL AND Key.KEY = KEY.S THEN  
    SAVE()  
END  
However, this introduces an important distinction:  
KEY  
represents the physical/logical key.  
CTRL  
ALT  
SHIFT  
represent modifiers.  
# **20. Printable Characters**  
For printable characters:  
Key.CHAR  
contains the character.  
Example:  
IF Key.CHAR <> NULL THEN  
    INSERT(Key.CHAR)  
END  
The KEY field may contain:  
KEY.UNKNOWN  
for ordinary printable characters when there is no dedicated enum value.  
This allows the application to distinguish:  
special key  
from:  
printable character  
using:  
IF Key.CHAR <> NULL THEN  
# **21. Enum Combination Operator**  
Enum values may be combined using:  
|  
This creates a combined enum value.  
Example:  
Weekend = Weekday.Saturday | Weekday.Sunday  
This is intended primarily for flag-style enums.  
# **22. Enum Flags**  
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
# **23. Testing Combined Enums**  
A combined enum value can be compared against an enum member using the appropriate enum containment semantics.  
For example:  
IF Permissions | PERMISSION.READ THEN  
    PRINT "Can read"  
END  
However, to avoid ambiguity with boolean expressions, the preferred 0.72 syntax for testing membership is:  
IF Permissions.CONTAINS(PERMISSION.READ) THEN  
    PRINT "Can read"  
END  
### **Important**  
The | operator is the **combination operator**.  
It does not represent logical OR.  
Logical OR remains:  
OR  
Example:  
IF A OR B THEN  
while:  
Permissions =  
    PERMISSION.READ |  
    PERMISSION.WRITE  
combines enum values.  
# **24. Keyboard Modifier Combinations**  
The enum system should also allow composite keyboard definitions where useful.  
For example, the runtime may internally represent:  
CTRL + S  
as:  
CTRL  
+  
KEY.S  
rather than introducing thousands of enum values such as:  
CTRL_S  
CTRL_C  
CTRL_V  
...  
This keeps KEY focused on keys.  
# **25. Recommended KISS Input Handling**  
The KISS editor can therefore use:  
Key = OSL.CONSOLE.GETKEY()  
   
IF Key.KEY = KEY.ESC THEN  
    RUNNING = FALSE  
    RETURN  
END  
   
IF Key.CTRL AND Key.KEY = KEY.S THEN  
    SAVE()  
    RETURN  
END  
   
IF Key.KEY = KEY.UP THEN  
    MOVEUP()  
    RETURN  
END  
   
IF Key.KEY = KEY.DOWN THEN  
    MOVEDOWN()  
    RETURN  
END  
   
IF Key.KEY = KEY.LEFT THEN  
    MOVELEFT()  
    RETURN  
END  
   
IF Key.KEY = KEY.RIGHT THEN  
    MOVERIGHT()  
    RETURN  
END  
   
IF Key.KEY = KEY.BACKSPACE THEN  
    BACKSPACE()  
    RETURN  
END  
   
IF Key.KEY = KEY.DELETE THEN  
    DELETE()  
    RETURN  
END  
   
IF Key.CHAR <> NULL THEN  
    INSERT(Key.CHAR)  
END  
With implicit ME, this becomes considerably cleaner.  
# **26. KISS Example With Implicit ** **ME**  
Instead of:  
ME.DOCUMENT.LINES  
ME.CURSORROW  
ME.CURSORCOLUMN  
ME.SAVE()  
ME.RENDER()  
ME.MOVEUP()  
the KISS can write:  
DOCUMENT.LINES  
CURSORROW  
CURSORCOLUMN  
SAVE()  
RENDER()  
MOVEUP()  
while retaining explicit ME whenever it improves readability.  
# **27. ** **ME** ** and Method Calls**  
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
# **28. ** **ME** ** and Properties**  
Properties can also omit ME.  
CURSORROW = 10  
CURSORCOLUMN = 20  
equivalent to:  
ME.CURSORROW = 10  
ME.CURSORCOLUMN = 20  
# **29. ** **ME** ** and Parent Members**  
Inherited members are also available implicitly.  
CLASS BASE  
   
    PUBLIC NAME  
   
END  
   
   
CLASS CHILD: BASE  
   
    FUNCTION SHOW()  
   
        PRINT NAME  
   
    END  
   
END  
NAME resolves to:  
CHILD  
 ↓  
BASE  
 ↓  
NAME  
# **30. Breaking Changes**  
OSLANG 0.72 is explicitly a **breaking release**.  
## **30.1 Keyboard values**  
Code such as:  
IF Key.KEY = "ENTER" THEN  
must be migrated to:  
IF Key.KEY = KEY.ENTER THEN  
## **30.2 Keyboard API return type**  
Key.KEY changes from a string-like representation to:  
ENUM KEY  
Applications relying on string comparisons must be updated.  
## **30.3 Implicit member resolution**  
Previously, code may have required:  
ME.NAME  
ME.SAVE()  
0.72 permits:  
NAME  
SAVE()  
The runtime/interpreter must implement the new resolution order.  
# **31. Compatibility Recommendation**  
For applications being migrated from 0.7:  
ME.X  
continues to be valid.  
Therefore existing code does not need to immediately remove ME.  
The breaking portion is primarily the keyboard API.  
# **32. OSL.CONSOLE 0.72**  
The console API introduced in 0.7 remains available:  
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
The principal change is the type of the keyboard event's KEY property.  
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
   
        IF Key.KEY = KEY.ESC THEN  
            RUNNING = FALSE  
            RETURN  
        END  
   
        IF Key.CTRL AND Key.KEY = KEY.S THEN  
            SAVE()  
            RETURN  
        END  
   
        IF Key.KEY = KEY.UP THEN  
            MOVEUP()  
            RETURN  
        END  
   
        IF Key.KEY = KEY.DOWN THEN  
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
The identifier resolver in OSLANG 0.72 must conceptually follow:  
                 Identifier  
                      │  
                      ▼  
             Current local scope?  
                 │          │  
                YES         NO  
                 │           │  
                 ▼           ▼  
              RETURN     Parameter?  
                             │  
                        ┌────┴────┐  
                       YES       NO  
                        │         │  
                        ▼         ▼  
                     RETURN   Current class?  
                                  │  
                             ┌────┴────┐  
                            YES       NO  
                             │         │  
                             ▼         ▼  
                          RETURN    Parent class?  
                                        │  
                                   ┌────┴────┐  
                                  YES       NO  
                                   │         │  
                                   ▼         ▼  
                                RETURN     ERROR  
Explicit references such as:  
ME.NAME  
bypass this implicit search.  
# **35. 0.72 Goals**  
The 0.72 release should leave OSLANG capable of expressing the majority of an interactive terminal application without artificial language constructs.  
The key improvements are:  
OSLANG 0.7  
    ↓  
terminal capability  
    ↓  
OSLANG 0.72  
    ↓  
ergonomic object model  
    +  
typed keyboard events  
    +  
enum flags  
    ↓  
KISS.OSL  
The most important architectural rule remains:  
**KISS-specific behavior belongs in KISS.OSL. Generic terminal capabilities belong in ** **OSL.CONSOLE** **.**  
This keeps the KISS migration from turning the OSLANG runtime into a collection of editor-specific APIs.  
   
