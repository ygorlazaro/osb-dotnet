namespace Osb.Lang.Runtime;

/// <summary>
/// Sinais internos de controle de fluxo, implementados como exceções para
/// simplificar o "unwind" através de blocos aninhados (IF dentro de FOR dentro de
/// TRY, etc.) em um interpretador tree-walking. Nunca escapam de
/// <see cref="Interpreter"/> - não são <see cref="Diagnostics.OslangException"/>
/// e não devem ser capturadas por TRY/CATCH de OSLANG (que captura apenas
/// <see cref="Diagnostics.OslangRuntimeException"/>).
/// </summary>
internal sealed class ReturnSignal(OslangValue value) : Exception
{
    public OslangValue Value { get; } = value;
}

internal sealed class BreakSignal : Exception
{
}

internal sealed class ContinueSignal : Exception
{
}
