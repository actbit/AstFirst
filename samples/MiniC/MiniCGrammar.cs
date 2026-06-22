using System.Collections.Generic;
using AstFirst;

namespace MiniC;

/// <summary>軽量C言語パーサのサンプル。変数・代入・if/while・式・print。</summary>

[Grammar]
[Skip(@"(\s|//[^\n]*)+")]
public abstract class Program : AstNode { }

// --- 文リスト ---
public sealed class ConsStmt : Program
{
    public Stmt First { get; }
    public Program Rest { get; }
    public ConsStmt(Stmt first, Program rest) { First = first; Rest = rest; }
}
public sealed class NilProgram : Program { public NilProgram() { } }

public abstract class Stmt : AstNode { }

// int x; | int x = expr;
public sealed class DeclStmt : Stmt
{
    public string Name { get; }
    public Expr? Init { get; }
    public DeclStmt([Pattern(@"int", Priority = 1)] Token kw, [Pattern(@"[A-Za-z_]\w*")] Token name, [Pattern(@";")] Token semi) { Name = name.Text; }
    public DeclStmt([Pattern(@"int", Priority = 1)] Token kw, [Pattern(@"[A-Za-z_]\w*")] Token name, [Pattern(@"=")] Token eq, Expr init, [Pattern(@";")] Token semi) { Name = name.Text; Init = init; }
}

// x = expr;
public sealed class AssignStmt : Stmt
{
    public string Name { get; }
    public Expr Value { get; }
    public AssignStmt([Pattern(@"[A-Za-z_]\w*")] Token name, [Pattern(@"=")] Token eq, Expr value, [Pattern(@";")] Token semi) { Name = name.Text; Value = value; }
}

// print(expr);
public sealed class PrintStmt : Stmt
{
    public Expr Value { get; }
    public PrintStmt([Pattern(@"print", Priority = 1)] Token kw, [Pattern(@"\(")] Token lp, Expr value, [Pattern(@"\)")] Token rp, [Pattern(@";")] Token semi) { Value = value; }
}

// if (expr) stmt
public sealed class IfStmt : Stmt
{
    public Expr Cond { get; }
    public Stmt Body { get; }
    public IfStmt([Pattern(@"if", Priority = 1)] Token kw, [Pattern(@"\(")] Token lp, Expr cond, [Pattern(@"\)")] Token rp, Stmt body) { Cond = cond; Body = body; }
}

// while (expr) stmt
public sealed class WhileStmt : Stmt
{
    public Expr Cond { get; }
    public Stmt Body { get; }
    public WhileStmt([Pattern(@"while", Priority = 1)] Token kw, [Pattern(@"\(")] Token lp, Expr cond, [Pattern(@"\)")] Token rp, Stmt body) { Cond = cond; Body = body; }
}

// { stmt... }
public sealed class BlockStmt : Stmt
{
    public Program Body { get; }
    public BlockStmt([Pattern(@"\{")] Token lb, Program body, [Pattern(@"\}")] Token rb) { Body = body; }
}

// --- 式 ---
public abstract class Expr : AstNode { }

public sealed class NumExpr : Expr
{
    public int Value { get; }
    public NumExpr([Pattern(@"[0-9]+")] Token num) { Value = int.Parse(num.Text); }
}

public sealed class VarExpr : Expr
{
    public string Name { get; }
    public VarExpr([Pattern(@"[A-Za-z_]\w*")] Token name) { Name = name.Text; }
}

[Precedence(1)]
public sealed class AddExpr : Expr
{
    public Expr Left { get; }
    public Expr Right { get; }
    public AddExpr(Expr left, [Pattern(@"\+")] Token op, Expr right) { Left = left; Right = right; }
}

[Precedence(1)]
public sealed class SubExpr : Expr
{
    public Expr Left { get; }
    public Expr Right { get; }
    public SubExpr(Expr left, [Pattern(@"-")] Token op, Expr right) { Left = left; Right = right; }
}

[Precedence(2)]
public sealed class MulExpr : Expr
{
    public Expr Left { get; }
    public Expr Right { get; }
    public MulExpr(Expr left, [Pattern(@"\*")] Token op, Expr right) { Left = left; Right = right; }
}

[Precedence(2)]
public sealed class DivExpr : Expr
{
    public Expr Left { get; }
    public Expr Right { get; }
    public DivExpr(Expr left, [Pattern(@"/")] Token op, Expr right) { Left = left; Right = right; }
}

[Precedence(3)]
public sealed class NegExpr : Expr
{
    public Expr Inner { get; }
    public NegExpr([Pattern(@"-")] Token op, Expr inner) { Inner = inner; }
}

public sealed class ParenExpr : Expr
{
    public Expr Inner { get; }
    public ParenExpr([Pattern(@"\(")] Token lp, Expr inner, [Pattern(@"\)")] Token rp) { Inner = inner; }
}
