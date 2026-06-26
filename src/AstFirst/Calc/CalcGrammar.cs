using AstFirst;

namespace Calc;

/// <summary>電卓の式 ([Rule] static モデル)。</summary>
[Grammar]
public abstract partial class Expr : AstNode { }

/// <summary>規則 Expr : [0-9]+</summary>
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

/// <summary>規則 Expr : Expr + Expr</summary>
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

/// <summary>規則 Expr : Expr * Expr</summary>
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
