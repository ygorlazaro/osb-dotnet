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
    private OslangValue EvalMethodCall(MethodCallExpr expr, Scope scope)
    {
        if (expr.Object is NamespaceExpr ns)
        {
            var args = expr.Args.Select(a => Eval(a, scope)).ToList();
            return CallNamespaceMethod(ns.NamespaceName, expr.MethodName, args, expr.Location);
        }

        var obj = Eval(expr.Object, scope);

        if (obj is ModuleValue module)
        {
            var args = expr.Args.Select(a => Eval(a, scope)).ToList();
            return CallModuleMethod(module, expr.MethodName, args, expr.Location);
        }

        if (obj is EnumValue enumValue)
        {
            var args = expr.Args.Select(a => Eval(a, scope)).ToList();
            return DispatchEnum(enumValue, expr.MethodName, args, expr.Location);
        }

        if (obj is EnumSetValue enumSet)
        {
            var args = expr.Args.Select(a => Eval(a, scope)).ToList();
            return DispatchEnumSet(enumSet, expr.MethodName, args, expr.Location);
        }

        if (obj is JsonObjectValue jsonObj)
        {
            var args = expr.Args.Select(a => Eval(a, scope)).ToList();
            return DispatchJsonObject(jsonObj, expr.MethodName, args, expr.Location);
        }

        if (obj is JsonArrayValue jsonArr)
        {
            var args = expr.Args.Select(a => Eval(a, scope)).ToList();
            return DispatchJsonArray(jsonArr, expr.MethodName, args, expr.Location);
        }

        if (obj is XmlNodeValue xmlNode)
        {
            var args = expr.Args.Select(a => Eval(a, scope)).ToList();
            return DispatchXmlNode(xmlNode, expr.MethodName, args, expr.Location);
        }

        if (obj is CnfConfigValue cnf)
        {
            var args = expr.Args.Select(a => Eval(a, scope)).ToList();
            return DispatchCnfConfig(cnf, expr.MethodName, args, expr.Location);
        }

        if (obj is ObjectValue objectValue)
        {
            var classDef = expr.Object is BaseExpr && objectValue.Instance.ClassDefinition.BaseClass is not null
                ? objectValue.Instance.ClassDefinition.BaseClass
                : objectValue.Instance.ClassDefinition;

            var method = classDef.FindMethod(expr.MethodName);
            if (method is null)
            {
                throw new OslangRuntimeException(expr.Location, $"Method '{expr.MethodName}' not found in class '{classDef.Name}'.");
            }

            var declaringClass = FindMethodDeclaringClass(classDef, expr.MethodName);
            var args = expr.Args.Select(a => Eval(a, scope)).ToList();
            return CallMethod(objectValue.Instance, method, declaringClass, args, expr.Location);
        }

        if (obj is StringValue or NumberValue or BooleanValue or ArrayValue)
        {
            var args = expr.Args.Select(a => Eval(a, scope)).ToList();
            return PrimitiveMethodDispatcher.Dispatch(obj, expr.MethodName, args, expr.Location);
        }

        if (obj is NullValue)
        {
            throw new OslangRuntimeException(expr.Location, $"Cannot call method '{expr.MethodName}' on NULL.");
        }

        throw new OslangRuntimeException(expr.Location, $"Cannot call method '{expr.MethodName}' on type {obj.TypeName}.");
    }


    private ClassDefinition? FindMethodDeclaringClass(ClassDefinition classDef, string methodName)
    {
        var current = classDef;
        while (current != null)
        {
            if (current.Methods.Any(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase)))
            {
                return current;
            }
            current = current.BaseClass;
        }
        return null;
    }


    private void CheckMemberAccessVisibility(ObjectInstance instance, Visibility memberVisibility, string memberName, SourceLocation location)
    {
        if (memberVisibility == Visibility.Public)
        {
            return;
        }

        var accessingClass = _enclosingClass;
        if (accessingClass is null && _currentObject is not null)
        {
            accessingClass = _currentObject.ClassDefinition;
        }

        var declaringClass = FindDeclaringClass(memberName);

        if (memberVisibility == Visibility.Private)
        {
            if (declaringClass is null || !ReferenceEquals(accessingClass, declaringClass))
            {
                throw new OslangRuntimeException(location, $"Property '{memberName}' is PRIVATE in class '{declaringClass?.Name ?? "unknown"}'.");
            }
        }
        else if (memberVisibility == Visibility.Protected)
        {
            if (declaringClass is null || !IsDerivedFrom(accessingClass, declaringClass))
            {
                throw new OslangRuntimeException(location, $"Property '{memberName}' is PROTECTED in class '{declaringClass?.Name ?? "unknown"}'.");
            }
        }
    }
}
