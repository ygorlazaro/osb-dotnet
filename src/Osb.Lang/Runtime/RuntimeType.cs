namespace Osb.Lang.Runtime;

/// <summary>
/// Os tipos de valor em runtime de OSLANG (seção 6). Os nomes dos membros do enum
/// coincidem, propositalmente, com o que TYPEOF() deve retornar (seção 43) - ver
/// <see cref="OslangValue.TypeName"/>.
/// </summary>
public enum RuntimeType
{
    Number,
    String,
    Boolean,
    Array,
    Null,
}
