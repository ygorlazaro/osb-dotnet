namespace Osb.Shell.Kernel;

public class PromptConfig
{
    public const string DefaultLayout = "%pwd%br%user@%hostname [@] ";
    public string Layout { get; set; } = DefaultLayout;

    public static string FilePath(string homeDir) => Path.Combine(homeDir, "CONF", "PROMPT.CFG");

    public static PromptConfig Load(string homeDir)
    {
        var path = FilePath(homeDir);
        try
        {
            if (File.Exists(path))
            {
                var firstLine = File.ReadAllLines(path).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(firstLine))
                {
                    return new PromptConfig { Layout = firstLine.Trim() };
                }
            }
        }
        catch
        {
        }

        return new PromptConfig();
    }

    public void Save(string homeDir)
    {
        try
        {
            var path = FilePath(homeDir);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Layout + Environment.NewLine);
        }
        catch
        {
        }
    }
}
