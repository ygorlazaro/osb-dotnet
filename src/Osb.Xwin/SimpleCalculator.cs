using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Osb.Xwin;

public static class SimpleCalculator
{
    public static decimal Evaluate(string expression)
    {
        var tokens = Tokenize(expression).ToList();
        var values = new Stack<decimal>();
        var ops = new Stack<char>();
        foreach (var token in tokens)
        {
            if (decimal.TryParse(token, out var number))
            {
                values.Push(number);
                continue;
            }

            if (token == "(")
            {
                ops.Push('(');
                continue;
            }
            if (token == ")")
            {
                while (ops.Count > 0 && ops.Peek() != '(')
                    ApplyTop(values, ops);
                if (ops.Count > 0)
                {
                    ops.Pop();
                }

                continue;
            }

            var op = token[0];
            while (ops.Count > 0 && Precedence(ops.Peek()) >= Precedence(op))
                ApplyTop(values, ops);
            ops.Push(op);
        }

        while (ops.Count > 0)
            ApplyTop(values, ops);

        return values.Count > 0 ? values.Pop() : 0m;
    }

    private static IEnumerable<string> Tokenize(string expression)
    {
        var builder = new StringBuilder();
        foreach (var ch in expression)
        {
            if (char.IsWhiteSpace(ch))
            {
                continue;
            }

            if (char.IsDigit(ch) || ch == '.')
            {
                builder.Append(ch);
                continue;
            }

            if (builder.Length > 0)
            {
                yield return builder.ToString();
                builder.Clear();
            }

            if (ch is '(' or ')')
            {
                yield return ch.ToString();
                continue;
            }

            if (ch == 's')
            {
                builder.Append(ch);
                continue;
            }

            yield return ch.ToString();
        }

        if (builder.Length > 0)
        {
            if (builder.ToString() == "sqrt")
            {
                yield return "sqrt";
            }
            else
            {
                yield return builder.ToString();
            }
        }
    }

    private static void ApplyTop(Stack<decimal> values, Stack<char> ops)
    {
        if (ops.Count == 0)
        {
            return;
        }

        var op = ops.Pop();
        if (op == 's')
        {
            if (values.Count < 1)
            {
                throw new InvalidOperationException("Operador sqrt sem operandos");
            }

            var operand = values.Pop();
            values.Push((decimal)Math.Sqrt((double)operand));
            return;
        }

        if (values.Count < 2)
        {
            return;
        }

        var right = values.Pop();
        var left = values.Pop();
        values.Push(op switch
        {
            '+' => left + right,
            '-' => left - right,
            '*' => left * right,
            '/' => right == 0 ? 0 : left / right,
            '%' => right == 0 ? 0 : left % right,
            '^' => (decimal)Math.Pow((double)left, (double)right),
            _ => throw new InvalidOperationException($"Operador inválido: {op}")
        });
    }

    private static int Precedence(char op) => op switch
    {
        '+' => 1,
        '-' => 1,
        '*' => 2,
        '/' => 2,
        _ => 0
    };
}
