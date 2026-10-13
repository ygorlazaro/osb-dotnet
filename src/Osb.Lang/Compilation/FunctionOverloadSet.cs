using Osb.Lang.Ast;
using Osb.Lang.Runtime;

namespace Osb.Lang.Compilation;

/// <summary>
/// An overload set for functions with the same name.
/// </summary>
public sealed class FunctionOverloadSet
{
    private readonly List<FunctionDecl> _overloads = new();
    private readonly Dictionary<string, FunctionDecl> _bySignature = new(StringComparer.OrdinalIgnoreCase);

    public string Name { get; }
    public IReadOnlyList<FunctionDecl> Overloads => _overloads;

    public FunctionOverloadSet(string name)
    {
        Name = name;
    }

    public void Add(FunctionDecl decl)
    {
        _overloads.Add(decl);
        var sig = ComputeSignature(decl);
        _bySignature[sig] = decl;
    }

    public FunctionDecl? Resolve(IReadOnlyList<RuntimeType> argumentTypes)
    {
        foreach (var decl in _overloads)
        {
            if (Matches(decl.Parameters, argumentTypes))
            {
                return decl;
            }
        }

        return null;
    }

    public bool HasDuplicateSignatures()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var decl in _overloads)
        {
            var sig = ComputeSignature(decl);
            if (!seen.Add(sig))
            {
                return true;
            }
        }

        return false;
    }

    private static string ComputeSignature(FunctionDecl decl)
    {
        return string.Join(";", decl.Parameters.Select(p => p.TypeName?.ToUpperInvariant() ?? "ANY"));
    }

    private static bool Matches(IReadOnlyList<ParameterDecl> parameters, IReadOnlyList<RuntimeType> argumentTypes)
    {
        if (argumentTypes.Count > parameters.Count)
        {
            return false;
        }

        // Missing trailing arguments are only legal when the corresponding parameter declares a default value.
        for (var i = argumentTypes.Count; i < parameters.Count; i++)
        {
            if (parameters[i].DefaultValue is null)
            {
                return false;
            }
        }

        for (var i = 0; i < argumentTypes.Count; i++)
        {
            var paramType = parameters[i].TypeName;
            if (paramType is null)
            {
                continue;
            }

            var expected = TypeSystem.ParseTypeName(paramType);
            if (expected != argumentTypes[i])
            {
                return false;
            }
        }

        return true;
    }
}
