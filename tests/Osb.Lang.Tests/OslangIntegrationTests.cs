using System;
using System.IO;
using System.Text;
using Osb.Lang;
using Osb.Lang.Diagnostics;
using Osb.Lang.Extensibility;
using Osb.Lang.Runtime;
using Xunit;

namespace Osb.Lang.Tests;

public class OslangIntegrationTests
{
    private OslangValue Execute(string source, StringWriter? output = null, TextReader? input = null, string? basePath = null, ExtensionRegistry? extensions = null)
    {
        var interpreter = new OslangInterpreter(extensions);
        return interpreter.Execute(source, output ?? new StringWriter(), input: input, basePath: basePath);
    }

    private static void RequireArgCount(IReadOnlyList<OslangValue> args, int expected, string fn, SourceLocation location)
    {
        if (args.Count != expected)
        {
            throw new OslangRuntimeException(location, $"{fn}() expects {expected} argument(s), got {args.Count}.");
        }
    }

    private static string RequireStringArg(IReadOnlyList<OslangValue> args, int index, string fn, SourceLocation location)
    {
        if (args[index] is not StringValue sv)
        {
            throw new OslangRuntimeException(location, $"{fn}() expects STRING at argument {index + 1}.");
        }
        return sv.Value;
    }

    [Fact]
    public void CompleteInteractiveApp_SimulatesMainLoop()
    {
        var output = new StringWriter();
        var source = @"
USING OSL.CONSOLE

CLASS APP
    PRIVATE VAR RunFlag BOOLEAN
    PRIVATE VAR StepCount NUMBER

    PUBLIC FUNCTION RUN()
        RunFlag = TRUE
        StepCount = 0

        OSL.CONSOLE.ENTER()
        OSL.CONSOLE.ALTERNATE(TRUE)
        OSL.CONSOLE.HIDECURSOR()

        WHILE RunFlag
            RENDER()
        END

        OSL.CONSOLE.SHOWCURSOR()
        OSL.CONSOLE.ALTERNATE(FALSE)
        OSL.CONSOLE.EXIT()
    END

    PRIVATE FUNCTION RENDER()
        StepCount = StepCount + 1
        IF StepCount >= 3 THEN
            RunFlag = FALSE
        END
    END
END CLASS

FUNCTION MAIN()
    APP = NEW APP()
    APP.RUN()
END FUNCTION";

        var extensions = new ExtensionRegistry();
        extensions.Register("CONSOLE.ENTER", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.EXIT", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.ALTERNATE", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.HIDECURSOR", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.SHOWCURSOR", (args, location) => OslangValue.Null);

        var ex = Record.Exception(() => Execute(source, output, extensions: extensions));
        Assert.Null(ex);
        Assert.Equal("", output.ToString());
    }

    [Fact]
    public void ObjectOrientedWorkflow_EncapsulatesStateAndBehavior()
    {
        var output = new StringWriter();
        var source = @"
CLASS COUNTER
    PRIVATE VAR VALUE

    PUBLIC FUNCTION INIT()
        VALUE = 0
    END

    PUBLIC FUNCTION INCREMENT()
        VALUE = VALUE + 1
    END

    PUBLIC FUNCTION GET()
        RETURN VALUE
    END
END CLASS

FUNCTION MAIN()
    C = NEW COUNTER()
    C.INIT()
    C.INCREMENT()
    C.INCREMENT()
    PRINT C.GET()
END FUNCTION";

        Execute(source, output);
        Assert.Equal("2" + Environment.NewLine, output.ToString());
    }

    [Fact]
    public void ErrorHandling_RecoverableRuntimeError_DoesNotCrash()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    TRY
        PRINT 1 / 0
    CATCH ERR
        PRINT ""Recovered""
    END
    PRINT ""Alive""
END FUNCTION";

        Execute(source, output);
        Assert.Contains("Recovered", output.ToString());
        Assert.Contains("Alive", output.ToString());
    }

    [Fact]
    public void ModuleSystem_ResolvesUsingAcrossFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "osb-integration-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "utils.osl"), @"
FUNCTION HELP()
    PRINT ""Helped""
END FUNCTION
", Encoding.UTF8);

            var output = new StringWriter();
            var source = $@"USING utils

FUNCTION MAIN()
    HELP()
