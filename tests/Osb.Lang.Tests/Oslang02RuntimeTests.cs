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
    private OslangValue Execute(string source, StringWriter? output = null, string? basePath = null)
    {
        var interpreter = new OslangInterpreter();
        return interpreter.Execute(source, output ?? new StringWriter(), basePath: basePath);
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
}

