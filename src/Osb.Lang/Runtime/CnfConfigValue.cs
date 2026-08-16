namespace Osb.Lang.Runtime;

/// <summary>
/// OSLANG 0.62 CNF config value wrapper.
/// </summary>
public sealed class CnfConfigValue(string? path, Dictionary<string, string> data) : OslangValue
{
    public string? Path { get; } = path;
    public Dictionary<string, string> Data { get; } = data;
    public override RuntimeType Type => RuntimeType.Object;
    public override string TypeName => "CNFCONFIG";

    public override string ToString()
    {
        return $"CNFCONFIG({Data.Count} entries)";
    }
}
