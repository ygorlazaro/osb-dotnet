using Osb.Lang.Ast;

namespace Osb.Lang.Runtime.Events;

/// <summary>
/// Represents an event definition in OSLANG.
/// </summary>
public sealed class EventDefinition
{
    public string Name { get; }
    public IReadOnlyList<ParameterDecl> Parameters { get; }
    public ClassDefinition? Owner { get; set; }

    public EventDefinition(string name, IReadOnlyList<ParameterDecl> parameters)
    {
        Name = name;
        Parameters = parameters;
    }
}
