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
    private OslangValue CreateMethodReference(MethodDefinition method)
    {
        if (_currentObject is null)
        {
            throw new OslangRuntimeException(SourceLocation.Unknown, $"Cannot reference method '{method.Name}' outside of an object context.");
        }

        var instance = _currentObject;
        var classDef = instance.ClassDefinition;
        var declaringClass = FindMethodDeclaringClass(classDef, method.Name);

        return new FunctionValue((args, location) =>
        {
            if (args.Count != method.Parameters.Count)
            {
                throw new OslangRuntimeException(location, $"Method '{method.Name}' expects {method.Parameters.Count} argument(s), got {args.Count}.");
            }

            var previousObject = _currentObject;
            var previousEnclosing = _enclosingClass;
            _currentObject = instance;
            _enclosingClass = declaringClass ?? classDef;

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
        });
    }
}
