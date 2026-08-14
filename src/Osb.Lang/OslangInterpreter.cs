using Osb.Lang.Ast;
using Osb.Lang.Diagnostics;
using Osb.Lang.Extensibility;
using Osb.Lang.Lexing;
using Osb.Lang.Parsing;
using Osb.Lang.Runtime;

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
    public OslangValue Execute(string source, TextWriter? output = null, TextReader? input = null, Action? clear = null)
    {
        var tokens = new Lexer(source).Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        
        var interpreter = new Interpreter(program, _extensions, output ?? Console.Out, input ?? Console.In, clear);
        
        foreach (var iface in parser.ParsedInterfaces)
        {
            var interfaceDef = new InterfaceDefinition(iface.Name, iface.Members);
            interpreter.RegisterInterface(interfaceDef);
        }
        
        foreach (var cls in parser.ParsedClasses)
        {
            var classDef = BuildClassDefinition(cls, interpreter);
            interpreter.RegisterClass(classDef);
        }
        
        SemanticAnalyzer.Validate(program, interpreter.GetClasses(), interpreter.GetInterfaces());
        
        return interpreter.Run();
    }

    private ClassDefinition BuildClassDefinition(ClassDecl decl, Interpreter interpreter)
    {
        ClassDefinition? baseClass = null;
        var interfaces = new List<InterfaceDefinition>();
        var remainingNames = new List<string>(decl.InheritedNames);

        if (remainingNames.Count > 0)
        {
            var first = remainingNames[0];
            if (interpreter.GetClass(first) is ClassDefinition potentialBase)
            {
                baseClass = potentialBase;
                remainingNames.RemoveAt(0);
            }
        }

        foreach (var name in remainingNames)
        {
            if (interpreter.GetClass(name) is not null)
            {
                throw new SemanticException(decl.Location, $"Class '{decl.Name}' cannot inherit from multiple classes. '{name}' is a class.");
            }
            if (interpreter.GetInterface(name) is InterfaceDefinition iface)
            {
                interfaces.Add(iface);
            }
            else
            {
                throw new SemanticException(decl.Location, $"Unknown interface '{name}' in class '{decl.Name}'.");
            }
        }

        var properties = new List<PropertyDefinition>();
        var methods = new List<MethodDefinition>();
        ConstructorDefinition? constructor = null;

        foreach (var member in decl.Members)
        {
            switch (member)
            {
                case PropertyDecl p:
                    properties.Add(new PropertyDefinition(p.Name, p.TypeName, p.Visibility));
                    break;
                case MethodDecl m:
                    methods.Add(new MethodDefinition(m.Name, m.Parameters, m.Body, m.Visibility, m.Location));
                    break;
                case ConstructorDecl c:
                    constructor = new ConstructorDefinition(c.Parameters, c.Body, c.Location);
                    break;
            }
        }

        return new ClassDefinition(decl.Name, baseClass, interfaces, properties, methods, constructor);
    }
}
