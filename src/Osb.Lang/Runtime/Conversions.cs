using System.Globalization;
using Osb.Lang.Diagnostics;

namespace Osb.Lang.Runtime;

/// <summary>Regras de truthiness (seção 19) e conversão textual (usada por PRINT, STR() e o operador + de concatenação).</summary>
public static class Conversions
{
    public static bool IsTruthy(OslangValue value) => value switch
    {
        NullValue => false,
        BooleanValue b => b.Value,
        NumberValue n => n.Value != 0,
        StringValue s => s.Value.Length > 0,
        ArrayValue => true, // "any ARRAY" é truthy, mesmo vazio (seção 19)
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    /// <summary>
    /// Formata um NUMBER para exibição. Decisão de design: números inteiros são
    /// exibidos sem casas decimais ("40", não "40.0"); a especificação não define
    /// um formato exato de exibição para NUMBER.
    /// </summary>
    public static string NumberToString(double value)
    {
        if (double.IsNaN(value))
        {
            return "NaN";
        }

        if (double.IsPositiveInfinity(value))
        {
            return "Infinity";
        }

        if (double.IsNegativeInfinity(value))
        {
            return "-Infinity";
        }

        return value == Math.Floor(value) && Math.Abs(value) < 1e15
            ? value.ToString("F0", CultureInfo.InvariantCulture)
            : value.ToString("G15", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Converte um valor para sua representação textual, usada por PRINT, STR() e
    /// pela concatenação com + (seção 23). Arrays não têm uma representação
    /// textual definida pela especificação e geram erro de runtime aqui.
    /// </summary>
    public static string ToDisplayString(OslangValue value, SourceLocation location) => value switch
    {
        NullValue => "NULL",
        StringValue s => s.Value,
        NumberValue n => NumberToString(n.Value),
        BooleanValue b => b.Value ? "TRUE" : "FALSE",
        ArrayValue => throw new OslangRuntimeException(location, "Cannot convert an ARRAY to STRING."),
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };
}
