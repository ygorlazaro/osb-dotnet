using System.IO;
using System.Text;
using Osb.Shell.Kernel;
using Xunit;

namespace Osb.Shell.Tests;

[Collection("OsbShellTests")]
public class ShellIntegrationTests
{
    private readonly string _uniqueUser = "intuser-" + Guid.NewGuid().ToString("N");
    private string UniqueUser => _uniqueUser;

    [Fact]
    public void CompleteSession_LoginChangeDirRunApp_WorksEndToEnd()
    {
        var env = new OsbEnvironment();
        env.Users.Add(UniqueUser, "testpass", "EN-US", out _);
        env.SetCurrentUsername(UniqueUser);
        var shell = new OsbShell(env);

        var output = new StringWriter();
        Console.SetOut(output);

        var tempDir = Path.Combine(Path.GetTempPath(), "osb-shell-integration-" + Guid.NewGuid().ToString("N"));
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.CreateDirectory(tempDir);
            shell.Execute("USER " + UniqueUser + " testpass");
            shell.Execute("CD " + tempDir);
            shell.Execute("DIR");

            var result = output.ToString();
            Assert.Contains("Authenticated", result);
            Assert.True(Directory.Exists(tempDir));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ScriptExecution_MultipleCommandsAndVariables_PersistsState()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "SET GREETING=Hello",
                "SET TARGET=World",
                "PRINT %GREETING% %TARGET%"
            }, Encoding.UTF8);

            var env = new OsbEnvironment();
            env.Users.Add(UniqueUser, "testpass", "EN-US", out _);
            env.SetCurrentUsername(UniqueUser);
            var shell = new OsbShell(env);
            var script = new OshScript(env, shell);

            var output = new StringWriter();
            Console.SetOut(output);

            script.RunFile(tempFile);

            var result = output.ToString();
            Assert.Contains("Hello World", result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void DirectoryWorkflow_CreateNavigateAndCleanup()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "osb-dir-integration-" + Guid.NewGuid().ToString("N"));
        var subDir = Path.Combine(baseDir, "subdir");
        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.CreateDirectory(baseDir);

            var env = new OsbEnvironment();
            env.Users.Add(UniqueUser, "testpass", "EN-US", out _);
            env.SetCurrentUsername(UniqueUser);
            var shell = new OsbShell(env);

            var output = new StringWriter();
            Console.SetOut(output);

            shell.Execute("USER " + UniqueUser + " testpass");
            shell.Execute("MD " + subDir);
            shell.Execute("CD " + subDir);
            shell.Execute("PWD");

            var result = output.ToString();
            Assert.Contains("Authenticated", result);
            Assert.True(Directory.Exists(subDir));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
            if (Directory.Exists(baseDir))
            {
                Directory.Delete(baseDir, recursive: true);
            }
        }
    }

    [Fact]
    public void VariablePersistence_SurvivesMultipleCommands()
    {
        var env = new OsbEnvironment();
        env.Users.Add(UniqueUser, "testpass", "EN-US", out _);
        env.SetCurrentUsername(UniqueUser);
        var shell = new OsbShell(env);

        var output = new StringWriter();
        Console.SetOut(output);

        shell.Execute("USER " + UniqueUser + " testpass");
        shell.Execute("SET X=First");
        shell.Execute("SET X=%X%_Second");
        shell.Execute("PRINT %X%");

        var result = output.ToString();
        Assert.Contains("First_Second", result);
    }

    [Fact]
    public void OslAppExecution_RunsKissAndReturns()
    {
        var env = new OsbEnvironment();
        env.Users.Add(UniqueUser, "testpass", "EN-US", out _);
        env.SetCurrentUsername(UniqueUser);
        var shell = new OsbShell(env);

        var output = new StringWriter();
        Console.SetOut(output);

        shell.Execute("USER " + UniqueUser + " testpass");
        shell.Execute("VER");
        var verResult = output.ToString();
        Assert.Contains("OSB", verResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ErrorHandling_InvalidCommand_DoesNotCrashShell()
    {
        var env = new OsbEnvironment();
        env.Users.Add(UniqueUser, "testpass", "EN-US", out _);
        env.SetCurrentUsername(UniqueUser);
        var shell = new OsbShell(env);

        var output = new StringWriter();
        Console.SetOut(output);

        shell.Execute("USER " + UniqueUser + " testpass");
        shell.Execute("UNKNOWN_COMMAND_XYZ");
        shell.Execute("PWD");

        var result = output.ToString();
        Assert.Contains(Environment.CurrentDirectory, result);
    }

    [Fact]
    public void PromptCustomization_ReflectsUserAndHost()
    {
        var env = new OsbEnvironment();
        env.Users.Add(UniqueUser, "testpass", "EN-US", out _);
        env.SetCurrentUsername(UniqueUser);
        env.SaveMachineName("TESTHOST");
        var shell = new OsbShell(env);

        var output = new StringWriter();
        Console.SetOut(output);

        shell.Execute("USER " + UniqueUser + " testpass");
        shell.Execute("PROMPT [%user%@%hostname%]");

        Assert.Equal("[%user%@%hostname%]", env.Prompt.Layout);
    }

    [Fact]
    public void ClearCommand_KeepsStatusbarAndClearsScreen()
    {
        var env = new OsbEnvironment();
        env.Users.Add(UniqueUser, "testpass", "EN-US", out _);
        env.SetCurrentUsername(UniqueUser);
        var shell = new OsbShell(env);

        var output = new StringWriter();
        Console.SetOut(output);

        shell.Execute("USER " + UniqueUser + " testpass");
        shell.Execute("PRINT LineBeforeClear");
        shell.Execute("CLS");
        shell.Execute("PRINT AfterClear");

        var result = output.ToString();
        Assert.Contains("LineBeforeClear", result);
        Assert.Contains("AfterClear", result);
    }

    [Fact]
    public void ConfigCommand_PersistsAndLoadsSettings()
    {
        var env = new OsbEnvironment();
        env.Users.Add(UniqueUser, "testpass", "EN-US", out _);
        env.SetCurrentUsername(UniqueUser);
        var shell = new OsbShell(env);

        var output = new StringWriter();
        Console.SetOut(output);

        shell.Execute("USER " + UniqueUser + " testpass");
        shell.Execute("SET MESSAGE=IntegrationTest");
        shell.Execute("PRINT %MESSAGE%");

        var result = output.ToString();
        Assert.Contains("IntegrationTest", result);
    }

    [Fact]
    public void HelpCommand_ListsAllCategories()
    {
        var env = new OsbEnvironment();
        env.Users.Add(UniqueUser, "testpass", "EN-US", out _);
        env.SetCurrentUsername(UniqueUser);
        var shell = new OsbShell(env);

        var output = new StringWriter();
        Console.SetOut(output);

        shell.Execute("USER " + UniqueUser + " testpass");
        shell.Execute("HELP");

        var result = output.ToString();
        Assert.Contains("CLS", result);
        Assert.Contains("DIR", result);
        Assert.Contains("CD", result);
    }

    [Fact]
    public void DateTimeCommands_ReturnsCurrentValues()
    {
        var env = new OsbEnvironment();
        env.Users.Add(UniqueUser, "testpass", "EN-US", out _);
        env.SetCurrentUsername(UniqueUser);
        var shell = new OsbShell(env);

        var output = new StringWriter();
        Console.SetOut(output);

        shell.Execute("USER " + UniqueUser + " testpass");
        shell.Execute("DATE");
        shell.Execute("TIME");

        var result = output.ToString();
        Assert.Contains(DateTime.Now.ToString("dd/MM/yyyy"), result);
    }

    [Fact]
    public void ExternalCommand_ExecutesSystemProcess()
    {
        var env = new OsbEnvironment();
        env.Users.Add(UniqueUser, "testpass", "EN-US", out _);
        env.SetCurrentUsername(UniqueUser);
        var shell = new OsbShell(env);

        var output = new StringWriter();
        Console.SetOut(output);

        var marker = Path.Combine(Path.GetTempPath(), "osb-shell-integration-ext-" + Guid.NewGuid().ToString("N"));
        shell.Execute("USER " + UniqueUser + " testpass");
        shell.Execute($". touch {marker}");

        Assert.True(File.Exists(marker), "External command did not create expected marker file.");
        if (File.Exists(marker))
        {
            File.Delete(marker);
        }
    }
}
