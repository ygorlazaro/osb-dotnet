using Osb.Lang.Ast;
using Osb.Lang.Compilation;
using Osb.Lang.Diagnostics;
using Osb.Lang.Extensibility;
using Osb.Lang.Lexing;
using Osb.Lang.Parsing;
using Osb.Lang.Runtime;

namespace Osb.Lang.Runtime;

internal sealed partial class Interpreter
{
    private void CheckMemberVisibility(Visibility memberVisibility, string memberName, SourceLocation location)
    {
        if (memberVisibility == Visibility.Public)
        {
            return;
        }

        var accessingClass = _enclosingClass;
        if (accessingClass is null && _currentObject is not null)
        {
            accessingClass = _currentObject.ClassDefinition;
        }

        var declaringClass = FindDeclaringClass(memberName);

        if (memberVisibility == Visibility.Private)
        {
            if (declaringClass is null || !ReferenceEquals(accessingClass, declaringClass))
            {
                throw new OslangRuntimeException(location, $"Property '{memberName}' is PRIVATE in class '{declaringClass?.Name ?? "unknown"}'.");
            }
        }
        else if (memberVisibility == Visibility.Protected)
        {
            if (declaringClass is null || !IsDerivedFrom(accessingClass, declaringClass))
            {
                throw new OslangRuntimeException(location, $"Property '{memberName}' is PROTECTED in class '{declaringClass?.Name ?? "unknown"}'.");
            }
        }
    }


    private ClassDefinition? FindDeclaringClass(string memberName)
    {
        if (_currentObject is null)
        {
            return null;
        }

        var current = _currentObject.ClassDefinition;
        while (current != null)
        {
            if (current.Properties.Any(p => p.Name.Equals(memberName, StringComparison.OrdinalIgnoreCase)))
            {
                return current;
            }
            if (current.Methods.Any(m => m.Name.Equals(memberName, StringComparison.OrdinalIgnoreCase)))
            {
                return current;
            }
            current = current.BaseClass;
        }

        return null;
    }


    private static bool IsDerivedFrom(ClassDefinition? derived, ClassDefinition? baseClass)
    {
        var current = derived;
        while (current != null)
        {
            if (ReferenceEquals(current, baseClass))
            {
                return true;
            }
            current = current.BaseClass;
        }

        return false;
    }


    private ArrayValue EvalArray(Expr expr, Scope scope)
    {
        var value = Eval(expr, scope);
        if (value is not ArrayValue array)
        {
            throw new OslangRuntimeException(expr.Location, $"Expected an ARRAY, got {value.TypeName}.");
        }

        return array;
    }


    private OslangValue EvalIndex(IndexExpr ix, Scope scope)
    {
        var array = EvalArray(ix.Array, scope);
        var index = ResolveIndex(Eval(ix.Index, scope), array, ix.Location);
        return array.Items[index];
    }


    private static int ResolveIndex(OslangValue indexValue, ArrayValue array, SourceLocation location)
    {
        if (indexValue is not NumberValue n)
        {
            throw new OslangRuntimeException(location, $"Array index must be a NUMBER, got {indexValue.TypeName}.");
        }

        if (n.Value != Math.Floor(n.Value))
        {
            throw new OslangRuntimeException(location, "Array index must be an integer.");
        }

        var index = (int)n.Value;
        if (index < 0 || index >= array.Items.Count)
        {
            throw new OslangRuntimeException(location, $"Array index {index} is out of range (array has {array.Items.Count} element(s)).");
        }

        return index;
    }


    private OslangValue EvalArrayLiteral(ArrayLiteralExpr expr, Scope scope)
    {
        var items = new List<OslangValue>(expr.Elements.Count);
        RuntimeType? elementType = null;

        foreach (var elementExpr in expr.Elements)
        {
            var value = Eval(elementExpr, scope);
            if (value.Type != RuntimeType.Null)
            {
                if (elementType is null)
                {
                    elementType = value.Type;
                }
                else if (elementType != value.Type)
                {
                    throw new OslangRuntimeException(
                        elementExpr.Location,
                        "Mixed-type arrays are not allowed - all elements of an ARRAY must be the same type.");
                }
            }

            items.Add(value);
        }

        return new ArrayValue(items, elementType);
    }
}
