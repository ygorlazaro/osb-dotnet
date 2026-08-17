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
    private void ExecuteEnumDecl(EnumDecl e)
    {
        var members = new List<(string MemberName, OslangValue Value)>();
        var underlyingType = (RuntimeType?)null;

        foreach (var member in e.Members)
        {
            if (member.Value is null)
            {
                var index = members.Count;
                OslangValue value = underlyingType == RuntimeType.String
                    ? new StringValue(index.ToString())
                    : new NumberValue(index);
                members.Add((member.Name, value));
            }
            else
            {
                var evaluatedValue = Eval(member.Value, new Scope(_globals));
                if (underlyingType is null)
                {
                    underlyingType = evaluatedValue.Type;
                    if (underlyingType == RuntimeType.String)
                    {
                        for (var i = 0; i < members.Count; i++)
                        {
                            if (members[i].Value is NumberValue nv)
                            {
                                members[i] = (members[i].MemberName, new StringValue(nv.Value.ToString()));
                            }
                        }
                    }
                }
                else if (evaluatedValue.Type != underlyingType)
                {
                    throw new OslangRuntimeException(member.Location, $"Enum member '{member.Name}' has inconsistent type. Expected {underlyingType}, got {evaluatedValue.Type}.");
                }
                members.Add((member.Name, evaluatedValue));
            }
        }

        _enums[e.Name] = members;
        _enumTypes[e.Name] = new EnumTypeValue(e.Name);
    }


    private void RegisterBuiltinKeyEnum()
    {
        var members = new List<(string MemberName, OslangValue Value)>
        {
            ("UNKNOWN", new NumberValue(0)),
            ("ENTER", new NumberValue(1)),
            ("ESC", new NumberValue(2)),
            ("TAB", new NumberValue(3)),
            ("BACKSPACE", new NumberValue(4)),
            ("DELETE", new NumberValue(5)),
            ("INSERT", new NumberValue(6)),
            ("SPACE", new NumberValue(7)),
            ("UP", new NumberValue(8)),
            ("DOWN", new NumberValue(9)),
            ("LEFT", new NumberValue(10)),
            ("RIGHT", new NumberValue(11)),
            ("HOME", new NumberValue(12)),
            ("END", new NumberValue(13)),
            ("PAGEUP", new NumberValue(14)),
            ("PAGEDOWN", new NumberValue(15)),
            ("F1", new NumberValue(16)),
            ("F2", new NumberValue(17)),
            ("F3", new NumberValue(18)),
            ("F4", new NumberValue(19)),
            ("F5", new NumberValue(20)),
            ("F6", new NumberValue(21)),
            ("F7", new NumberValue(22)),
            ("F8", new NumberValue(23)),
            ("F9", new NumberValue(24)),
            ("F10", new NumberValue(25)),
            ("F11", new NumberValue(26)),
            ("F12", new NumberValue(27)),
        };

        _enums["KEYCODE"] = members;
        _enumTypes["KEYCODE"] = new EnumTypeValue("KEYCODE");
    }

    // ============================================================
    // Expressões
    // ============================================================
}
