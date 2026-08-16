namespace Osb.Lang.Runtime;

/// <summary>
/// OSLANG 0.62 JSON array value wrapper.
/// </summary>
public sealed class JsonArrayValue(List<OslangValue> items) : OslangValue
{
    public List<OslangValue> Items { get; } = items;
    public override RuntimeType Type => RuntimeType.Array;
    public override string TypeName => "JSONARRAY";

    public override string ToString()
    {
        return $"JSONARRAY({Items.Count} items)";
    }
}
