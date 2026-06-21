using AstFirst;

namespace Calc;

/// <summary>電卓の式 (フェーズ3 の動作確認用 DSL)。</summary>
[Grammar]
public abstract class Expr : AstNode { }

/// <summary>規則 Expr : [0-9]+</summary>
public sealed class NumExpr : Expr
{
    public int Value { get; }
    public NumExpr([Pattern(@"[0-9]+")] Token num)
    {
        Value = int.Parse(num.Text);
        Span = num.Span;
    }
}

/// <summary>規則 Expr : Expr + Expr</summary>
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

/// <summary>規則 Expr : Expr * Expr</summary>
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
