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
    private OslangValue EvalMe(Scope scope)
    {
        if (_currentObject is null)
        {
            throw new OslangRuntimeException(SourceLocation.Unknown, "ME used outside of a class method.");
        }

        return new ObjectValue(_currentObject);
    }


    private OslangValue EvalBase(Scope scope)
    {
        if (_currentObject is null)
        {
            throw new OslangRuntimeException(SourceLocation.Unknown, "BASE used outside of a class method.");
        }

        var baseClass = _currentObject.ClassDefinition.BaseClass;
        if (baseClass is null)
        {
            throw new OslangRuntimeException(SourceLocation.Unknown, "BASE can only be used in a derived class.");
        }

        return new ObjectValue(_currentObject);
    }


    private OslangValue EvalNamespace(NamespaceExpr ns, Scope scope)
    {
        return ns.NamespaceName.ToUpperInvariant() switch
        {
            "MATH" => OslangValue.Null, // MATH is a namespace marker, actual methods dispatched in EvalMethodCall
            "FILE" => OslangValue.Null, // FILE is a namespace marker
            "DIR" => OslangValue.Null, // DIR is a namespace marker
            "OSL" => OslangValue.Null,  // OSL is a namespace marker, actual methods dispatched in EvalMethodCall
            "OSB" => OslangValue.Null,  // OSB is a namespace marker, actual methods dispatched in EvalMethodCall
            _ => throw new OslangRuntimeException(ns.Location, $"Unknown namespace '{ns.NamespaceName}'."),
        };
    }


    private OslangValue EvalMemberAccess(MemberAccessExpr expr, Scope scope)
    {
        if (expr.Object is NamespaceExpr ns)
        {
            return CallNamespaceMethod(ns.NamespaceName, expr.MemberName, [], expr.Location);
        }

        var obj = Eval(expr.Object, scope);

        if (obj is ModuleValue module)
        {
            return CallModuleMethod(module, expr.MemberName, [], expr.Location);
        }

        if (obj is EnumTypeValue enumType)
        {
            if (!_enums.TryGetValue(enumType.EnumName, out var members))
            {
                throw new OslangRuntimeException(expr.Location, $"Unknown enum type '{enumType.EnumName}'.");
            }

            var member = members.FirstOrDefault(m => m.MemberName.Equals(expr.MemberName, StringComparison.OrdinalIgnoreCase));
            if (member.MemberName is null)
            {
                throw new OslangRuntimeException(expr.Location, $"Member '{expr.MemberName}' not found in enum '{enumType.EnumName}'.");
            }

            return new EnumValue(member.Value, enumType.EnumName, member.MemberName);
        }

        if (obj is EnumValue enumValue)
        {
            return DispatchEnum(enumValue, expr.MemberName, [], expr.Location);
        }

        if (obj is EnumSetValue enumSet)
        {
            return DispatchEnumSet(enumSet, expr.MemberName, [], expr.Location);
        }

        if (obj is JsonObjectValue jsonObj)
        {
            if (jsonObj.Data.TryGetValue(expr.MemberName, out var jsonValue))
            {
                return jsonValue;
            }
            return OslangValue.Null;
        }

        if (obj is XmlNodeValue xmlNode)
        {
            return xmlNode.GetProperty(expr.MemberName);
        }

        if (obj is CnfConfigValue cnf)
        {
            if (cnf.Data.TryGetValue(expr.MemberName, out var cnfValue))
            {
                return new StringValue(cnfValue);
            }
            return OslangValue.Null;
        }

        if (obj is KeyValue keyValue)
        {
            return expr.MemberName.ToUpperInvariant() switch
            {
                "KEY" => keyValue.Key,
                "CHAR" => keyValue.Char is not null ? new StringValue(keyValue.Char) : OslangValue.Null,
                "CTRL" => BooleanValue.Of(keyValue.Ctrl),
                "ALT" => BooleanValue.Of(keyValue.Alt),
                "SHIFT" => BooleanValue.Of(keyValue.Shift),
                _ => throw new OslangRuntimeException(expr.Location, $"Property '{expr.MemberName}' not found on KEY."),
            };
        }

        if (obj is SizeValue sizeValue)
        {
            return expr.MemberName.ToUpperInvariant() switch
            {
                "WIDTH" => new NumberValue(sizeValue.Width),
                "HEIGHT" => new NumberValue(sizeValue.Height),
                _ => throw new OslangRuntimeException(expr.Location, $"Property '{expr.MemberName}' not found on SIZE."),
            };
        }

        if (obj is CursorPositionValue cursorValue)
        {
            return expr.MemberName.ToUpperInvariant() switch
            {
                "ROW" => new NumberValue(cursorValue.Row),
                "COLUMN" => new NumberValue(cursorValue.Column),
                _ => throw new OslangRuntimeException(expr.Location, $"Property '{expr.MemberName}' not found on CURSOR."),
            };
        }

        if (obj is not ObjectValue objectValue)
        {
            throw new OslangRuntimeException(expr.Location, $"Cannot access member '{expr.MemberName}' on type {obj.TypeName}.");
        }

        var classDef = expr.Object is BaseExpr && objectValue.Instance.ClassDefinition.BaseClass is not null
            ? objectValue.Instance.ClassDefinition.BaseClass
            : objectValue.Instance.ClassDefinition;

        var prop = classDef.FindProperty(expr.MemberName);
        if (prop is null)
        {
            throw new OslangRuntimeException(expr.Location, $"Property '{expr.MemberName}' not found in class '{classDef.Name}'.");
        }

        CheckMemberAccessVisibility(objectValue.Instance, prop.Visibility, prop.Name, expr.Location);
        
        if (objectValue.Instance.PropertyValues.TryGetValue(prop.Name, out var value))
        {
            return value;
        }
        return OslangValue.Null;
    }
}
