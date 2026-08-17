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
    private OslangValue EvalNew(NewExpr expr, Scope scope)
    {
        if (!_classes.TryGetValue(expr.ClassName, out var classDef))
        {
            throw new OslangRuntimeException(expr.Location, $"Unknown class '{expr.ClassName}'.");
        }

        var instance = new ObjectInstance(classDef);
        
        InitializeProperties(instance, classDef);
        
        var args = expr.Args.Select(a => Eval(a, scope)).ToList();
        
        var previousObject = _currentObject;
        var previousEnclosing = _enclosingClass;
        _currentObject = instance;
        _enclosingClass = classDef;
        
        try
        {
            var constructor = classDef.Constructor;
            if (constructor is not null)
            {
                ExecuteConstructor(instance, constructor, args, classDef, expr.Location);
            }
            else if (classDef.BaseClass is not null && classDef.BaseClass.Constructor is not null)
            {
                ExecuteBaseConstructor(instance, classDef.BaseClass, [], expr.Location);
            }
        }
        finally
        {
            _currentObject = previousObject;
            _enclosingClass = previousEnclosing;
        }

        return new ObjectValue(instance);
    }


    private void InitializeProperties(ObjectInstance instance, ClassDefinition classDef)
    {
        var current = classDef;
        while (current != null)
        {
            foreach (var prop in current.Properties)
            {
                if (!instance.PropertyValues.ContainsKey(prop.Name))
                {
                    instance.PropertyValues[prop.Name] = prop.TypeName is not null 
                        ? TypeSystem.DefaultValueFor(TypeSystem.ParseTypeName(prop.TypeName)) 
                        : OslangValue.Null;
                }
            }
            current = current.BaseClass;
        }
    }


    private void ExecuteBaseConstructor(ObjectInstance instance, ClassDefinition baseClass, IReadOnlyList<OslangValue> args, SourceLocation callLocation)
    {
        if (baseClass.BaseClass is not null)
        {
            ExecuteBaseConstructor(instance, baseClass.BaseClass, args, callLocation);
        }

        var constructor = baseClass.Constructor;
        if (constructor is not null)
        {
            ExecuteConstructor(instance, constructor, args, baseClass, callLocation);
        }
    }


    private void ExecuteConstructor(ObjectInstance instance, ConstructorDefinition constructor, IReadOnlyList<OslangValue> args, ClassDefinition enclosingClass, SourceLocation callLocation)
    {
        if (args.Count != constructor.Parameters.Count)
        {
            throw new OslangRuntimeException(callLocation, $"Constructor expects {constructor.Parameters.Count} argument(s), got {args.Count}.");
        }

        var previousObject = _currentObject;
        var previousEnclosing = _enclosingClass;
        var previousInConstructor = _inConstructor;
        _currentObject = instance;
        _enclosingClass = enclosingClass;
        _inConstructor = true;

        var scope = new Scope(_globals);
        for (var i = 0; i < constructor.Parameters.Count; i++)
        {
            var param = constructor.Parameters[i];
            var variable = scope.DeclareLocal(param.Name);
            if (param.TypeName is not null)
            {
                variable.EstablishedType = TypeSystem.ParseTypeName(param.TypeName);
            }
            TypeSystem.Assign(variable, args[i], param.Location, $"parameter '{param.Name}'");
        }

        try
        {
            var body = constructor.Body;
            var remainingBody = body;
            
            if (body.Count > 0 && body[0] is BaseCallStmt baseCall)
            {
                var baseArgs = baseCall.Args.Select(a => Eval(a, scope)).ToList();
                if (enclosingClass.BaseClass is not null && enclosingClass.BaseClass.Constructor is not null)
                {
                    ExecuteConstructor(instance, enclosingClass.BaseClass.Constructor, baseArgs, enclosingClass.BaseClass, baseCall.Location);
                }
                else
                {
                    throw new OslangRuntimeException(baseCall.Location, "BASE can only be used in a derived class with a base class constructor.");
                }
                remainingBody = body.Skip(1).ToList();
            }
            else if (enclosingClass.BaseClass is not null && enclosingClass.BaseClass.Constructor is not null)
            {
                ExecuteBaseConstructor(instance, enclosingClass.BaseClass, [], callLocation);
            }
            
            ExecuteBlock(remainingBody, scope, loopDepth: 0);
        }
        catch (ReturnSignal)
        {
            // constructors don't return values, ignore
        }
        finally
        {
            _currentObject = previousObject;
            _enclosingClass = previousEnclosing;
            _inConstructor = previousInConstructor;
        }
    }
}
