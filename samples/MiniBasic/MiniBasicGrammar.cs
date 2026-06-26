using AstFirst;

namespace MiniBasic;

/// <summary>MiniBASIC パーサのサンプル。行番号付き BASIC（PRINT / LET / IF-THEN / GOTO）([Rule] static モデル)。</summary>

[Grammar]
[Skip(@"[\s]+")]
public abstract partial class Line : AstNode { }

// --- 行リスト ---
public sealed partial class ConsLine : Line
{
    [Rule]
    public static void Cons(Stmt first, Line rest) { }
}
public sealed partial class EndLine : Line
{
    [Rule]
    public static void Nil() { }
}

// --- 文 ---
public abstract partial class Stmt : AstNode { }

// 10 PRINT expr
public sealed partial class PrintStmt : Stmt
{
    [Rule]
    public static void Print([Token(@"[0-9]+")] Token lineNum, [Token(@"PRINT", Priority = 1)] Token kw, Expr value) { }
}

// 20 LET x = expr  (または 20 x = expr)
public sealed partial class LetStmt : Stmt
{
    public string Name { get; private set; } = "";
    [Rule]
    public static void Let([Token(@"[0-9]+")] Token lineNum, [Token(@"LET", Priority = 1)] Token kw, [Token(@"[A-Z]")] Token nameTok, [Token(@"=")] Token eq, Expr value) { }
    partial void OnReduce() { Name = NameTok.Text; }
}

// 30 IF expr THEN 10
public sealed partial class IfStmt : Stmt
{
    public int TargetLine { get; private set; }
    [Rule]
    public static void If([Token(@"[0-9]+")] Token lineNum, [Token(@"IF", Priority = 1)] Token kwIf, Expr cond, [Token(@"THEN", Priority = 1)] Token kwThen, [Token(@"[0-9]+")] Token target) { }
    partial void OnReduce() { TargetLine = int.Parse(Target.Text); }
}

// 30 IF expr THEN GOTO 10  (GOTO 付きは別規則/別クラスに分割)
public sealed partial class IfGotoStmt : Stmt
{
    public int TargetLine { get; private set; }
    [Rule]
    public static void IfGoto([Token(@"[0-9]+")] Token lineNum, [Token(@"IF", Priority = 1)] Token kwIf, Expr cond, [Token(@"THEN", Priority = 1)] Token kwThen, [Token(@"GOTO", Priority = 1)] Token kwGoto, [Token(@"[0-9]+")] Token target) { }
    partial void OnReduce() { TargetLine = int.Parse(Target.Text); }
}

// 40 GOTO 10
public sealed partial class GotoStmt : Stmt
{
    public int TargetLine { get; private set; }
    [Rule]
    public static void Goto([Token(@"[0-9]+")] Token lineNum, [Token(@"GOTO", Priority = 1)] Token kw, [Token(@"[0-9]+")] Token target) { }
    partial void OnReduce() { TargetLine = int.Parse(Target.Text); }
}

// 50 END
public sealed partial class EndStmt : Stmt
{
    [Rule]
    public static void End([Token(@"[0-9]+")] Token lineNum, [Token(@"END", Priority = 1)] Token kw) { }
}

// --- 式 ---
public abstract partial class Expr : AstNode { }

public sealed partial class NumExpr : Expr
{
    public int Value { get; private set; }
    [Rule]
    public static void NumToken([Token(@"[0-9]+")] Token num) { }
    partial void OnReduce() { Value = int.Parse(Num.Text); }
}

public sealed partial class VarExpr : Expr
{
    public string Name { get; private set; } = "";
    [Rule]
    public static void Var([Token(@"[A-Z]")] Token nameTok) { }
    partial void OnReduce() { Name = NameTok.Text; }
}

[Precedence(1)]
public sealed partial class AddExpr : Expr
{
    [Rule]
    public static void Add(Expr left, [Token(@"\+")] Token op, Expr right) { }
}

[Precedence(1)]
public sealed partial class SubExpr : Expr
{
    [Rule]
    public static void Sub(Expr left, [Token(@"-")] Token op, Expr right) { }
}

[Precedence(2)]
public sealed partial class MulExpr : Expr
{
    [Rule]
    public static void Mul(Expr left, [Token(@"\*")] Token op, Expr right) { }
}

[Precedence(3)]
public sealed partial class EqExpr : Expr
{
    [Rule]
    public static void Eq(Expr left, [Token(@"=")] Token op, Expr right) { }
}
