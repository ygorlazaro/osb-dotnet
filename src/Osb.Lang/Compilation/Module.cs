using Osb.Lang.Ast;
using Osb.Lang.Diagnostics;

namespace Osb.Lang.Compilation;

/// <summary>
/// Represents a loaded and parsed OSLANG source module.
/// </summary>
public sealed class Module
{
    public string Name { get; }
    public string Path { get; }
    public string Source { get; }
    public OslangProgram Program { get; }
    public IReadOnlyList<Ast.UsingDecl> Usings { get; }
    public SourceLocation? MainLocation { get; }
    public bool IsParsed { get; set; }

    public Module(string name, string path, string source, OslangProgram program, IReadOnlyList<Ast.UsingDecl> usings, SourceLocation? mainLocation)
    {
        Name = name;
        Path = path;
        Source = source;
        Program = program;
        Usings = usings;
        MainLocation = mainLocation;
        IsParsed = true;
    }
}
