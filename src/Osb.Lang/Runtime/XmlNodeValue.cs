namespace Osb.Lang.Runtime;

/// <summary>
/// OSLANG 0.62 XML node value wrapper.
/// </summary>
public sealed class XmlNodeValue(string name, string? value, Dictionary<string, string> attributes, List<XmlNodeValue> children) : OslangValue
{
    public string Name { get; } = name;
    public string? Value { get; } = value;
    public Dictionary<string, string> Attributes { get; } = attributes;
    public List<XmlNodeValue> Children { get; } = children;
    public override RuntimeType Type => RuntimeType.Object;
    public override string TypeName => "XMLNODE";

    public override string ToString()
    {
        return $"<{Name}>...</{Name}>";
    }

    public OslangValue GetProperty(string name)
    {
        return name.ToUpperInvariant() switch
        {
            "NAME" => new StringValue(Name),
            "VALUE" => new StringValue(Value ?? ""),
            "ATTRIBUTES" => new JsonObjectValue(Attributes.ToDictionary(kv => kv.Key, kv => (OslangValue)new StringValue(kv.Value))),
            "CHILDREN" => new JsonArrayValue(Children.Select(c => (OslangValue)c).ToList()),
            _ => OslangValue.Null,
        };
    }
}
