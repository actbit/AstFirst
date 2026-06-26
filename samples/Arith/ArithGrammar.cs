using AstFirst;

namespace Arith;

/// <summary>括弧付き四則演算のサンプル。式をパースして AST を得る ([Rule] static モデル)。</summary>

[Grammar]
[Skip(@"\s+")]
public abstract partial class Expr : AstNode { }

// --- 値 ---

public sealed partial class NumExpr : Expr
{
    public int Value { get; private set; }
    [Rule]
    public static void NumToken([Token(@"[0-9]+")] Token num) { }
    partial void OnReduce()
    {
        Value = int.Parse(Num.Text);
        Span = Num.Span;
    }
}

// ( Expr )
public sealed partial class ParenExpr : Expr
{
    [Rule]
    public static void Group([Token(@"\(")] Token lp, Expr inner, [Token(@"\)")] Token rp) { }
    partial void OnReduce()
    {
        Span = Inner.Span;
    }
}

// --- 二項演算 (優先度: + - が 1、* / が 2。左結合) ---

[Precedence(1)]
public sealed partial class AddExpr : Expr
{
    [Rule]
    public static void Add(Expr left, [Token(@"\+")] Token op, Expr right) { }
    partial void OnReduce()
    {
        Span = SourceSpan.Merge(Left.Span, Right.Span);
    }
}

[Precedence(1)]
public sealed partial class SubExpr : Expr
{
    [Rule]
    public static void Sub(Expr left, [Token(@"-")] Token op, Expr right) { }
    partial void OnReduce()
    {
        Span = SourceSpan.Merge(Left.Span, Right.Span);
    }
}

[Precedence(2)]
public sealed partial class MulExpr : Expr
{
    [Rule]
    public static void Mul(Expr left, [Token(@"\*")] Token op, Expr right) { }
    partial void OnReduce()
    {
        Span = SourceSpan.Merge(Left.Span, Right.Span);
    }
}

[Precedence(2)]
public sealed partial class DivExpr : Expr
{
    [Rule]
    public static void Div(Expr left, [Token(@"/")] Token op, Expr right) { }
    partial void OnReduce()
    {
        Span = SourceSpan.Merge(Left.Span, Right.Span);
    }
}
