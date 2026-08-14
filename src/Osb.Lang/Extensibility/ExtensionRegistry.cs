using Osb.Lang.Diagnostics;
using Osb.Lang.Runtime;

namespace Osb.Lang.Extensibility;

/// <summary>
/// Ponte controlada entre OSLANG e o host (Osb.Shell, Osb.Xwin, futuras
/// extensões) - seção 45. O host registra explicitamente funções nomeadas
/// (ex.: PWD, DIR, CD, MKDIR, DEL, TYPE) através de <see cref="Register"/>; o
/// core de OSLANG nunca expõe reflexão nem executa código C# arbitrário vindo
/// de um script - só chama exatamente as funções que o host decidiu registrar.
///
/// A implementação recebe a <see cref="SourceLocation"/> da chamada, para que
/// erros lançados pelo host (ex.: "CD(): diretório não existe") também saiam
/// com linha/coluna corretas, no mesmo formato dos erros do core (seção 44).
/// </summary>
public sealed class ExtensionRegistry
{
    private readonly Dictionary<string, Func<IReadOnlyList<OslangValue>, SourceLocation, OslangValue>> _functions = new();

    /// <summary>Registra uma função host, exposta a OSLANG pelo nome informado (case-insensitive).</summary>
    public void Register(string name, Func<IReadOnlyList<OslangValue>, SourceLocation, OslangValue> implementation)
    {
        _functions[name.ToUpperInvariant()] = implementation;
    }

    public bool TryGet(string name, out Func<IReadOnlyList<OslangValue>, SourceLocation, OslangValue> implementation) =>
        _functions.TryGetValue(name, out implementation!);
}

