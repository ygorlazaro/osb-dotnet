using System;
using System.IO;
using Osb.Lang;
using Osb.Lang.Diagnostics;
using Osb.Lang.Extensibility;
using Osb.Lang.Runtime;
using Xunit;

namespace Osb.Lang.Tests;

public class Oslang02RuntimeTests
{
    private OslangValue Execute(string source, StringWriter? output = null, TextReader? input = null, string? basePath = null)
    {
        var interpreter = new OslangInterpreter();
        return interpreter.Execute(source, output ?? new StringWriter(), input: input, basePath: basePath);
    }

    [Fact]
    public void CompleteExample_OutputsItemBlueAndItemRed()
    {
        var output = new StringWriter();
        var source = @"
INTERFACE IColor

    GET()

    SET(Color String)

END


CLASS Color: IColor

    PRIVATE VAR Color String

    CONSTRUCTOR()

        ME.Color = ""Blue""

    END

    PUBLIC GET()

        RETURN ME.Color

    END

    PUBLIC SET(Color String)

        ME.Color = Color

    END

END


CLASS Item: Color

    PRIVATE VAR Name String

    CONSTRUCTOR()

        ME.Name = ""Item""

    END

    PUBLIC GET_NAME()

        RETURN ME.Name

    END

    PUBLIC DESCRIBE()

        PRINT ME.Name + "": "" + ME.GET()

    END

END


FUNCTION MAIN()

    Item = NEW Item()

    Item.DESCRIBE()

    Item.SET(""Red"")

    Item.DESCRIBE()

END FUNCTION";

        Execute(source, output);
        var result = output.ToString();
        Assert.Contains("Item: Blue", result);
        Assert.Contains("Item: Red", result);
    }

