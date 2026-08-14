using System.IO;
using System.Text;
using Osb.Shell.Kernel;
using Xunit;

namespace Osb.Shell.Tests;

[Collection("OsbShellTests")]
public class OshScriptTests
{
    private readonly string _uniqueUser = "testuser-" + Guid.NewGuid().ToString("N");
    private string UniqueUser => _uniqueUser;
    [Fact]
    public void RunFile_ExecutesSetAndCommands()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "SET MY_VAR=42",
                "SET EMPTY=",
                "REM this is a comment",
                "' another comment",
                "; another comment",
                "",
                "PRINT done"
            }, Encoding.UTF8);

            var env = new OsbEnvironment();
            env.SetCurrentUsername(UniqueUser);
            var shell = new OsbShell(env);
            var script = new OshScript(env, shell);

            var output = new StringWriter();
            Console.SetOut(output);

            script.RunFile(tempFile);

            var result = output.ToString();
            Assert.Contains("done", result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void RunFile_ReportsMissingFile()
    {
        var env = new OsbEnvironment();
        env.SetCurrentUsername(UniqueUser);
        var shell = new OsbShell(env);
        var script = new OshScript(env, shell);

        var output = new StringWriter();
        Console.SetOut(output);

        script.RunFile("/nonexistent/path/script.osh");

        var result = output.ToString();
        Assert.Contains("Script não encontrado", result);
    }

    [Fact]
    public void RunFile_ExpandsVariablesInCommands()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "SET MSG=HelloWorld",
                "PRINT %MSG%"
            }, Encoding.UTF8);

            var env = new OsbEnvironment();
            env.SetCurrentUsername(UniqueUser);
            var shell = new OsbShell(env);
            var script = new OshScript(env, shell);

            var output = new StringWriter();
            Console.SetOut(output);

            script.RunFile(tempFile);

            var result = output.ToString();
            Assert.Contains("HelloWorld", result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void RunFile_ExpandsPositionalParametersInSet()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "SET MSG=%1%",
                "PRINT %MSG%"
            }, Encoding.UTF8);

            var env = new OsbEnvironment();
            env.SetCurrentUsername(UniqueUser);
            var shell = new OsbShell(env);
            var script = new OshScript(env, shell);

            var output = new StringWriter();
            Console.SetOut(output);

            script.RunFile(tempFile, new[] { "hello" });

            var result = output.ToString();
            Assert.Contains("hello", result);
            Assert.DoesNotContain("%1%", result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void RunFile_IgnoresEmptyLinesAndComments()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "",
                "   ",
                "REM full line comment",
                "' apostrophe comment",
                "; semicolon comment",
                "SET X=1",
                "",
                "PRINT X is set"
            }, Encoding.UTF8);

            var env = new OsbEnvironment();
            env.SetCurrentUsername(UniqueUser);
            var shell = new OsbShell(env);
            var script = new OshScript(env, shell);

            var output = new StringWriter();
            Console.SetOut(output);

            script.RunFile(tempFile);

            var result = output.ToString();
            Assert.Contains("X is set", result);
            Assert.DoesNotContain("REM", result);
            Assert.DoesNotContain("' apostrophe", result);
            Assert.DoesNotContain("; semicolon", result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void RunFile_WithArgs_ExposesPositionalParameters()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "PRINT %1%",
                "PRINT %2%"
            }, Encoding.UTF8);

            var env = new OsbEnvironment();
            env.SetCurrentUsername(UniqueUser);
            var shell = new OsbShell(env);
            var script = new OshScript(env, shell);

            var output = new StringWriter();
            Console.SetOut(output);

            script.RunFile(tempFile, new[] { "alpha", "beta" });

            var result = output.ToString();
            Assert.Contains("alpha", result);
            Assert.Contains("beta", result);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void RunFile_PositionalParameters_AreRestoredAfterRun()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[]
            {
                "PRINT %1%"
            }, Encoding.UTF8);

            var env = new OsbEnvironment();
            env.SetCurrentUsername(UniqueUser);
            env.Variables.Set(UniqueUser, "1", "preset");
            var shell = new OsbShell(env);
            var script = new OshScript(env, shell);

            var output = new StringWriter();
            Console.SetOut(output);

            script.RunFile(tempFile, new[] { "runtime" });

            var result = output.ToString();
            Assert.Contains("runtime", result);

            var after = env.Variables.GetForUser(UniqueUser);
            Assert.Equal("preset", after["1"]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void RunFile_ExecutesDotExternalCommands()
    {
        var tempFile = Path.GetTempFileName();
        var marker = Path.Combine(Path.GetTempPath(), "osb-osh-dot-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(tempFile, $". touch {marker}\n", Encoding.UTF8);

            var env = new OsbEnvironment();
            env.SetCurrentUsername(UniqueUser);
            var shell = new OsbShell(env);
            var script = new OshScript(env, shell);

            script.RunFile(tempFile);

            Assert.True(File.Exists(marker), "External dot command did not create expected marker file.");
        }
        finally
        {
            File.Delete(tempFile);
            if (File.Exists(marker))
            {
                File.Delete(marker);
            }
        }
    }

    [Fact]
    public void Execute_RecognizesOshFileAndRunsIt()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "script.osh");
        try
        {
            File.WriteAllText(tempFile, "SET INLINE=yes\nPRINT %INLINE%\n", Encoding.UTF8);

            var env = new OsbEnvironment();
            env.SetCurrentUsername(UniqueUser);
            var shell = new OsbShell(env);

            var output = new StringWriter();
            Console.SetOut(output);

            shell.Execute(tempFile);

            var result = output.ToString();
            Assert.Contains("yes", result);
        }
        finally
        {
            File.Delete(tempFile);
            Directory.Delete(tempDir);
        }
    }

    [Fact]
    public void Execute_RecognizesBareOshFilenameAndRunsIt()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "script.osh");
        try
        {
            File.WriteAllText(tempFile, "SET INLINE=bare\nPRINT %INLINE%\n", Encoding.UTF8);

            var env = new OsbEnvironment();
            env.SetCurrentUsername(UniqueUser);
            var shell = new OsbShell(env);

            var output = new StringWriter();
            Console.SetOut(output);

            var originalDir = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(tempDir);
                shell.Execute("script.osh");
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDir);
            }

            var result = output.ToString();
            Assert.Contains("bare", result);
        }
        finally
        {
            File.Delete(tempFile);
            Directory.Delete(tempDir);
        }
    }

    [Fact]
    public void Execute_RecognizesOshWithArgs_AndExpandsPositional()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempFile = Path.Combine(tempDir, "script.osh");
        try
        {
            File.WriteAllText(tempFile, "SET MSG=%1%\nPRINT %MSG%\n", Encoding.UTF8);

            var env = new OsbEnvironment();
            env.SetCurrentUsername(UniqueUser);
            var shell = new OsbShell(env);

            var output = new StringWriter();
            Console.SetOut(output);

            var originalDir = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(tempDir);
                shell.Execute("script.osh \"hello world\"");
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDir);
            }

            var result = output.ToString();
            Assert.Contains("hello world", result);
        }
        finally
        {
            File.Delete(tempFile);
            Directory.Delete(tempDir);
        }
    }
}
