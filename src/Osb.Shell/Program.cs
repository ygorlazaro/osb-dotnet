using Osb.Shell.Kernel;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.CancelKeyPress += (_, e) => { e.Cancel = true; };

var debugMode = Environment.GetCommandLineArgs().Contains("--debug");

var env = new OsbEnvironment(debugMode, Path.Combine(AppContext.BaseDirectory, ".osb"));
var shell = new OsbShell(env, debugMode);

var argsList = Environment.GetCommandLineArgs().Skip(1).ToArray();
if (argsList.Length > 0 && !debugMode)
{
    var scriptPath = Path.GetFullPath(argsList[0]);
    if (File.Exists(scriptPath))
    {
        var runner = new OshScript(env, shell);
        var scriptArgs = argsList.Length > 1 ? argsList[1..] : Array.Empty<string>();
        runner.RunFile(scriptPath, scriptArgs);
        return;
    }
}

BootSequence.Run(env, shell);
shell.Run();
