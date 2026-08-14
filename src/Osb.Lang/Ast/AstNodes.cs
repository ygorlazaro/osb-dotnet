using Osb.Lang.Diagnostics;

namespace Osb.Lang.Ast;

// ============================================================
// Programa / funções
// ============================================================

/// <summary>Um programa OSLANG completo: a lista de funções declaradas no arquivo (em qualquer ordem, seção 18).</summary>
public sealed record OslangProgram(IReadOnlyList<FunctionDecl> Functions);

/// <summary>
/// Parâmetro de função. <see cref="TypeName"/> é "NUMBER"/"STRING"/"BOOLEAN" para
/// parâmetros tipados (seção 16), ou null para parâmetros não tipados.
/// </summary>
public sealed record ParameterDecl(string Name, string? TypeName, SourceLocation Location);

public sealed record FunctionDecl(
    string Name,
    IReadOnlyList<ParameterDecl> Parameters,
    IReadOnlyList<Stmt> Body,
    SourceLocation Location);

// ============================================================
// Statements
// ============================================================

public abstract record Stmt(SourceLocation Location);

/// <summary>VAR Name [TYPE] - declaração local (seção 13). TypeName null = não tipada.</summary>
public sealed record VarDeclStmt(string Name, string? TypeName, SourceLocation Location) : Stmt(Location);

/// <summary>GLOBAL Name = expr - declara/atualiza uma variável global (seção 14).</summary>
public sealed record GlobalDeclStmt(string Name, Expr Value, SourceLocation Location) : Stmt(Location);

/// <summary>Target = expr, onde Target é uma variável simples ou uma posição de array.</summary>
public sealed record AssignStmt(AssignTarget Target, Expr Value, SourceLocation Location) : Stmt(Location);

/// <summary>Uma expressão usada como statement (ex.: chamar HELLO() só pelo efeito colateral).</summary>
public sealed record ExpressionStmt(Expr Expression, SourceLocation Location) : Stmt(Location);

/// <summary>PRINT expr [, expr]* - seção 37.</summary>
public sealed record PrintStmt(IReadOnlyList<Expr> Expressions, SourceLocation Location) : Stmt(Location);

/// <summary>INPUT Variable - seção 38.</summary>
public sealed record InputStmt(string VariableName, SourceLocation Location) : Stmt(Location);

/// <summary>CLEAR - seção 39.</summary>
public sealed record ClearStmt(SourceLocation Location) : Stmt(Location);

public sealed record ElifBranch(Expr Condition, IReadOnlyList<Stmt> Body);

public sealed record IfStmt(
    Expr Condition,
    IReadOnlyList<Stmt> ThenBody,
    IReadOnlyList<ElifBranch> ElifBranches,
    IReadOnlyList<Stmt>? ElseBody,
    SourceLocation Location) : Stmt(Location);

/// <summary>FOR Variable = Start TO End [STEP Step] - seções 29/30.</summary>
public sealed record ForStmt(
    string VariableName,
    Expr Start,
    Expr End,
    Expr? Step,
    IReadOnlyList<Stmt> Body,
    SourceLocation Location) : Stmt(Location);

/// <summary>WHILE condition - seção 31.</summary>
public sealed record WhileStmt(Expr Condition, IReadOnlyList<Stmt> Body, SourceLocation Location) : Stmt(Location);

/// <summary>
/// DO WHILE condition - seção 32. A especificação define explicitamente que a
/// condição é avaliada ANTES de cada iteração (podendo executar zero vezes), ou
/// seja: semanticamente idêntico a WHILE apesar do nome. Implementado à risca,
/// sem "corrigir" para o do-while clássico de outras linguagens.
/// </summary>
public sealed record DoWhileStmt(Expr Condition, IReadOnlyList<Stmt> Body, SourceLocation Location) : Stmt(Location);

public sealed record BreakStmt(SourceLocation Location) : Stmt(Location);

public sealed record ContinueStmt(SourceLocation Location) : Stmt(Location);

/// <summary>RETURN [expr] - Value null significa RETURN sem valor (retorna NULL, seção 17).</summary>
public sealed record ReturnStmt(Expr? Value, SourceLocation Location) : Stmt(Location);

/// <summary>TRY / CATCH ERR / END - seção 35.</summary>
public sealed record TryCatchStmt(
    IReadOnlyList<Stmt> TryBody,
    string CatchVariableName,
    IReadOnlyList<Stmt> CatchBody,
    SourceLocation Location) : Stmt(Location);

// ============================================================
// Alvos de atribuição
// ============================================================

public abstract record AssignTarget(SourceLocation Location);

public sealed record VariableTarget(string Name, SourceLocation Location) : AssignTarget(Location);

public sealed record IndexTarget(Expr ArrayExpr, Expr IndexExpr, SourceLocation Location) : AssignTarget(Location);

// ============================================================
// Expressões
// ============================================================

public abstract record Expr(SourceLocation Location);

public sealed record NumberLiteralExpr(double Value, SourceLocation Location) : Expr(Location);

public sealed record StringLiteralExpr(string Value, SourceLocation Location) : Expr(Location);

public sealed record BooleanLiteralExpr(bool Value, SourceLocation Location) : Expr(Location);

public sealed record NullLiteralExpr(SourceLocation Location) : Expr(Location);

public sealed record ArrayLiteralExpr(IReadOnlyList<Expr> Elements, SourceLocation Location) : Expr(Location);

public sealed record IdentifierExpr(string Name, SourceLocation Location) : Expr(Location);

public sealed record IndexExpr(Expr Array, Expr Index, SourceLocation Location) : Expr(Location);

/// <summary>
/// Chamada de função: tanto para funções definidas pelo usuário quanto para
/// funções de biblioteca padrão (STR, NUMBER, BOOL, SQRT, POW, FLOOR, CEIL,
/// COUNT, TYPEOF, ABS) e funções registradas por extensões do host (seção 45).
/// <see cref="Name"/> já vem normalizado para maiúsculas.
/// </summary>
public sealed record CallExpr(string Name, IReadOnlyList<Expr> Args, SourceLocation Location) : Expr(Location);

/// <summary>Operador unário: "NOT" ou "-" (menos unário).</summary>
public sealed record UnaryExpr(string Op, Expr Operand, SourceLocation Location) : Expr(Location);

/// <summary>Operador binário: "+", "-", "*", "/", "%", "=", "&lt;&gt;", "&lt;", "&gt;", "&lt;=", "&gt;=", "AND", "OR".</summary>
public sealed record BinaryExpr(string Op, Expr Left, Expr Right, SourceLocation Location) : Expr(Location);
