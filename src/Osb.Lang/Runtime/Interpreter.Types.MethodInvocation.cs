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
    private OslangValue CallMethod(ObjectInstance instance, MethodDefinition method, ClassDefinition? declaringClass, IReadOnlyList<OslangValue> args, SourceLocation callLocation)
    {
        if (args.Count != method.Parameters.Count)
        {
            throw new OslangRuntimeException(callLocation, $"Method '{method.Name}' expects {method.Parameters.Count} argument(s), got {args.Count}.");
        }

        var previousObject = _currentObject;
        var previousEnclosing = _enclosingClass;
        _currentObject = instance;
        _enclosingClass = declaringClass ?? instance.ClassDefinition;

        var scope = new Scope(_globals);
        for (var i = 0; i < method.Parameters.Count; i++)
        {
            var param = method.Parameters[i];
            var variable = scope.DeclareLocal(param.Name);
            if (param.TypeName is not null)
            {
                variable.EstablishedType = TypeSystem.ParseTypeName(param.TypeName);
            }
            TypeSystem.Assign(variable, args[i], param.Location, $"parameter '{param.Name}'");
        }

        try
        {
            ExecuteBlock(method.Body, scope, loopDepth: 0);
        }
        catch (ReturnSignal ret)
        {
            return ret.Value;
        }
        finally
        {
            _currentObject = previousObject;
            _enclosingClass = previousEnclosing;
        }

        return OslangValue.Null;
    }


    private OslangValue ResolveMemberAccess(ObjectInstance instance, string memberName, SourceLocation location, bool isWrite)
    {
        var classDef = instance.ClassDefinition;
        var prop = classDef.FindProperty(memberName);
        if (prop is not null)
        {
            if (isWrite)
            {
                instance.PropertyValues[prop.Name] = OslangValue.Null; // placeholder, actual assignment handled elsewhere
            }
            if (instance.PropertyValues.TryGetValue(prop.Name, out var value))
            {
                return value;
            }
            return OslangValue.Null;
        }

        var method = classDef.FindMethod(memberName);
        if (method is not null)
        {
            // Return a callable wrapper - for now, just return Null since method calls use MethodCallExpr
            return OslangValue.Null;
        }

        throw new OslangRuntimeException(location, $"Member '{memberName}' not found in class '{classDef.Name}'.");
    }


    private OslangValue ResolveMemberForAssignment(ObjectInstance instance, string memberName, SourceLocation location)
    {
        var classDef = instance.ClassDefinition;
        var prop = classDef.FindProperty(memberName);
        if (prop is null)
        {
            throw new OslangRuntimeException(location, $"Property '{memberName}' not found in class '{classDef.Name}'.");
        }

        return instance.PropertyValues.GetValueOrDefault(prop.Name, OslangValue.Null);
    }
}
