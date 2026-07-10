using AstFirst;

namespace AstFirst.Tests.EndToEnd.GlrTest;

/// <summary>LightGlr モードの End-to-End テスト用文法 (電卓の部分集合)。</summary>
[Grammar(ParseMode = ParseMode.LightGlr)]
[Skip(@"\s+")]
public abstract partial class GlrExpr : AstNode { }

/// <summary>規則 GlrExpr → [0-9]+</summary>
public sealed partial class GlrNum : GlrExpr
{
    public int Value { get; private set; }
    [Rule]
    public static void N([Token(@"[0-9]+")] Token num) { }
    partial void OnReduce() { Value = int.Parse(Num.Text); }
}

/// <summary>規則 GlrExpr → GlrExpr + GlrExpr</summary>
[Precedence(1)]
public sealed partial class GlrAdd : GlrExpr
{
    [Rule]
    public static void A(GlrExpr left, [Token(@"\+")] Token op, GlrExpr right) { }
}
