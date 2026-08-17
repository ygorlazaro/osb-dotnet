namespace Osb.Lang.Runtime;

/// <summary>
/// Escopo de uma chamada de função. OSLANG não tem escopo de bloco (seção 55 -
/// manter a linguagem pequena): IF/FOR/WHILE/TRY não criam um novo escopo, só
/// FUNCTION cria (seção 14/15). Por isso um único dicionário "local" cobre toda a
/// execução de uma chamada de função, incluindo dentro de laços e blocos condicionais.
///
/// Resolução de nomes (seção 14 - "local variables take precedence over globals"):
/// leitura e atribuição implícita (Name = valor, sem VAR/GLOBAL) procuram primeiro
/// no escopo local; se não existir localmente, procuram no escopo global; se não
/// existir em nenhum dos dois, criam uma variável local nova. Isso não está 100%
/// explícito na especificação, mas é a leitura mais direta da regra de precedência
/// combinada com "variables are local by default" (seção 14) - documentado aqui
/// em vez de inventado silenciosamente.
///
/// VAR sempre declara uma variável local nova (mesmo que exista uma global com o
/// mesmo nome, ela passa a ficar sombreada nesta função). GLOBAL sempre declara/
/// atualiza diretamente o dicionário global, de qualquer função.
/// </summary>
public sealed class Scope
{
    private readonly Dictionary<string, Variable> _locals = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Variable> _globals;
    private readonly Scope? _parent;

    public Scope(Dictionary<string, Variable> globals)
    {
        _globals = globals;
    }

    public Scope(Scope parent) : this(parent._globals)
    {
        _parent = parent;
    }

    /// <summary>Declara (ou redeclara) uma variável local - usado por VAR e por parâmetros de função.</summary>
    public Variable DeclareLocal(string name)
    {
        var variable = new Variable();
        _locals[name] = variable;
        return variable;
    }

    /// <summary>Resolve um nome para leitura: local primeiro, depois global. Retorna null se não existir.</summary>
    public Variable? TryResolve(string name)
    {
        if (_locals.TryGetValue(name, out var local))
        {
            return local;
        }

        if (_parent is not null)
        {
            var parentResult = _parent.TryResolve(name);
            if (parentResult is not null)
            {
                return parentResult;
            }
        }

        return _globals.GetValueOrDefault(name);
    }

    public bool HasLocal(string name) => _locals.ContainsKey(name);

    public Variable ResolveForAssignment(string name)
    {
        if (_locals.TryGetValue(name, out var local))
        {
            return local;
        }

        if (_parent is not null)
        {
            var parentVar = _parent.TryResolve(name);
            if (parentVar is not null)
            {
                return parentVar;
            }
        }

        if (_globals.TryGetValue(name, out var global))
        {
            return global;
        }

        return DeclareLocal(name);
    }

    /// <summary>Declara ou atualiza uma variável global diretamente (statement GLOBAL).</summary>
    public Variable DeclareOrGetGlobal(string name)
    {
        if (_globals.TryGetValue(name, out var existing))
        {
            return existing;
        }

        var variable = new Variable();
        _globals[name] = variable;
        return variable;
    }
}
