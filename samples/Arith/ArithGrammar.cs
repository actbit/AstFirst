using AstFirst;

namespace Arith;

/// <summary>括弧付き四則演算のサンプル。式をパースして AST を得る。</summary>

[Grammar]
[Skip(@"\s+")]
public abstract class Expr : AstNode { }

// --- 値 ---

public sealed class NumExpr : Expr
{
    public int Value { get; }
    public NumExpr([Pattern(@"[0-9]+")] Token num)
    {
        Value = int.Parse(num.Text);
        Span = num.Span;
    }
}

// ( Expr )
public sealed class ParenExpr : Expr
{
    public Expr Inner { get; }
    public ParenExpr([Pattern(@"\(")] Token lp, Expr inner, [Pattern(@"\)")] Token rp)
    {
        Inner = inner;
        Span = inner.Span;
    }
}

// --- 二項演算 (優先度: + - が 1、* / が 2。左結合) ---

[Precedence(1)]
public sealed class AddExpr : Expr
{
    public Expr Left { get; }
    public Expr Right { get; }
    public AddExpr(Expr left, [Pattern(@"\+")] Token op, Expr right)
    {
        Left = left;
        Right = right;
        Span = SourceSpan.Merge(left.Span, right.Span);
    }
}

[Precedence(1)]
public sealed class SubExpr : Expr
{
    public Expr Left { get; }
    public Expr Right { get; }
    public SubExpr(Expr left, [Pattern(@"-")] Token op, Expr right)
    {
        Left = left;
        Right = right;
        Span = SourceSpan.Merge(left.Span, right.Span);
    }
}

[Precedence(2)]
public sealed class MulExpr : Expr
{
    public Expr Left { get; }
    public Expr Right { get; }
    public MulExpr(Expr left, [Pattern(@"\*")] Token op, Expr right)
    {
        Left = left;
        Right = right;
        Span = SourceSpan.Merge(left.Span, right.Span);
    }
}

[Precedence(2)]
public sealed class DivExpr : Expr
{
    public Expr Left { get; }
    public Expr Right { get; }
    public DivExpr(Expr left, [Pattern(@"/")] Token op, Expr right)
    {
        Left = left;
        Right = right;
        Span = SourceSpan.Merge(left.Span, right.Span);
    }
}
