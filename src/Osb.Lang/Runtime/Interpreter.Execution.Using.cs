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
    private void ExecuteUsing(UsingDecl usingDecl, Scope scope)
    {
        var moduleName = usingDecl.ModuleName;

        if (!moduleName.StartsWith("OSL.", StringComparison.OrdinalIgnoreCase) && !moduleName.StartsWith("OSB.", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var shortName = moduleName.Split('.')[^1];
        if (_standardLibraries.ContainsKey(shortName))
        {
            return;
        }

        if (moduleName.Equals("OSL.I18N", StringComparison.OrdinalIgnoreCase))
        {
            var moduleValue = new ModuleValue("OSL.I18N");
            _standardLibraries[shortName] = moduleValue;
            scope.DeclareOrGetGlobal(shortName);
            _globals[shortName] = new Variable { Value = moduleValue };
            return;
        }

        if (moduleName.Equals("OSL.JSON", StringComparison.OrdinalIgnoreCase))
        {
            var moduleValue = new ModuleValue("OSL.JSON");
            _standardLibraries[shortName] = moduleValue;
            scope.DeclareOrGetGlobal(shortName);
            _globals[shortName] = new Variable { Value = moduleValue };
            return;
        }

        if (moduleName.Equals("OSL.CSV", StringComparison.OrdinalIgnoreCase))
        {
            var moduleValue = new ModuleValue("OSL.CSV");
            _standardLibraries[shortName] = moduleValue;
            scope.DeclareOrGetGlobal(shortName);
            _globals[shortName] = new Variable { Value = moduleValue };
            return;
        }

        if (moduleName.Equals("OSL.XML", StringComparison.OrdinalIgnoreCase))
        {
            var moduleValue = new ModuleValue("OSL.XML");
            _standardLibraries[shortName] = moduleValue;
            scope.DeclareOrGetGlobal(shortName);
            _globals[shortName] = new Variable { Value = moduleValue };
            return;
        }

        if (moduleName.Equals("OSL.CNF", StringComparison.OrdinalIgnoreCase))
        {
            var moduleValue = new ModuleValue("OSL.CNF");
            _standardLibraries[shortName] = moduleValue;
            scope.DeclareOrGetGlobal(shortName);
            _globals[shortName] = new Variable { Value = moduleValue };
            return;
        }

        if (moduleName.Equals("OSB.NET", StringComparison.OrdinalIgnoreCase))
        {
            var moduleValue = new ModuleValue("OSB.NET");
            _standardLibraries[shortName] = moduleValue;
            scope.DeclareOrGetGlobal(shortName);
            _globals[shortName] = new Variable { Value = moduleValue };
            return;
        }

        if (moduleName.Equals("OSL.CONSOLE", StringComparison.OrdinalIgnoreCase))
        {
            var moduleValue = new ModuleValue("OSL.CONSOLE");
            _standardLibraries[shortName] = moduleValue;
            scope.DeclareOrGetGlobal(shortName);
            _globals[shortName] = new Variable { Value = moduleValue };
            return;
        }

        if (moduleName.Equals("OSL.APP", StringComparison.OrdinalIgnoreCase))
        {
            var moduleValue = new ModuleValue("OSL.APP");
            _standardLibraries[shortName] = moduleValue;
            scope.DeclareOrGetGlobal(shortName);
            _globals[shortName] = new Variable { Value = moduleValue };
            return;
        }

        if (moduleName.Equals("OSL.FILE", StringComparison.OrdinalIgnoreCase))
        {
            var moduleValue = new ModuleValue("OSL.FILE");
            _standardLibraries[shortName] = moduleValue;
            scope.DeclareOrGetGlobal(shortName);
            _globals[shortName] = new Variable { Value = moduleValue };
            return;
        }

        throw new OslangRuntimeException(usingDecl.Location, $"Unknown standard library module '{moduleName}'.");
    }
}
