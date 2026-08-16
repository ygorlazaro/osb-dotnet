namespace Osb.Lang.Runtime;

/// <summary>
/// OSLANG 0.62 JSON object value wrapper.
/// </summary>
public sealed class JsonObjectValue(Dictionary<string, OslangValue> data) : OslangValue
{
    public Dictionary<string, OslangValue> Data { get; } = new Dictionary<string, OslangValue>(data, StringComparer.OrdinalIgnoreCase);
    public override RuntimeType Type => RuntimeType.Object;
    public override string TypeName => "JSONOBJECT";

    public override string ToString()
    {
        return $"JSONOBJECT({Data.Count} entries)";
    }
}
