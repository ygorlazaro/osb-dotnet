namespace Osb.Lang.Runtime;

/// <summary>
/// Uma variável (local, global ou parâmetro) de OSLANG.
///
/// <see cref="EstablishedType"/> é null enquanto a variável for não tipada e
/// ainda não tiver recebido nenhum valor não-NULL (seção 11); para variáveis
/// declaradas explicitamente com VAR Name TYPE, já nasce fixo. Uma vez fixo
/// (seja por declaração explícita ou pela primeira atribuição não-NULL), toda
/// atribuição seguinte deve respeitar esse tipo - exceto NULL, que nunca fixa
/// nem quebra o tipo (seção 11, "NULL does not establish or change a variable's type").
/// </summary>
public sealed class Variable
{
    public RuntimeType? EstablishedType { get; set; }
    public OslangValue Value { get; set; } = OslangValue.Null;
}
