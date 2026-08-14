using Osb.Lang.Diagnostics;

namespace Osb.Lang.Compilation;

/// <summary>
/// Severity level for OSLANG diagnostics.
/// </summary>
public enum DiagnosticSeverity
{
    Error,
    Warning,
    Info,
}

/// <summary>
/// Represents a diagnostic message produced during compilation.
/// </summary>
public sealed record OslangDiagnostic(DiagnosticSeverity Severity, SourceLocation Location, string Message)
{
    public override string ToString()
    {
        var location = Location.Line == 0 ? "" : $"{Location}: ";
        return $"{location}{Message}";
    }
}