END FUNCTION";

            Execute(source, output, basePath: tempDir);
            Assert.Contains("Helped", output.ToString());
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ComplexDataFlow_ArraysObjectsAndFunctions()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION SUM(A, B)
    RETURN A + B
END FUNCTION

FUNCTION MAIN()
    ITEMS = [1, 2, 3, 4, 5]
    TOTAL = 0
    FOR I = 0 TO COUNT(ITEMS) - 1
        TOTAL = TOTAL + ITEMS[I]
    END
    PRINT TOTAL
    PRINT SUM(10, 20)
END FUNCTION";

        Execute(source, output);
        var result = output.ToString();
        Assert.Contains("15", result);
        Assert.Contains("30", result);
    }

    [Fact]
    public void KeyboardEventFlow_ProcessesSpecialKeys()
    {
        var output = new StringWriter();
        var source = @"
USING OSL.CONSOLE

FUNCTION MAIN()
    PRINT ""Ready""
END FUNCTION";

        var extensions = new ExtensionRegistry();
        extensions.Register("CONSOLE.ENTER", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.EXIT", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.ALTERNATE", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.HIDECURSOR", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.SHOWCURSOR", (args, location) => OslangValue.Null);

        Execute(source, output, extensions: extensions);
        Assert.Contains("Ready", output.ToString());
    }

    [Fact]
    public void TerminalApp_ClearsAndWritesFrames()
    {
        var output = new StringWriter();
        var source = @"
USING OSL.CONSOLE

FUNCTION MAIN()
    OSL.CONSOLE.ENTER()
    OSL.CONSOLE.ALTERNATE(TRUE)
    OSL.CONSOLE.BEGINFRAME()
    OSL.CONSOLE.CLEAR()
    OSL.CONSOLE.WRITE(1, 1, ""Hello"")
    OSL.CONSOLE.ENDFRAME()
    OSL.CONSOLE.FLUSH()
    OSL.CONSOLE.ALTERNATE(FALSE)
    OSL.CONSOLE.EXIT()
    PRINT ""Done""
END FUNCTION";

        var extensions = new ExtensionRegistry();
        extensions.Register("CONSOLE.ENTER", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.EXIT", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.ALTERNATE", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.BEGINFRAME", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.ENDFRAME", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.FLUSH", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.CLEAR", (args, location) => OslangValue.Null);
        extensions.Register("CONSOLE.WRITE", (args, location) => OslangValue.Null);

        Execute(source, output, extensions: extensions);
        Assert.Contains("Done", output.ToString());
    }

    [Fact]
    public void EnumFlags_CombinesAndTestsMembers()
    {
        var output = new StringWriter();
        var source = @"
ENUM PERMISSION
    READ
    WRITE
    EXECUTE
END

FUNCTION MAIN()
    P = PERMISSION.READ | PERMISSION.WRITE
    PRINT P.CONTAINS(PERMISSION.READ)
    PRINT P.CONTAINS(PERMISSION.EXECUTE)
END FUNCTION";

        Execute(source, output);
        var result = output.ToString();
        Assert.Contains("TRUE", result);
        Assert.Contains("FALSE", result);
    }

    [Fact]
    public void StringAndArrayMethods_ProcessesCollectionData()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    NAME = ""  OSB System  ""
    PRINT TRIM(NAME)
    PRINT TOUPPER(NAME)
    PRINT TOLOWER(NAME)

    ITEMS = [""a"", ""b"", ""c""]
    PRINT ITEMS.JOIN(""-"")
    PRINT COUNT(ITEMS)
END FUNCTION";

        Execute(source, output);
        var result = output.ToString();
        Assert.Contains("OSB System", result);
        Assert.Contains("osb system", result);
        Assert.Contains("a-b-c", result);
        Assert.Contains("3", result);
    }

    [Fact]
    public void ControlFlow_SwitchWithEnumAndStrings()
    {
        var output = new StringWriter();
        var source = @"
ENUM STATUS
    READY
    BUSY
END

FUNCTION MAIN()
    S = STATUS.READY
    SWITCH S
        CASE STATUS.READY
            PRINT ""Ready""
        CASE STATUS.BUSY
            PRINT ""Busy""
    END

    PRINT TYPEOF(S)
END FUNCTION";

        Execute(source, output);
        var result = output.ToString();
        Assert.Contains("Ready", result);
        Assert.Contains("STATUS", result);
    }

    [Fact]
    public void RecursiveFunction_ComputesFactorial()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION FACT(N)
    IF N <= 1 THEN
        RETURN 1
    END
    RETURN N * FACT(N - 1)
