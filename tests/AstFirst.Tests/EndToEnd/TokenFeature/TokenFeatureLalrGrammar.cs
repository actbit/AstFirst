using AstFirst;
using AstFirst.Tests.EndToEnd.TokenFeature;

namespace AstFirst.Tests.EndToEnd.TokenFeature.Lalr;

/// <summary>Token 機能拡張テスト用 LALR 文法 (電卓の部分集合)。
/// TokExprLG (LightGlr) と別名前空間に置くことで、Generator が両文法で同じ Nodes を収集するのを防ぐ。</summary>
[Grammar]
[Skip(@"\s+")]
public abstract partial class TokExpr : AstNode { }

public sealed partial class TokNum : TokExpr
{
    public string? CapturedKind { get; private set; }
    public bool TokenWasInserted { get; private set; }
    [Rule]
    public static void N([Token(@"[0-9]+", Kind = "number")] NumberToken num) { }
    partial void OnReduce()
    {
        CapturedKind = Num.Kind;
        TokenWasInserted = Num.IsInserted;
    }
}

[Precedence(1)]
public sealed partial class TokAdd : TokExpr
{
    public bool OpWasInserted { get; private set; }
    [Rule]
    public static void A(TokExpr left, [Token(@"\+")] Token op, TokExpr right) { }
    partial void OnReduce() { OpWasInserted = Op.IsInserted; }
}
