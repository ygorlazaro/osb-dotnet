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
    private OslangValue Execute(string source, StringWriter? output = null)
    {
        var interpreter = new OslangInterpreter();
        return interpreter.Execute(source, output ?? new StringWriter());
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
    public void BaseCall_OutsideConstructor_ThrowsRuntimeError()
    {
        var source = @"
CLASS BaseClass

END


CLASS Derived: BaseClass

    PUBLIC TEST()

        BASE()

    END

END


FUNCTION MAIN()

    D = NEW Derived()
    D.TEST()

END FUNCTION";

        var ex = Assert.Throws<OslangRuntimeException>(() => Execute(source));
        Assert.Contains("BASE can only be used inside a constructor", ex.Message);
    }
}

