namespace Osb.Lang.Compilation;

/// <summary>
/// Abstraction responsible for locating and loading OSLANG source modules.
/// </summary>
public interface IModuleResolver
{
    /// <summary>
    /// Resolves a module name (e.g. "PERSON", "OSB.SHELL") to a module source.
    /// Returns null if the module cannot be found.
    /// </summary>
    Task<Module?> ResolveAsync(string moduleName, CancellationToken cancellationToken = default);
}
