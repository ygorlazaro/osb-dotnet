using Osb.Lang.Ast;
using Osb.Lang.Diagnostics;

namespace Osb.Lang.Compilation;

/// <summary>
/// Represents a symbol declared in an OSLANG module.
/// </summary>
public abstract record Symbol(string Name, SourceLocation Location);

/// <summary>
/// A function or method symbol, possibly part of an overload set.
/// </summary>
public sealed record FunctionSymbol(string Name, FunctionDecl Decl, SourceLocation Location) : Symbol(Name, Location);

/// <summary>
/// A class symbol.
/// </summary>
public sealed record ClassSymbol(string Name, SourceLocation Location) : Symbol(Name, Location);

/// <summary>
/// An interface symbol.
/// </summary>
public sealed record InterfaceSymbol(string Name, SourceLocation Location) : Symbol(Name, Location);

/// <summary>
/// An event symbol.
/// </summary>
public sealed record EventSymbol(string Name, SourceLocation Location) : Symbol(Name, Location);

/// <summary>
/// A generic type parameter symbol.
/// </summary>
public sealed record GenericParameter(string Name, SourceLocation Location) : Symbol(Name, Location);
