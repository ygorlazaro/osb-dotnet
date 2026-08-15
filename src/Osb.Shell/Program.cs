using Osb.Shell.Kernel;

// OSB - Operating System Basic
// Shell de linha de comando multiplataforma.

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.CancelKeyPress += (_, e) => { e.Cancel = true; };

var env = new OsbEnvironment();
var shell = new OsbShell(env);

var argsList = Environment.GetCommandLineArgs().Skip(1).ToArray();
if (argsList.Length > 0)
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