    [Fact]
    public void Inheritance_OverriddenMethod_ReturnsDerivedValue()
    {
        var output = new StringWriter();
        var source = @"
CLASS Color

    GET()

        RETURN ""Blue""

    END

END


CLASS RedColor: Color

    GET()

        RETURN ""Red""

    END

END


FUNCTION MAIN()

    Color = NEW RedColor()

    PRINT Color.GET()

END FUNCTION";

        Execute(source, output);
        Assert.Equal("Red" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void TypeOf_ReturnsClassName()
    {
        var output = new StringWriter();
        var source = @"
CLASS Person

END


FUNCTION MAIN()

    P = NEW Person()

    PRINT TYPEOF(P)

END FUNCTION";

        Execute(source, output);
        Assert.Equal("PERSON" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ObjectEquality_SameInstance_ReturnsTrue()
    {
        var output = new StringWriter();
        var source = @"
CLASS Person

END


FUNCTION MAIN()

    A = NEW Person()
    B = A
    PRINT A = B

END FUNCTION";

        Execute(source, output);
        Assert.Equal("TRUE" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ObjectEquality_DifferentInstances_ReturnsFalse()
    {
        var output = new StringWriter();
        var source = @"
CLASS Person

END


FUNCTION MAIN()

    A = NEW Person()
    B = NEW Person()
    PRINT A = B

END FUNCTION";

        Execute(source, output);
        Assert.Equal("FALSE" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void PrivateProperty_AccessFromOutside_ThrowsRuntimeError()
    {
        var source = @"
CLASS Person

    PRIVATE VAR Name String

    PUBLIC GET_NAME()

        RETURN ME.Name

    END

END


FUNCTION MAIN()

    P = NEW Person()
    PRINT P.Name

END FUNCTION";

        var ex = Assert.Throws<OslangRuntimeException>(() => Execute(source));
        Assert.Contains("PRIVATE", ex.Message);
    }

    [Fact]
    public void ImplicitMemberLookup_ResolvesToMeMember()
    {
        var output = new StringWriter();
        var source = @"
CLASS Person

    VAR Name String

    CONSTRUCTOR()

        ME.Name = ""Alice""

    END

    PUBLIC GET_NAME()

        RETURN Name

    END

END


FUNCTION MAIN()

    P = NEW Person()
    PRINT P.GET_NAME()

END FUNCTION";

        Execute(source, output);
        Assert.Equal("Alice" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void DefaultConstructor_NoExplicitConstructor_CreatesInstance()
    {
        var output = new StringWriter();
        var source = @"
CLASS Person

    VAR Name String

END


FUNCTION MAIN()

    P = NEW Person()
    PRINT TYPEOF(P)

END FUNCTION";

        Execute(source, output);
        Assert.Equal("PERSON" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void InterfaceValidation_MissingMethod_ThrowsSemanticError()
    {
        var source = @"
INTERFACE IColor

    GET()

    SET(Color String)

END


CLASS Color: IColor

    PUBLIC GET()

        RETURN ""Blue""

    END

END


FUNCTION MAIN()

    C = NEW Color()

END FUNCTION";

        var ex = Assert.Throws<SemanticException>(() => Execute(source));
        Assert.Contains("Missing method", ex.Message);
    }

    [Fact]
    public void ProtectedMember_AccessibleFromDerivedClass()
    {
        var output = new StringWriter();
        var source = @"
CLASS BaseClass

    PROTECTED VAR Value Number

END


CLASS Derived: BaseClass

    PUBLIC GET_VALUE()

        RETURN ME.Value

    END

END


FUNCTION MAIN()

    D = NEW Derived()
    PRINT D.GET_VALUE()

END FUNCTION";

        Execute(source, output);
        Assert.Equal("0" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void BaseCall_ExplicitParentConstructor_WithArgs()
    {
        var output = new StringWriter();
        var source = @"
INTERFACE IShape

    GET_NAME()

    GET_AREA()

END


CLASS Shape: IShape

    VAR Name String

    CONSTRUCTOR(Name String)

        ME.Name = Name

    END

    PUBLIC GET_NAME()

        RETURN ME.Name

    END

    PUBLIC GET_AREA()

        RETURN 0

    END

END


CLASS Circle: Shape

    VAR Radius Number

    CONSTRUCTOR(Radius Number)

        BASE(""Unnamed Circle"")
        ME.Radius = Radius

    END

    PUBLIC GET_AREA()

        RETURN 3.14159 * ME.Radius * ME.Radius

    END

END


FUNCTION MAIN()

    C = NEW Circle(10)
    PRINT C.GET_NAME()
    PRINT C.GET_AREA()

END FUNCTION";

        Execute(source, output);
        var lines = output.ToString().Split('\n');
        Assert.Equal("Unnamed Circle", lines[0]);
        Assert.Contains("314.159", lines[1]);
    }

    [Fact]
    public void BaseCall_OutsideConstructor_Works()
    {
        var source = @"
CLASS BaseClass

    VAR BaseValue String

    CONSTRUCTOR()

        ME.BaseValue = ""Base""

    END

    PUBLIC GET_BASE_VALUE()

        RETURN ME.BaseValue

    END

END


CLASS Derived: BaseClass

    PUBLIC TEST()

        BASE()

    END

    PUBLIC GET_TEST_VALUE()

        RETURN BASE.GET_BASE_VALUE()

    END

END


FUNCTION MAIN()

    D = NEW Derived()
    D.TEST()
    PRINT D.GET_TEST_VALUE()

END FUNCTION";

        var output = new StringWriter();
        Execute(source, output);
        Assert.Equal("Base", output.ToString().Trim());
    }

    [Fact]
    public void MultiFile_Using_LoadsClassFromAnotherModule()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "oslang-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var personSource = "CLASS PERSON\r\n\tVAR Name STRING\r\n\tCONSTRUCTOR(Name STRING)\r\n\t\tME.Name = Name\r\n\tEND CONSTRUCTOR\r\n\tPUBLIC GREET()\r\n\t\tRETURN \"Hello, \" + ME.Name\r\n\tEND\r\nEND CLASS";
            File.WriteAllText(Path.Combine(tempDir, "Person.osl"), personSource);

            var mainSource = "USING Person\r\n\r\nFUNCTION MAIN()\r\n\tP = NEW PERSON(\"World\")\r\n\tPRINT P.GREET()\r\nEND FUNCTION";
            File.WriteAllText(Path.Combine(tempDir, "Main.osl"), mainSource);

            var interpreter = new OslangInterpreter();
            var output = new StringWriter();
            var programSource = File.ReadAllText(Path.Combine(tempDir, "Main.osl"));
            interpreter.Execute(programSource, output, basePath: tempDir);

            Assert.Contains("Hello, World", output.ToString());
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void SwitchExpression_ParsesAndEvaluatesCorrectly()
    {
        var source = @"FUNCTION MAIN()
    Result = SWITCH ""Adult""
        CASE ""Child"" => ""Young""
        CASE ""Adult"" => ""Mature""
        DEFAULT => ""Unknown""
    PRINT Result
END FUNCTION";

        var interpreter = new OslangInterpreter();
        var output = new StringWriter();
        interpreter.Execute(source, output);
        Assert.Contains("Mature", output.ToString());
    }

    [Fact]
    public void MultiFileSample_ParsesSuccessfully()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "oslang-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var personSource = @"CLASS PERSON
    VAR Name STRING
    CONSTRUCTOR(Name STRING)
        ME.Name = Name
    END CONSTRUCTOR
    PUBLIC FUNCTION GREET()
        RETURN ""Hello, "" + ME.Name
    END FUNCTION
    PUBLIC FUNCTION GET_AGE_GROUP()
        RETURN ""Adult""
    END FUNCTION
END CLASS";
            File.WriteAllText(Path.Combine(tempDir, "Person.osl"), personSource);

            var mathSource = @"FUNCTION SQUARE(X NUMBER)
    RETURN X * X
END FUNCTION";
            File.WriteAllText(Path.Combine(tempDir, "Utils.osl"), mathSource);

            var mainSource = @"USING Person
USING Utils

FUNCTION MAIN()
    P = NEW PERSON(""Ygor"")
    PRINT SQUARE(5)
    PRINT P.GREET()
    Result = SWITCH P.GET_AGE_GROUP()
        CASE ""Child"" => ""Young""
        CASE ""Adult"" => ""Mature""
        DEFAULT => ""Unknown""
    PRINT Result
END FUNCTION";
            File.WriteAllText(Path.Combine(tempDir, "Main.osl"), mainSource);

            var interpreter = new OslangInterpreter();
            var output = new StringWriter();
            interpreter.Execute(mainSource, output, basePath: tempDir);
            Assert.Contains("25", output.ToString());
            Assert.Contains("Hello, Ygor", output.ToString());
            Assert.Contains("Mature", output.ToString());
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void PrivateMethod_MeCall_FromSameClass_Works()
    {
        var output = new StringWriter();
        var source = @"
CLASS CALENDARAPP
    PUBLIC FUNCTION RUN(Args)
        ME.SHOWMONTH(8, 2026)
    END FUNCTION

    PRIVATE FUNCTION SHOWMONTH(Month, Year)
        PRINT ""Month: "" + STR(Month)
        FirstDay = ME.GETFIRSTDAYOFWEEK(Month, Year)
        PRINT ""FirstDay: "" + STR(FirstDay)
    END FUNCTION

    PRIVATE FUNCTION GETFIRSTDAYOFWEEK(Month, Year)
        RETURN 1
    END FUNCTION
END CLASS

FUNCTION MAIN(Args)
    App = NEW CALENDARAPP()
    App.RUN(Args)
END FUNCTION";

        Execute(source, output);
        Assert.Contains("FirstDay: 1", output.ToString());
    }

    [Fact]
    public void ShowStatement_OutputsWithoutNewline()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    SHOW ""Hello ""
    SHOW ""World""
    PRINT """"
END FUNCTION";

        Execute(source, output);
        Assert.Equal("Hello World" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ArrowFunction_ExpressionBody_Works()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    Double = X => X * 2
    PRINT Double(10)
END FUNCTION";

        Execute(source, output);
        Assert.Equal("20" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ArrowFunction_MultipleParameters_Works()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    Add = (A, B) => A + B
    PRINT Add(3, 4)
END FUNCTION";

        Execute(source, output);
        Assert.Equal("7" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ArrowFunction_BlockBody_Works()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    Double = X =>
        Result = X * 2
        RETURN Result
    END
    PRINT Double(10)
END FUNCTION";

        Execute(source, output);
        Assert.Equal("20" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ArrowFunction_Closure_CapturesVariable()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    Multiplier = 10
    Multiply = X => X * Multiplier
    PRINT Multiply(5)
END FUNCTION";

        Execute(source, output);
        Assert.Equal("50" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ModOperator_Works()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    Result = 10 MOD 3
    PRINT Result
END FUNCTION";

        Execute(source, output);
        Assert.Equal("1" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void PowerOperator_Works()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    Result = 2 ** 8
    PRINT Result
END FUNCTION";

        Execute(source, output);
        Assert.Equal("256" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void PostfixIncrement_ReturnsOriginalValue()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    Counter = 10
    Value = Counter++
    PRINT Value
    PRINT Counter
END FUNCTION";

        Execute(source, output);
        var lines = output.ToString().Split('\n');
        Assert.Equal("10", lines[0]);
        Assert.Equal("11", lines[1]);
    }

    [Fact]
    public void CompoundAssignment_PlusEqual_Works()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    Total = 10
    Total += 5
    PRINT Total
END FUNCTION";

        Execute(source, output);
        Assert.Equal("15" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void NestedArrays_Work()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    Matrix = [[1, 2], [3, 4]]
    PRINT Matrix[0][1]
END FUNCTION";

        Execute(source, output);
        Assert.Equal("2" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ArrayFindIndex_Works()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    Numbers = [5, 8, 13, 21]
    Index = Numbers.FINDINDEX(X => X > 10)
    PRINT Index
END FUNCTION";

        Execute(source, output);
        Assert.Equal("2" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ArrayPushPop_Work()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    Numbers = [1, 2, 3]
    Numbers.PUSH(4)
    Last = Numbers.POP()
    PRINT Last
    PRINT Numbers[2]
END FUNCTION";

        Execute(source, output);
        var lines = output.ToString().Split('\n');
        Assert.Equal("4", lines[0]);
        Assert.Equal("3", lines[1]);
    }

    [Fact]
    public void ArrayFlat_Works()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    Values = [[1, 2], [3, 4]]
    Result = Values.FLAT()
    PRINT Result[2]
END FUNCTION";

        Execute(source, output);
        Assert.Equal("3" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void StringPadStart_Works()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    Result = ""42"".PADSTART(5, ""0"")
    PRINT Result
END FUNCTION";

        Execute(source, output);
        Assert.Equal("00042" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void StringRepeat_Works()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    Result = ""OS"".REPEAT(3)
    PRINT Result
END FUNCTION";

        Execute(source, output);
        Assert.Equal("OSOSOS" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void NumberTrunc_WithDecimals_Works()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    Value = 3.141592
    PRINT Value.TRUNC(2)
END FUNCTION";

        Execute(source, output);
        Assert.Equal("3.14" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void PrefixIncrement_ThrowsSyntaxError()
    {
        var source = @"FUNCTION MAIN()
    Counter = 10
    Value = ++Counter
END FUNCTION";

        var ex = Assert.Throws<SyntaxException>(() => Execute(source));
        Assert.Contains("++", ex.Message);
    }

    [Fact]
    public void MathPi_Constant_Works()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    PRINT MATH.PI
END FUNCTION";

        Execute(source, output);
        Assert.Contains("3.14159", output.ToString());
    }

    [Fact]
    public void ArrayForeach_Works()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    Sum = 0
    Numbers = [1, 2, 3]
    Numbers.FOREACH(X =>
        Sum = Sum + X
    END)
    PRINT Sum
END FUNCTION";

        Execute(source, output);
        Assert.Equal("6" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void PowerOperator_RightAssociative_Works()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    Result = 2 ** 3 ** 2
    PRINT Result
END FUNCTION";

        Execute(source, output);
        Assert.Equal("512" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void StringPadEnd_Works()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    Result = ""42"".PADEND(5, ""0"")
    PRINT Result
END FUNCTION";

        Execute(source, output);
        Assert.Equal("42000" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void Using_OslI18n_RegistersI18nModule()
    {
        var output = new StringWriter();
        var source = @"
USING OSL.I18N
FUNCTION MAIN()
    PRINT I18N.GET(""hello"")
END FUNCTION";

        Execute(source, output);
        Assert.Equal("hello" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void OslI18n_Get_ReturnsTranslatedString()
    {
        var output = new StringWriter();
        var source = @"
USING OSL.I18N
FUNCTION MAIN()
    PRINT I18N.GET(""boot.welcome"")
END FUNCTION";

        Execute(source, output, basePath: "I18N");
        Assert.Equal("Welcome to OSB" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void OslI18n_Get_WithParameters_SubstitutesPlaceholders()
    {
        var output = new StringWriter();
        var source = @"
USING OSL.I18N
FUNCTION MAIN()
    PRINT I18N.GET(""user.greeting"", ""Ygor"")
END FUNCTION";

        Execute(source, output, basePath: "I18N");
        Assert.Equal("Hello, Ygor!" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void OslI18n_Has_ReturnsTrueForExistingKey()
    {
        var output = new StringWriter();
        var source = @"
USING OSL.I18N
FUNCTION MAIN()
    IF I18N.HAS(""boot.welcome"") THEN
        PRINT ""YES""
    END
END FUNCTION";

        Execute(source, output, basePath: "I18N");
        Assert.Equal("YES" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void OslI18n_Get_MissingKey_ReturnsKey()
    {
        var output = new StringWriter();
        var source = @"
USING OSL.I18N
FUNCTION MAIN()
    PRINT I18N.GET(""missing.key"")
END FUNCTION";

        Execute(source, output);
        Assert.Equal("missing.key" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void OslI18n_SetLanguage_ChangesActiveLanguage()
    {
        var output = new StringWriter();
        var source = @"
USING OSL.I18N
FUNCTION MAIN()
    I18N.SETLANGUAGE(""PT-BR"")
    PRINT I18N.GET(""boot.welcome"")
END FUNCTION";

        Execute(source, output, basePath: "I18N");
        Assert.Equal("Bem-vindo ao OSB" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void OslI18n_Languages_ReturnsAvailableLanguages()
    {
        var output = new StringWriter();
        var source = @"
USING OSL.I18N
FUNCTION MAIN()
    langs = I18N.LANGUAGES()
    FOR I = 0 TO COUNT(langs) - 1
        PRINT langs[I]
    END
END FUNCTION";

        Execute(source, output, basePath: "I18N");
        var result = output.ToString();
        Assert.Contains("EN-US", result);
        Assert.Contains("PT-BR", result);
    }

    [Fact]
    public void OslI18n_FullyQualified_OslI18nGet_Works()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    PRINT OSL.I18N.GET(""boot.welcome"")
END FUNCTION";

        Execute(source, output, basePath: "I18N");
        Assert.Equal("Welcome to OSB" + Environment.NewLine, output.ToString());
    }

    // ============================================================
    // OSLANG 0.61 - ENUM
    // ============================================================

    [Fact]
    public void Enum_Declaration_CreatesTypedValues()
    {
        var output = new StringWriter();
        var source = @"
ENUM Color
    RED
    GREEN
    BLUE
END

FUNCTION MAIN()
    C = Color.RED
    PRINT TYPEOF(C)
    PRINT C.NAME()
    PRINT C.VALUE()
END FUNCTION";

        Execute(source, output);
        var result = output.ToString();
        Assert.Contains("COLOR" + Environment.NewLine, result);
        Assert.Contains("RED" + Environment.NewLine, result);
        Assert.Contains("0" + Environment.NewLine, result);
    }

    [Fact]
    public void Enum_NumericValues_ArePreserved()
    {
        var output = new StringWriter();
        var source = @"
ENUM Weekday
    Sunday = 0
    Monday = 1
    Saturday = 6
END

FUNCTION MAIN()
    PRINT Weekday.Saturday.VALUE()
END FUNCTION";

        Execute(source, output);
        Assert.Equal("6" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void EnumSet_Operator_CreatesSet()
    {
        var output = new StringWriter();
        var source = @"
ENUM Weekday
    Saturday = 6
    Sunday = 0
    Monday = 1
END

FUNCTION MAIN()
    Weekend = Weekday.Saturday | Weekday.Sunday
    PRINT Weekend.CONTAINS(Weekday.Saturday)
    PRINT Weekend.COUNT()
END FUNCTION";

        Execute(source, output);
        var result = output.ToString();
        Assert.Contains("TRUE" + Environment.NewLine, result);
        Assert.Contains("2" + Environment.NewLine, result);
    }

    [Fact]
    public void Switch_Statement_SelectsCase()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    Day = 2
    SWITCH Day
        CASE 1
            PRINT ""One""
        CASE 2
            PRINT ""Two""
        DEFAULT
            PRINT ""Other""
    END
END FUNCTION";

        Execute(source, output);
        Assert.Equal("Two" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void Break_ExitsSwitch()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    X = 1
    SWITCH X
        CASE 1
            PRINT ""Matched""
            BREAK
        DEFAULT
            PRINT ""Default""
    END
    PRINT ""After""
END FUNCTION";

        Execute(source, output);
        var result = output.ToString();
        Assert.Contains("Matched" + Environment.NewLine, result);
        Assert.Contains("After" + Environment.NewLine, result);
    }

    [Fact]
    public void StringInterpolation_SubstitutesVariables()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    Name = ""Ygor""
    Age = 40
    Message = ""Hello ${Name}, you are ${Age} years old.""
    PRINT Message
END FUNCTION";

        Execute(source, output);
        Assert.Equal("Hello Ygor, you are 40 years old." + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void MultilineString_PreservesNewlines()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    Message = """"""
Hello
World
""""""
    PRINT Message
END FUNCTION";

        Execute(source, output);
        Assert.Equal("Hello" + Environment.NewLine + "World" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void StringEscape_NewlineAndTab_Work()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    Message = ""Hello\nWorld\tTab""
    PRINT Message
END FUNCTION";

        Execute(source, output);
        Assert.Equal("Hello" + Environment.NewLine + "World\tTab" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void IfStatement_TrueCondition_ExecutesThenBlock()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    IF TRUE THEN
        PRINT ""Yes""
    END
END FUNCTION";

        Execute(source, output);
        Assert.Equal("Yes" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void IfStatement_FalseCondition_ExecutesElseBlock()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    IF FALSE THEN
        PRINT ""Yes""
    ELSE
        PRINT ""No""
    END
END FUNCTION";

        Execute(source, output);
        Assert.Equal("No" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ElifStatement_SecondCondition_ExecutesCorrectly()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    X = 2
    IF X = 1 THEN
        PRINT ""One""
    ELIF X = 2 THEN
        PRINT ""Two""
    ELSE
        PRINT ""Other""
    END
END FUNCTION";

        Execute(source, output);
        Assert.Equal("Two" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void WhileLoop_IteratesCorrectly()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    I = 1
    WHILE I <= 3
        PRINT I
        I = I + 1
    END
END FUNCTION";

        Execute(source, output);
        Assert.Equal("1" + Environment.NewLine + "2" + Environment.NewLine + "3" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void DoWhileLoop_ExecutesAtLeastOnce()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    I = 1
    DO WHILE I <= 3
        PRINT I
        I = I + 1
    END
END FUNCTION";

        Execute(source, output);
        Assert.Equal("1" + Environment.NewLine + "2" + Environment.NewLine + "3" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ForLoop_CountsUpCorrectly()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    FOR I = 1 TO 3
        PRINT I
    END
END FUNCTION";

        Execute(source, output);
        Assert.Equal("1" + Environment.NewLine + "2" + Environment.NewLine + "3" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ForLoop_CountsDownWithStep()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    FOR I = 3 TO 1 STEP -1
        PRINT I
    END
END FUNCTION";

        Execute(source, output);
        Assert.Equal("3" + Environment.NewLine + "2" + Environment.NewLine + "1" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void BreakStatement_ExitsLoop()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    FOR I = 1 TO 10
        IF I = 4 THEN
            BREAK
        END
        PRINT I
    END
END FUNCTION";

        Execute(source, output);
        Assert.Equal("1" + Environment.NewLine + "2" + Environment.NewLine + "3" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ContinueStatement_SkipsIteration()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    FOR I = 1 TO 5
        IF I = 3 THEN
            CONTINUE
        END
        PRINT I
    END
END FUNCTION";

        Execute(source, output);
        Assert.Equal("1" + Environment.NewLine + "2" + Environment.NewLine + "4" + Environment.NewLine + "5" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ReturnStatement_ExitsFunction()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    PRINT TEST()
    PRINT ""After""
END FUNCTION

FUNCTION TEST()
    RETURN 42
END FUNCTION";

        Execute(source, output);
        Assert.Equal("42" + Environment.NewLine + "After" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void TryCatch_HandlesRuntimeError()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    TRY
        PRINT 10 / 0
    CATCH err
        PRINT ""Caught: "" + err
    END
END FUNCTION";

        Execute(source, output);
        Assert.Equal("Caught: Division by zero." + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void NewOperator_CreatesInstance()
    {
        var output = new StringWriter();
        var source = @"
CLASS Person
    PUBLIC VAR Name String
END CLASS

FUNCTION MAIN()
    P = NEW Person()
    P.Name = ""Alice""
    PRINT P.Name
END FUNCTION";

        Execute(source, output);
        Assert.Equal("Alice" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void NullValue_ComparesCorrectly()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    X = NULL
    IF X = NULL THEN
        PRINT ""Null""
    ELSE
        PRINT ""Not null""
    END
END FUNCTION";

        Execute(source, output);
        Assert.Equal("Null" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void BooleanOperators_AndOr_WorkCorrectly()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    PRINT TRUE AND FALSE
    PRINT TRUE OR FALSE
    PRINT NOT TRUE
    PRINT TRUE AND TRUE
END FUNCTION";

        Execute(source, output);
        Assert.Equal("FALSE" + Environment.NewLine + "TRUE" + Environment.NewLine + "FALSE" + Environment.NewLine + "TRUE" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ComparisonOperators_WorkCorrectly()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    PRINT 5 = 5
    PRINT 5 <> 3
    PRINT 3 < 5
    PRINT 5 > 3
    PRINT 3 <= 3
    PRINT 5 >= 5
END FUNCTION";

        Execute(source, output);
        Assert.Equal("TRUE" + Environment.NewLine + "TRUE" + Environment.NewLine + "TRUE" + Environment.NewLine + "TRUE" + Environment.NewLine + "TRUE" + Environment.NewLine + "TRUE" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void StringConcatenation_WithPlus_Works()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    Result = ""Hello"" + "" "" + ""World""
    PRINT Result
END FUNCTION";

        Execute(source, output);
        Assert.Equal("Hello World" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ArithmeticOperators_WorkCorrectly()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    PRINT 10 + 5
    PRINT 10 - 3
    PRINT 4 * 7
    PRINT 20 / 4
    PRINT 10 % 3
END FUNCTION";

        Execute(source, output);
        Assert.Equal("15" + Environment.NewLine + "7" + Environment.NewLine + "28" + Environment.NewLine + "5" + Environment.NewLine + "1" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void UnaryMinus_NegatesNumber()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    PRINT -5
    PRINT -(-3)
END FUNCTION";

        Execute(source, output);
        Assert.Equal("-5" + Environment.NewLine + "3" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void VariableDeclaration_WithType_Works()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    VAR Total NUMBER
    Total = 42
    PRINT Total
END FUNCTION";

        Execute(source, output);
        Assert.Equal("42" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void FunctionParameters_AndReturn_Work()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    Result = ADD(3, 4)
    PRINT Result
END FUNCTION

FUNCTION ADD(A NUMBER, B NUMBER)
    RETURN A + B
END FUNCTION";

        Execute(source, output);
        Assert.Equal("7" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void RecursiveFunction_ComputesFactorial()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    PRINT FACTORIAL(5)
END FUNCTION

FUNCTION FACTORIAL(N NUMBER)
    IF N <= 1 THEN
        RETURN 1
    END
    RETURN N * FACTORIAL(N - 1)
END FUNCTION";

        Execute(source, output);
        Assert.Equal("120" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void StringMethods_ToUpperToLowerTrimLengthSubstr_Work()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    PRINT ""hello"".TOUPPER()
    PRINT ""HELLO"".TOLOWER()
    PRINT ""  hi  "".TRIM()
    PRINT ""abc"".COUNT()
    PRINT ""abc"".SUBSTR(0, 2)
    PRINT ""hello"".CONTAINS(""ell"")
END FUNCTION";

        Execute(source, output);
        Assert.Equal("HELLO" + Environment.NewLine + "hello" + Environment.NewLine + "hi" + Environment.NewLine + "3" + Environment.NewLine + "ab" + Environment.NewLine + "TRUE" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void StringMethod_Reverse_Works()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    PRINT ""hello"".REVERSE()
END FUNCTION";

        Execute(source, output);
        Assert.Equal("olleh" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void StringMethod_Normalize_RemovesDiacritics()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    PRINT ""Açúcar"".NORMALIZE()
END FUNCTION";

        Execute(source, output);
        Assert.Equal("ACUCAR" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ArrayMethods_CountFirstLastSortJoin_Work()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    N = [3, 1, 2]
    PRINT N.COUNT()
    PRINT N.FIRST()
    PRINT N.LAST()
    PRINT N.SORT().JOIN("","")
END FUNCTION";

        Execute(source, output);
        Assert.Equal("3" + Environment.NewLine + "3" + Environment.NewLine + "2" + Environment.NewLine + "1,2,3" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ArrayMethod_IndexOf_ReturnsCorrectIndex()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    N = [10, 20, 30]
    PRINT N.INDEXOF(20)
    PRINT N.INDEXOF(99)
END FUNCTION";

        Execute(source, output);
        Assert.Equal("1" + Environment.NewLine + "-1" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ArrayMethod_Remove_RemovesFirstOccurrence()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    N = [1, 2, 3, 2]
    N.REMOVE(2)
    PRINT N.JOIN("","")
END FUNCTION";

        Execute(source, output);
        Assert.Equal("1,2,3" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ArrayMethod_Reverse_ReversesInPlace()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    N = [1, 2, 3]
    N.REVERSE()
    PRINT N.JOIN("","")
END FUNCTION";

        Execute(source, output);
        Assert.Equal("3,2,1" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void TypeConversion_StrNumberBool_Work()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    PRINT STR(42)
    PRINT NUMBER(""3.14"")
    PRINT BOOL(1)
    PRINT BOOL(0)
END FUNCTION";

        Execute(source, output);
        Assert.Equal("42" + Environment.NewLine + "3.14" + Environment.NewLine + "TRUE" + Environment.NewLine + "FALSE" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void MathSqrt_PowFloorCeil_Work()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    PRINT MATH.SQRT(9)
    PRINT MATH.POW(2, 3)
    PRINT MATH.FLOOR(3.7)
    PRINT MATH.CEIL(3.1)
END FUNCTION";

        Execute(source, output);
        Assert.Equal("3" + Environment.NewLine + "8" + Environment.NewLine + "3" + Environment.NewLine + "4" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void DivisionByZero_ThrowsRuntimeError()
    {
        var source = @"
FUNCTION MAIN()
    PRINT 10 / 0
END FUNCTION";

        Assert.Throws<OslangRuntimeException>(() => Execute(source));
    }

    [Fact]
    public void ArrayIndexOutOfBounds_ThrowsRuntimeError()
    {
        var source = @"
FUNCTION MAIN()
    X = [1, 2, 3]
    PRINT X[5]
END FUNCTION";

        Assert.Throws<OslangRuntimeException>(() => Execute(source));
    }

    [Fact]
    public void UndefinedVariable_ThrowsRuntimeError()
    {
        var source = @"
FUNCTION MAIN()
    PRINT UndefinedVar
END FUNCTION";

        Assert.Throws<OslangRuntimeException>(() => Execute(source));
    }

    [Fact]
    public void PostfixDecrement_ReturnsOriginalValue()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    Counter = 10
    Value = Counter--
    PRINT Value
    PRINT Counter
END FUNCTION";

        Execute(source, output);
        Assert.Equal("10" + Environment.NewLine + "9" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void PrefixIncrement_SyntaxError()
    {
        var source = @"
FUNCTION MAIN()
    ++Counter
END FUNCTION";

        Assert.Throws<SyntaxException>(() => Execute(source));
    }

    [Fact]
    public void SwitchExpression_ReturnsValue()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    Day = 2
    Name = SWITCH Day
        CASE 1 => ""Monday""
        CASE 2 => ""Tuesday""
        DEFAULT => ""Unknown""
    PRINT Name
END FUNCTION";

        Execute(source, output);
        Assert.Equal("Tuesday" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ForEachLoop_IteratesArray()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    Sum = 0
    Numbers = [1, 2, 3, 4]
    Numbers.FOREACH(X =>
        Sum = Sum + X
    END)
    PRINT Sum
END FUNCTION";

        Execute(source, output);
        Assert.Equal("10" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void Comments_AreIgnored()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    REM This is a comment
    PRINT ""Hello"" REM inline comment
    ' Another comment
    PRINT ""World""
END FUNCTION";

        Execute(source, output);
        Assert.Equal("Hello" + Environment.NewLine + "World" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void CaseInsensitive_Keywords_Work()
    {
        var output = new StringWriter();
        var source = @"
function main()
    print ""Hello""
end function";

        Execute(source, output);
        Assert.Equal("Hello" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void NestedFunctionCalls_Work()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    PRINT ADD(2, MULTIPLY(3, 4))
END FUNCTION

FUNCTION ADD(A NUMBER, B NUMBER)
    RETURN A + B
END FUNCTION

FUNCTION MULTIPLY(A NUMBER, B NUMBER)
    RETURN A * B
END FUNCTION";

        Execute(source, output);
        Assert.Equal("14" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void MemberAccess_OnObject_Works()
    {
        var output = new StringWriter();
        var source = @"
CLASS Point
    PUBLIC VAR X NUMBER
    PUBLIC VAR Y NUMBER
END CLASS

FUNCTION MAIN()
    P = NEW Point()
    P.X = 10
    P.Y = 20
    PRINT P.X
    PRINT P.Y
END FUNCTION";

        Execute(source, output);
        Assert.Equal("10" + Environment.NewLine + "20" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void MeKeyword_RefersToCurrentInstance()
    {
        var output = new StringWriter();
        var source = @"
CLASS Counter
    PRIVATE VAR Value NUMBER
    
    PUBLIC CONSTRUCTOR()
        ME.Value = 0
    END
    
    PUBLIC INCREMENT()
        ME.Value = ME.Value + 1
    END
    
    PUBLIC GET()
        RETURN ME.Value
    END
END CLASS

FUNCTION MAIN()
    C = NEW Counter()
    C.INCREMENT()
    C.INCREMENT()
    PRINT C.GET()
END FUNCTION";

        Execute(source, output);
        Assert.Equal("2" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void BaseCall_WithArguments_Works()
    {
        var output = new StringWriter();
        var source = @"
CLASS Person
    PUBLIC VAR Name String
    
    PUBLIC CONSTRUCTOR(Name String)
        ME.Name = Name
    END
END CLASS

CLASS Employee: Person
    PUBLIC VAR Id NUMBER
    
    PUBLIC CONSTRUCTOR(Name String, Id NUMBER)
        BASE(Name)
        ME.Id = Id
    END
    
    PUBLIC DESCRIBE()
        RETURN ME.Name + "" (#"" + STR(ME.Id) + "")""
    END
END CLASS

FUNCTION MAIN()
    E = NEW Employee(""Alice"", 123)
    PRINT E.DESCRIBE()
END FUNCTION";

        Execute(source, output);
        Assert.Equal("Alice (#123)" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void EnumSet_Contains_Works()
    {
        var output = new StringWriter();
        var source = @"ENUM Color
    RED
    GREEN
    BLUE
END

FUNCTION MAIN()
    C = Color.RED
    PRINT C.NAME()
END FUNCTION";

        Execute(source, output);
        Assert.Equal("RED" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void DefaultBranch_SwitchExpression_Works()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    X = 99
    Result = SWITCH X
        CASE 1 => ""One""
        CASE 2 => ""Two""
        DEFAULT => ""Other""
    PRINT Result
END FUNCTION";

        Execute(source, output);
        Assert.Equal("Other" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void NestedArray_Access_Works()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    Matrix = [[1, 2], [3, 4]]
    PRINT Matrix[0][0]
    PRINT Matrix[0][1]
    PRINT Matrix[1][0]
    PRINT Matrix[1][1]
END FUNCTION";

        Execute(source, output);
        Assert.Equal("1" + Environment.NewLine + "2" + Environment.NewLine + "3" + Environment.NewLine + "4" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void NumberTrunc_WithDefaultPrecision_Works()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    PRINT 3.14159.TRUNC()
END FUNCTION";

        Execute(source, output);
        Assert.Equal("3" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void NumberTrunc_WithPrecision_Works()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    PRINT 3.14159.TRUNC(2)
END FUNCTION";

        Execute(source, output);
        Assert.Equal("3.14" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ArrayPushPop_WorkCorrectly()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    N = [1, 2]
    N.PUSH(3)
    PRINT N.JOIN("","")
    Last = N.POP()
    PRINT Last
    PRINT N.JOIN("","")
END FUNCTION";

        Execute(source, output);
        Assert.Equal("1,2,3" + Environment.NewLine + "3" + Environment.NewLine + "1,2" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ArrayFlat_FlattensNestedArrays()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    M = [[1, 2], [3, 4]]
    F = M.FLAT()
    PRINT F.JOIN("","")
END FUNCTION";

        Execute(source, output);
        Assert.Equal("1,2,3,4" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ModOperator_KeywordForm_Works()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    PRINT 10 MOD 3
END FUNCTION";

        Execute(source, output);
        Assert.Equal("1" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void InputStatement_ReadsUserInput()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    INPUT Name
    PRINT Name
END FUNCTION";

        Execute(source, output, input: new StringReader("Alice"));
        Assert.Equal("Alice" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void TypeOf_ReturnsClassNameForInstance()
    {
        var output = new StringWriter();
        var source = @"
CLASS Foo
END CLASS

FUNCTION MAIN()
    PRINT TYPEOF(NEW Foo())
END FUNCTION";

        Execute(source, output);
        Assert.Equal("FOO" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ShowStatement_NoTrailingNewline()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    SHOW ""Hello ""
    SHOW ""World""
    PRINT """"
END FUNCTION";

        Execute(source, output);
        Assert.Equal("Hello World" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void SwitchStatement_WithBreak_Works()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    X = 2
    SWITCH X
        CASE 1
            PRINT ""One""
        CASE 2
            PRINT ""Two""
        CASE 3
            PRINT ""Three""
        DEFAULT
            PRINT ""Other""
    END
END FUNCTION";

        Execute(source, output);
        Assert.Equal("Two" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void EnumDeclaration_WithValues_Works()
    {
        var output = new StringWriter();
        var source = @"ENUM Color
    RED = 1
    GREEN = 2
    BLUE = 4
END

FUNCTION MAIN()
    PRINT Color.RED.VALUE()
    PRINT Color.GREEN.VALUE()
    PRINT Color.BLUE.VALUE()
END FUNCTION";

        Execute(source, output);
        Assert.Equal("1" + Environment.NewLine + "2" + Environment.NewLine + "4" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void OslI18n_Keys_ReturnsAllKeys()
    {
        var output = new StringWriter();
        var source = @"
USING OSL.I18N

FUNCTION MAIN()
    Keys = I18N.KEYS()
    PRINT COUNT(Keys)
END FUNCTION";

        Execute(source, output);
        var count = int.Parse(output.ToString().Trim());
        Assert.True(count > 0, $"Expected at least 1 I18N key, got {count}");
    }

    [Fact]
    public void ArrayFindIndex_WithLambda_Works()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    N = [1, 2, 3, 4, 5]
    Idx = N.FINDINDEX(X => X > 3)
    PRINT Idx
END FUNCTION";

        Execute(source, output);
        Assert.Equal("3" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void EnumValue_Equality_Works()
    {
        var output = new StringWriter();
        var source = @"ENUM Color
    RED
    GREEN
    BLUE
END

FUNCTION MAIN()
    C = Color.RED
    IF C = Color.RED THEN
        PRINT ""matched""
    ELSE
        PRINT ""no match""
    END
END FUNCTION";

        Execute(source, output);
        Assert.Equal("matched" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void EnumValue_Inequality_Works()
    {
        var output = new StringWriter();
        var source = @"ENUM Color
    RED
    GREEN
    BLUE
END

FUNCTION MAIN()
    C = Color.RED
    IF C = Color.GREEN THEN
        PRINT ""matched""
    ELSE
        PRINT ""no match""
    END
END FUNCTION";

        Execute(source, output);
        Assert.Equal("no match" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void Switch_Enum_CaseMatches()
    {
        var output = new StringWriter();
        var source = @"ENUM Status
    PENDING
    ACTIVE
    DONE
END

FUNCTION MAIN()
    S = Status.ACTIVE
    SWITCH S
        CASE Status.PENDING
            PRINT ""pending""
        CASE Status.ACTIVE
            PRINT ""active""
        CASE Status.DONE
            PRINT ""done""
        DEFAULT
            PRINT ""unknown""
    END
END FUNCTION";

        Execute(source, output);
        Assert.Equal("active" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void Switch_EnumSet_Contains_Matches()
    {
        var output = new StringWriter();
        var source = @"ENUM Weekday
    Sunday
    Monday
    Tuesday
    Wednesday
    Thursday
    Friday
    Saturday
END

FUNCTION MAIN()
    D = Weekday.Saturday
    SWITCH D
        CASE Weekday.Saturday | Weekday.Sunday
            PRINT ""weekend""
        CASE Weekday.Monday | Weekday.Tuesday | Weekday.Wednesday | Weekday.Thursday | Weekday.Friday
            PRINT ""weekday""
        DEFAULT
            PRINT ""unknown""
    END
END FUNCTION";

        Execute(source, output);
        Assert.Equal("weekend" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void StringInterpolation_EscapedDollar_ProducesLiteral()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    Message = ""\${Name}""
    PRINT Message
END FUNCTION";

        Execute(source, output);
        Assert.Equal("${Name}" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void StringInterpolation_UnescapedDollar_Interpolates()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    Name = ""OSB""
    Message = ""Hello ${Name}""
    PRINT Message
END FUNCTION";

        Execute(source, output);
        Assert.Equal("Hello OSB" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void Enum_ImplicitThenExplicitString_ConvertsImplicit()
    {
        var output = new StringWriter();
        var source = @"ENUM Priority
    LOW
    MEDIUM = ""medium""
    HIGH
END

FUNCTION MAIN()
    PRINT Priority.LOW.VALUE()
    PRINT Priority.MEDIUM.VALUE()
    PRINT Priority.HIGH.VALUE()
END FUNCTION";

        Execute(source, output);
        Assert.Equal("0" + Environment.NewLine + "medium" + Environment.NewLine + "2" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void Json_Parse_ReturnsObjectWithProperties()
    {
        var output = new StringWriter();
        var source = @"USING OSL.JSON

FUNCTION MAIN()
    JsonText = ""{\""name\"":\""Ygor\"",\""age\"":40}""
    Data = JSON.PARSE(JsonText)
    PRINT Data.name
    PRINT Data.age
END FUNCTION";

        Execute(source, output);
        Assert.Equal("Ygor" + Environment.NewLine + "40" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void Json_Stringify_ProducesValidJson()
    {
        var output = new StringWriter();
        var source = @"USING OSL.JSON

FUNCTION MAIN()
    JsonText = ""{\""name\"":\""Ygor\"",\""age\"":40}""
    Parsed = JSON.PARSE(JsonText)
    Output = JSON.STRINGIFY(Parsed)
    PRINT Output
END FUNCTION";

        Execute(source, output);
        Assert.Contains("\"name\":\"Ygor\"", output.ToString());
        Assert.Contains("\"age\":40", output.ToString());
    }

    [Fact]
    public void Json_Pretty_FormatsJson()
    {
        var output = new StringWriter();
        var source = @"USING OSL.JSON

FUNCTION MAIN()
    JsonText = ""{\""name\"":\""Ygor\"",\""age\"":40}""
    Parsed = JSON.PARSE(JsonText)
    PrettyOutput = JSON.PRETTY(Parsed)
    PRINT PrettyOutput
END FUNCTION";

        Execute(source, output);
        var result = output.ToString();
        Assert.Contains("\n", result);
        Assert.Contains("  ", result);
    }

    [Fact]
    public void Csv_Parse_ReturnsArrayOfObjects()
    {
        var output = new StringWriter();
        var source = @"USING OSL.CSV

FUNCTION MAIN()
    CsvText = ""name,age\nYgor,40\nJohn,25""
    Data = CSV.PARSE(CsvText, TRUE)
    PRINT Data[0].name
    PRINT Data[1].age
END FUNCTION";

        Execute(source, output);
        Assert.Equal("Ygor" + Environment.NewLine + "25" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void Xml_Parse_ReturnsNavigableDocument()
    {
        var output = new StringWriter();
        var source = @"USING OSL.XML

FUNCTION MAIN()
    XmlText = ""<user><name>Ygor</name><age>40</age></user>""
    Doc = XML.PARSE(XmlText)
    PRINT Doc.NAME()
    Name = Doc.CHILD(""name"")
    PRINT Name.VALUE()
END FUNCTION";

        Execute(source, output);
        Assert.Equal("user" + Environment.NewLine + "Ygor" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void Cnf_ReadGetSetSave_Works()
    {
        var output = new StringWriter();
        var tempPath = Path.Combine(Path.GetTempPath(), "osb-test-" + Guid.NewGuid().ToString("N") + ".cfg");
        File.WriteAllText(tempPath, "COLOR=RED\nLANG=EN-US\n");
        try
        {
            var source = $@"USING OSL.CNF

FUNCTION MAIN()
    Config = CNF.READ(""{tempPath}"")
    IF Config.HAS(""COLOR"") THEN
        PRINT Config.GET(""COLOR"")
    END
    Config.SET(""COLOR"", ""BLUE"")
    PRINT Config.GET(""COLOR"")
    Config.SAVE(""{tempPath}"")
END FUNCTION";

            Execute(source, output);
            Assert.Contains("RED", output.ToString());
            Assert.Contains("BLUE", output.ToString());
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public void Net_Ping_ReturnsResultObject()
    {
        var output = new StringWriter();
        var source = @"USING OSB.NET

FUNCTION MAIN()
    Result = OSB.NET.PING(""localhost"")
    PRINT Result.host
    PRINT Result.success
END FUNCTION";

        Execute(source, output);
        Assert.Equal("localhost" + Environment.NewLine + "TRUE" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void Net_Down_ReturnsResponse()
    {
        var output = new StringWriter();
        var source = @"USING OSB.NET

FUNCTION MAIN()
    Response = OSB.NET.DOWN(""https://example.com"")
    PRINT Response.STATUS
    PRINT Response.BODY
END FUNCTION";

        Execute(source, output);
        Assert.Contains("200", output.ToString());
    }

    [Fact]
    public void Console_Namespace_WritesAndColors()
    {
        var output = new StringWriter();
        var source = @"USING OSL.CONSOLE
USING OSL.APP

FUNCTION MAIN()
    OSL.CONSOLE.WRITE(1, 1, ""Hello"")
    OSL.CONSOLE.CLEAR()
    OSL.CONSOLE.HIDECURSOR()
    OSL.CONSOLE.SHOWCURSOR()
    OSL.CONSOLE.ENTER()
    OSL.CONSOLE.EXIT()
    OSL.CONSOLE.ALTERNATE(TRUE)
    OSL.CONSOLE.ALTERNATE(FALSE)
    OSL.CONSOLE.BEGINFRAME()
    OSL.CONSOLE.ENDFRAME()
    OSL.CONSOLE.FLUSH()
    OSL.CONSOLE.BEEP()
    PRINT ""Console OK""
    OSL.APP.EXIT(0)
END FUNCTION";

        var extensions = new ExtensionRegistry();
        extensions.Register("CONSOLE.WHITE", (args, location) => new NumberValue(7));
        extensions.Register("CONSOLE.BLUE", (args, location) => new NumberValue(1));
        extensions.Register("CONSOLE.WRITE", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.CLEAR", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.HIDECURSOR", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.SHOWCURSOR", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.ENTER", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.EXIT", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.ALTERNATE", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.BEGINFRAME", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.ENDFRAME", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.FLUSH", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.BEEP", (args, location) => OslangValue.Null);
        extensions.Register("APP.EXIT", (args, location) => throw new AppExitException(0));
        var interpreter = new OslangInterpreter(extensions);
        var ex = Assert.Throws<AppExitException>(() => interpreter.Execute(source, output));
        Assert.Equal(0, ex.ExitCode);
        Assert.Contains("Console OK", output.ToString());
    }

    [Fact]
    public void Console_KeyConstants_And_KeyObject()
    {
        var output = new StringWriter();
        var source = @"USING OSL.CONSOLE

FUNCTION MAIN()
    Key = OSL.CONSOLE.GETKEY()
    IF Key.KEY = KEYCODE.ESC THEN
        PRINT ""ESC""
    END
    IF Key.CHAR <> NULL THEN
        PRINT ""HasChar""
    END
    IF Key.CTRL THEN
        PRINT ""Ctrl""
    END
    IF Key.ALT THEN
        PRINT ""Alt""
    END
    IF Key.SHIFT THEN
        PRINT ""Shift""
    END
    PRINT ""KeyOK""
END FUNCTION";

        var extensions = new ExtensionRegistry();
        extensions.Register("CONSOLE.GETKEY", (args, location) => new KeyValue(new EnumValue(new NumberValue(2), "KEYCODE", "ESC"), null, false, false, false));
        var interpreter = new OslangInterpreter(extensions);
        interpreter.Execute(source, output, Console.In, Console.Clear, null);
        Assert.Contains("KeyOK", output.ToString());
    }

    [Fact]
    public void App_Exit_ThrowsAppExit()
    {
        var output = new StringWriter();
        var source = @"USING OSL.APP

FUNCTION MAIN()
    OSL.APP.EXIT(42)
END FUNCTION";

        var extensions = new ExtensionRegistry();
        extensions.Register("APP.EXIT", (args, location) => throw new AppExitException(42));
        var interpreter = new OslangInterpreter(extensions);
        Assert.Throws<AppExitException>(() => interpreter.Execute(source, output));
    }

    [Fact]
    public void File_ReadTextAndLines_Work()
    {
        var output = new StringWriter();
        var source = @"USING OSL.FILE

FUNCTION MAIN()
    Content = OSL.FILE.READTEXT(""test.txt"")
    PRINT Content
    Lines = OSL.FILE.READLINES(""test.txt"")
    PRINT COUNT(Lines)
    IF OSL.FILE.EXISTS(""test.txt"") THEN
        PRINT ""Exists""
    END
    OSL.FILE.WRITETEXT(""out.txt"", ""hello"")
    OSL.FILE.WRITELINES(""out.txt"", [""a"", ""b""])
    Size = OSL.FILE.SIZE(""test.txt"")
    PRINT Size
    OSL.FILE.DELETE(""out.txt"")
    OSL.FILE.RENAME(""out.txt"", ""renamed.txt"")
END FUNCTION";

        var extensions = new ExtensionRegistry();
        extensions.Register("FILE.READTEXT", (args, location) => new StringValue("hello world"));
        extensions.Register("FILE.READLINES", (args, location) => new ArrayValue([new StringValue("a"), new StringValue("b")], RuntimeType.String));
        extensions.Register("FILE.EXISTS", (args, location) => BooleanValue.True);
        extensions.Register("FILE.WRITETEXT", (args, location) => OslangValue.Null);
        extensions.Register("FILE.WRITELINES", (args, location) => OslangValue.Null);
        extensions.Register("FILE.SIZE", (args, location) => new NumberValue(11));
        extensions.Register("FILE.DELETE", (args, location) => OslangValue.Null);
        extensions.Register("FILE.RENAME", (args, location) => OslangValue.Null);
        var interpreter = new OslangInterpreter(extensions);
        interpreter.Execute(source, output, Console.In, Console.Clear, null);
        Assert.Contains("hello world", output.ToString());
        Assert.Contains("2", output.ToString());
        Assert.Contains("Exists", output.ToString());
    }

    [Fact]
    public void Args_GlobalVariable_IsPopulated()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    PRINT COUNT(ARGS)
    IF COUNT(ARGS) > 0 THEN
        PRINT ARGS[0]
    END
END FUNCTION";

        var extensions = new ExtensionRegistry();
        var interpreter = new OslangInterpreter(extensions);
        interpreter.Execute(source, output, Console.In, Console.Clear, null, [new StringValue("first"), new StringValue("second")]);
        Assert.Contains("2", output.ToString());
        Assert.Contains("first", output.ToString());
    }

    [Fact]
    public void String_LeftRightFindInsertRemove_Work()
    {
        var output = new StringWriter();
        var source = @"FUNCTION MAIN()
    S = ""Hello World""
    PRINT S.LEFT(5)
    PRINT S.RIGHT(5)
    PRINT S.FIND(""World"")
    S = S.INSERT(6, ""Beautiful "")
    PRINT S
    S = S.REMOVE(6, 10)
    PRINT S
END FUNCTION";

        Execute(source, output);
        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Hello", lines[0]);
        Assert.Equal("World", lines[1]);
        Assert.Equal("6", lines[2]);
        Assert.Equal("Hello Beautiful World", lines[3]);
        Assert.Equal("Hello World", lines[4]);
    }
}
