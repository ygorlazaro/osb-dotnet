using Osb.Lang.Ast;
using Osb.Lang.Compilation;
using Osb.Lang.Diagnostics;
using Osb.Lang.Extensibility;
using Osb.Lang.Lexing;
using Osb.Lang.Parsing;
using Osb.Lang.Runtime;

namespace Osb.Lang.Runtime;

internal sealed partial class Interpreter
{
    private static bool ValuesEqual(OslangValue left, OslangValue right)
    {
        // seção 20: NULL só é igual a NULL.
        if (left.Type == RuntimeType.Null || right.Type == RuntimeType.Null)
        {
            return left.Type == RuntimeType.Null && right.Type == RuntimeType.Null;
        }

        if (left.Type != right.Type)
        {
            return false;
        }

        return (left, right) switch
        {
            (NumberValue a, NumberValue b) => a.Value == b.Value,
            (StringValue a, StringValue b) => a.Value == b.Value,
            (BooleanValue a, BooleanValue b) => a.Value == b.Value,
            (ArrayValue a, ArrayValue b) => ReferenceEquals(a, b),
            (ObjectValue a, ObjectValue b) => ReferenceEquals(a.Instance, b.Instance),
            (EnumValue a, EnumValue b) => a.Equals(b),
            _ => false,
        };
    }

    // ============================================================
    // OSLANG 0.2 - Orientação a objetos
    // ============================================================
}
