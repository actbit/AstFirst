using AstFirst;
using AstFirst.Tests.EndToEnd.TokenFeature;

namespace AstFirst.Tests.EndToEnd.TokenFeature.LightGlr;

/// <summary>Token 機能拡張テスト用 LightGlr 文法 (LALR と同構造)。
/// TokExpr (LALR) と別名前空間に置くことで、Generator が両文法で同じ Nodes を収集するのを防ぐ。</summary>
[Grammar(ParseMode = ParseMode.LightGlr)]
[Skip(@"\s+")]
public abstract partial class TokExprLG : AstNode { }

public sealed partial class TokNumLG : TokExprLG
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
public sealed partial class TokAddLG : TokExprLG
{
    public bool OpWasInserted { get; private set; }
    [Rule]
    public static void A(TokExprLG left, [Token(@"\+")] Token op, TokExprLG right) { }
    partial void OnReduce() { OpWasInserted = Op.IsInserted; }
}
