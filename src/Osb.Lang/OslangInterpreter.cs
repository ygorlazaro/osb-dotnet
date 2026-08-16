using Osb.Lang.Ast;
using Osb.Lang.Compilation;
using Osb.Lang.Diagnostics;
using Osb.Lang.Extensibility;
using Osb.Lang.Lexing;
using Osb.Lang.Parsing;
using Osb.Lang.Runtime;

using System.IO;

namespace Osb.Lang;

/// <summary>
/// Ponto de entrada público da biblioteca OSLANG (seção 47):
///
/// <code>
/// var interpreter = new OslangInterpreter();
/// var result = interpreter.Execute(source);
/// </code>
///
/// Todo o pipeline (lexer → parser → interpretador) roda dentro de
/// <see cref="Execute"/>. Erros léxicos, sintáticos, semânticos e de runtime são
/// todos <see cref="Diagnostics.OslangException"/> e devem ser tratados pelo
/// chamador (o host - Osb.Shell, Osb.Xwin, etc. - decide como exibi-los).
/// </summary>
public sealed class OslangInterpreter
{
    public const string Version = "0.61";

    private readonly ExtensionRegistry _extensions;

    /// <param name="extensions">
    /// Funções registradas pelo host (seção 45). Se omitido, um registro vazio é
    /// usado - o programa só pode chamar FUNCTIONs do próprio script e a
    /// biblioteca padrão de OSLANG.
    /// </param>
    public OslangInterpreter(ExtensionRegistry? extensions = null)
    {
        _extensions = extensions ?? new ExtensionRegistry();
    }

    /// <summary>
    /// Lexa, parseia e executa <paramref name="source"/>, começando por FUNCTION
    /// MAIN() (seção 18). Retorna o valor de RETURN de MAIN() (ou NULL se MAIN
    /// não usar RETURN explícito).
    /// </summary>
    /// <param name="source">Código-fonte OSLANG (.osl).</param>
    /// <param name="output">Destino de PRINT. Usa Console.Out se omitido.</param>
    /// <param name="input">Origem de INPUT. Usa Console.In se omitido.</param>
    /// <param name="clear">Ação executada por CLEAR. Se omitido, CLEAR não faz nada.</param>
    /// <exception cref="Diagnostics.OslangException">
    /// Erro léxico, sintático, semântico ou de runtime não capturado por nenhum
    /// TRY/CATCH dentro do próprio programa.
    /// </exception>
    public OslangValue Execute(string source, TextWriter? output = null, TextReader? input = null, Action? clear = null, string? basePath = null, IReadOnlyList<OslangValue>? args = null)
    {
        var program = ParseSource(source);
        var module = new Module("main", string.Empty, source, program, program.Usings, null);
        var modules = new List<Module> { module };
        var resolver = new FilesystemModuleResolver(basePath ?? Directory.GetCurrentDirectory());
        var compilation = global::Osb.Lang.Compilation.Compilation.Create(modules, resolver);
        
        _extensions.BasePath = basePath ?? Directory.GetCurrentDirectory();
        return compilation.Execute(output ?? Console.Out, input ?? Console.In, clear, _extensions, args);
    }

    private static OslangProgram ParseSource(string source)
    {
        var tokens = new Lexer(source).Tokenize();
        var parser = new Parser(tokens);
        return parser.Parse();
    }
}