END FUNCTION

FUNCTION MAIN()
    PRINT FACT(5)
    PRINT FACT(10)
END FUNCTION";

        Execute(source, output);
        var result = output.ToString();
        Assert.Contains("120", result);
        Assert.Contains("3628800", result);
    }

    [Fact]
    public void MultipleClasses_InheritanceAndPolymorphism()
    {
        var output = new StringWriter();
        var source = @"
CLASS ANIMAL
    PUBLIC VAR NAME

    PUBLIC FUNCTION SPEAK()
        PRINT NAME + "" makes a sound""
    END
END CLASS

CLASS DOG: ANIMAL
    PUBLIC FUNCTION SPEAK()
        PRINT NAME + "" barks""
    END
END CLASS

FUNCTION MAIN()
    A = NEW ANIMAL()
    A.NAME = ""Generic""
    A.SPEAK()

    D = NEW DOG()
    D.NAME = ""Rex""
    D.SPEAK()
END FUNCTION";

        Execute(source, output);
        var result = output.ToString();
        Assert.Contains("Generic makes a sound", result);
        Assert.Contains("Rex barks", result);
    }

    [Fact]
    public void I18nIntegration_LoadsAndUsesTranslations()
    {
        var output = new StringWriter();
        var source = @"
USING OSL.I18N

FUNCTION MAIN()
    KEYS = I18N.KEYS()
    PRINT COUNT(KEYS)
    PRINT I18N.GET(""boot.starting"")
END FUNCTION";

        Execute(source, output, basePath: AppContext.BaseDirectory);
        var result = output.ToString();
        Assert.Contains("boot.starting", result);
    }

    [Fact]
    public void FileOperations_ReadsAndWritesText()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "osb-file-integration-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            var filePath = Path.Combine(tempDir, "test.txt");

            var extensions = new ExtensionRegistry();
            extensions.Register("FILE.WRITE", (args, location) =>
            {
                RequireArgCount(args, 2, "FILE.WRITE", location);
                var path = RequireStringArg(args, 0, "FILE.WRITE", location);
                var text = RequireStringArg(args, 1, "FILE.WRITE", location);
                File.WriteAllText(path, text);
                return OslangValue.Null;
            });
            extensions.Register("FILE.READ", (args, location) =>
            {
                RequireArgCount(args, 1, "FILE.READ", location);
                var path = RequireStringArg(args, 0, "FILE.READ", location);
                return new StringValue(File.ReadAllText(path));
            });

            var output = new StringWriter();
            var source = $@"USING OSL.FILE

FUNCTION MAIN()
    FILE.WRITE(""{filePath}"", ""Hello OSB"")
    CONTENT = FILE.READ(""{filePath}"")
    PRINT CONTENT
END FUNCTION";

            Execute(source, output, basePath: tempDir, extensions: extensions);
            Assert.Contains("Hello OSB", output.ToString());
            Assert.True(File.Exists(filePath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void SemanticError_ReportsCompileTimeFailure()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    PRINT UNDEFINED_VARIABLE
END FUNCTION";

        Assert.Throws<OslangRuntimeException>(() => Execute(source, output));
    }

    [Fact]
    public void RuntimeError_ReportsDivideByZero()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    PRINT 1 / 0
END FUNCTION";

        Assert.Throws<OslangRuntimeException>(() => Execute(source, output));
    }

    [Fact]
    public void LongRunningLoop_CompletesWithinReasonableTime()
    {
        var output = new StringWriter();
        var source = @"
FUNCTION MAIN()
    SUM = 0
    FOR I = 1 TO 1000
        SUM = SUM + I
    END
    PRINT SUM
END FUNCTION";

        var start = DateTime.UtcNow;
        Execute(source, output);
        var elapsed = DateTime.UtcNow - start;
        Assert.Contains("500500", output.ToString());
        Assert.True(elapsed.TotalSeconds < 5, $"Loop took too long: {elapsed.TotalSeconds}s");
    }
}
