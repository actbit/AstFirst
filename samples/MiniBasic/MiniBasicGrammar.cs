using AstFirst;

namespace MiniBasic;

/// <summary>MiniBASIC パーサのサンプル。行番号付き BASIC（PRINT / LET / IF-THEN / GOTO）。</summary>

[Grammar]
[Skip(@"[\s]+")]
public abstract class Line : AstNode { }

// --- 行リスト ---
public sealed class ConsLine : Line
{
    public Stmt First { get; }
    public Line Rest { get; }
    public ConsLine(Stmt first, Line rest) { First = first; Rest = rest; }
}
public sealed class EndLine : Line { public EndLine() { } }

// --- 文 ---
public abstract class Stmt : AstNode { }

// 10 PRINT expr
public sealed class PrintStmt : Stmt
{
    public Expr Value { get; }
    public PrintStmt([Pattern(@"[0-9]+")] Token lineNum, [Pattern(@"PRINT", Priority = 1)] Token kw, Expr value) { Value = value; }
}

// 20 LET x = expr  (または 20 x = expr)
public sealed class LetStmt : Stmt
{
    public string Name { get; }
    public Expr Value { get; }
    public LetStmt([Pattern(@"[0-9]+")] Token lineNum, [Pattern(@"LET", Priority = 1)] Token kw, [Pattern(@"[A-Z]")] Token name, [Pattern(@"=")] Token eq, Expr value) { Name = name.Text; Value = value; }
}

// 30 IF expr THEN GOTO 10  (または 30 IF expr THEN 10)
public sealed class IfStmt : Stmt
{
    public Expr Cond { get; }
    public int TargetLine { get; }
    public IfStmt([Pattern(@"[0-9]+")] Token lineNum, [Pattern(@"IF", Priority = 1)] Token kwIf, Expr cond, [Pattern(@"THEN", Priority = 1)] Token kwThen, [Pattern(@"GOTO", Priority = 1)] Token kwGoto, [Pattern(@"[0-9]+")] Token target) { Cond = cond; TargetLine = int.Parse(target.Text); }
    public IfStmt([Pattern(@"[0-9]+")] Token lineNum, [Pattern(@"IF", Priority = 1)] Token kwIf, Expr cond, [Pattern(@"THEN", Priority = 1)] Token kwThen, [Pattern(@"[0-9]+")] Token target) { Cond = cond; TargetLine = int.Parse(target.Text); }
}

// 40 GOTO 10
public sealed class GotoStmt : Stmt
{
    public int TargetLine { get; }
    public GotoStmt([Pattern(@"[0-9]+")] Token lineNum, [Pattern(@"GOTO", Priority = 1)] Token kw, [Pattern(@"[0-9]+")] Token target) { TargetLine = int.Parse(target.Text); }
}

// 50 END
public sealed class EndStmt : Stmt
{
    public EndStmt([Pattern(@"[0-9]+")] Token lineNum, [Pattern(@"END", Priority = 1)] Token kw) { }
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
    public VarExpr([Pattern(@"[A-Z]")] Token name) { Name = name.Text; }
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

[Precedence(3)]
public sealed class EqExpr : Expr
{
    public Expr Left { get; }
    public Expr Right { get; }
    public EqExpr(Expr left, [Pattern(@"=")] Token op, Expr right) { Left = left; Right = right; }
}
