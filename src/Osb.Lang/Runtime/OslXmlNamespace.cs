using System.IO;
using System.Text;
using System.Xml;
using Osb.Lang.Diagnostics;

namespace Osb.Lang.Runtime;

/// <summary>
/// OSLANG 0.62 OSL.XML standard library implementation.
/// </summary>
public static class OslXmlNamespace
{
    public static OslangValue Call(string methodName, IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        var upper = methodName.ToUpperInvariant();
        return upper switch
        {
            "PARSE" => Parse(args, location),
            "STRINGIFY" => Stringify(args, location),
            "READ" => Read(args, location),
            "WRITE" => Write(args, location),
            "NAME" => Name(args, location),
            "VALUE" => Value(args, location),
            "ATTRIBUTES" => Attributes(args, location),
            "CHILDREN" => Children(args, location),
            "CHILD" => Child(args, location),
            "HAS" => Has(args, location),
            _ => throw new OslangRuntimeException(location, $"Unknown OSL.XML method '{methodName}'."),
        };
    }

    private static OslangValue Parse(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 1)
        {
            throw new OslangRuntimeException(location, "XML.PARSE() expects exactly 1 argument (XML text).");
        }
        if (args[0] is not StringValue sv)
        {
            throw new OslangRuntimeException(location, "XML.PARSE() expects a STRING argument.");
        }

        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(sv.Value);
            return ParseXmlElement(doc.DocumentElement!);
        }
        catch (XmlException ex)
        {
            throw new OslangRuntimeException(location, $"XML.PARSE() failed: {ex.Message}");
        }
    }

    private static XmlNodeValue ParseXmlElement(XmlElement element)
    {
        var name = element.Name;
        var value = string.IsNullOrEmpty(element.InnerText) ? null : element.InnerText;
        var attributes = new Dictionary<string, string>();
        foreach (XmlAttribute attr in element.Attributes)
        {
            attributes[attr.Name] = attr.Value;
        }
        var children = new List<XmlNodeValue>();
        foreach (XmlNode child in element.ChildNodes)
        {
            if (child is XmlElement childElement)
            {
                children.Add(ParseXmlElement(childElement));
            }
        }
        return new XmlNodeValue(name, value, attributes, children);
    }

    private static OslangValue Stringify(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 1)
        {
            throw new OslangRuntimeException(location, "XML.STRINGIFY() expects exactly 1 argument (node).");
        }
        if (args[0] is not XmlNodeValue node)
        {
            throw new OslangRuntimeException(location, "XML.STRINGIFY() expects an XMLNODE argument.");
        }

        return new StringValue(StringifyXmlNode(node, 0));
    }

    private static string StringifyXmlNode(XmlNodeValue node, int indent)
    {
        var pad = new string(' ', indent);
        var attrs = "";
        foreach (var attr in node.Attributes)
        {
            attrs += $" {attr.Key}=\"{EscapeXml(attr.Value)}\"";
        }

        if (node.Children.Count == 0 && string.IsNullOrEmpty(node.Value))
        {
            return $"{pad}<{node.Name}{attrs} />";
        }

        if (node.Children.Count == 0)
        {
            return $"{pad}<{node.Name}{attrs}>{EscapeXml(node.Value)}</{node.Name}>";
        }

        var sb = new System.Text.StringBuilder();
        sb.Append($"{pad}<{node.Name}{attrs}>");
        if (!string.IsNullOrEmpty(node.Value))
        {
            sb.Append(EscapeXml(node.Value));
        }
        foreach (var child in node.Children)
        {
            sb.AppendLine();
            sb.Append(StringifyXmlNode(child, indent + 2));
        }
        sb.AppendLine();
        sb.Append($"{pad}</{node.Name}>");
        return sb.ToString();
    }

    private static string EscapeXml(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var ch in text)
        {
            switch (ch)
            {
                case '&': sb.Append("&"); break;
                case '<': sb.Append("<"); break;
                case '>': sb.Append(">"); break;
                case '"': sb.Append('"'); break;
                case '\'': sb.Append("'"); break;
                default: sb.Append(ch); break;
            }
        }
        return sb.ToString();
    }

    private static OslangValue Read(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 1)
        {
            throw new OslangRuntimeException(location, "XML.READ() expects exactly 1 argument (path).");
        }
        if (args[0] is not StringValue sv)
        {
            throw new OslangRuntimeException(location, "XML.READ() expects a STRING argument for path.");
        }

        if (!File.Exists(sv.Value))
        {
            throw new OslangRuntimeException(location, $"XML.READ() file not found: '{sv.Value}'.");
        }

        var text = File.ReadAllText(sv.Value);
        return Parse([new StringValue(text)], location);
    }

    private static OslangValue Write(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 2)
        {
            throw new OslangRuntimeException(location, "XML.WRITE() expects exactly 2 arguments (path, node).");
        }
        if (args[0] is not StringValue sv)
        {
            throw new OslangRuntimeException(location, "XML.WRITE() expects a STRING argument for path.");
        }
        if (args[1] is not XmlNodeValue node)
        {
            throw new OslangRuntimeException(location, "XML.WRITE() expects an XMLNODE argument.");
        }

        try
        {
            var xml = StringifyXmlNode(node, 0);
            File.WriteAllText(sv.Value, xml, Encoding.UTF8);
            return OslangValue.Null;
        }
        catch (Exception ex)
        {
            throw new OslangRuntimeException(location, $"XML.WRITE() failed: {ex.Message}");
        }
    }

    private static OslangValue Name(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 1)
        {
            throw new OslangRuntimeException(location, "XML.NAME() expects exactly 1 argument (node).");
        }
        if (args[0] is not XmlNodeValue node)
        {
            throw new OslangRuntimeException(location, "XML.NAME() expects an XMLNODE argument.");
        }

        return new StringValue(node.Name);
    }

    private static OslangValue Value(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 1)
        {
            throw new OslangRuntimeException(location, "XML.VALUE() expects exactly 1 argument (node).");
        }
        if (args[0] is not XmlNodeValue node)
        {
            throw new OslangRuntimeException(location, "XML.VALUE() expects an XMLNODE argument.");
        }

        return node.Value is null ? OslangValue.Null : new StringValue(node.Value);
    }

    private static OslangValue Attributes(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 1)
        {
            throw new OslangRuntimeException(location, "XML.ATTRIBUTES() expects exactly 1 argument (node).");
        }
        if (args[0] is not XmlNodeValue node)
        {
            throw new OslangRuntimeException(location, "XML.ATTRIBUTES() expects an XMLNODE argument.");
        }

        var data = new Dictionary<string, OslangValue>();
        foreach (var kvp in node.Attributes)
        {
            data[kvp.Key] = new StringValue(kvp.Value);
        }
        return new JsonObjectValue(data);
    }

    private static OslangValue Children(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 1)
        {
            throw new OslangRuntimeException(location, "XML.CHILDREN() expects exactly 1 argument (node).");
        }
        if (args[0] is not XmlNodeValue node)
        {
            throw new OslangRuntimeException(location, "XML.CHILDREN() expects an XMLNODE argument.");
        }

        return new JsonArrayValue(node.Children.Cast<OslangValue>().ToList());
    }

    private static OslangValue Child(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 2)
        {
            throw new OslangRuntimeException(location, "XML.CHILD() expects exactly 2 arguments (node, name).");
        }
        if (args[0] is not XmlNodeValue node)
        {
            throw new OslangRuntimeException(location, "XML.CHILD() expects an XMLNODE as first argument.");
        }
        if (args[1] is not StringValue sv)
        {
            throw new OslangRuntimeException(location, "XML.CHILD() expects a STRING argument for name.");
        }

        var child = node.Children.FirstOrDefault(c => c.Name.Equals(sv.Value, StringComparison.OrdinalIgnoreCase));
        return child is null ? OslangValue.Null : child;
    }

    private static OslangValue Has(IReadOnlyList<OslangValue> args, SourceLocation location)
    {
        if (args.Count != 2)
        {
            throw new OslangRuntimeException(location, "XML.HAS() expects exactly 2 arguments (node, name).");
        }
        if (args[0] is not XmlNodeValue node)
        {
            throw new OslangRuntimeException(location, "XML.HAS() expects an XMLNODE as first argument.");
        }
        if (args[1] is not StringValue sv)
        {
            throw new OslangRuntimeException(location, "XML.HAS() expects a STRING argument for name.");
        }

        return BooleanValue.Of(node.Children.Any(c => c.Name.Equals(sv.Value, StringComparison.OrdinalIgnoreCase)));
    }
}
