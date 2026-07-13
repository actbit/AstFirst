using AstFirst.Tests.EndToEnd.TokenFeature.Lalr;
using AstFirst.Tests.EndToEnd.TokenFeature.LightGlr;

namespace AstFirst.Tests.EndToEnd.TokenFeature;

/// <summary>Token 機能拡張 (Kind/IsInserted/派生型引き継ぎ) の End-to-End テスト。
/// LALR と LightGlr の両モードで、派生型 NumberToken を通じて各機能を検証する。</summary>
public class TokenFeatureTests
{
    // ===== Kind (派生型 NumberToken に Kind が設定されるか) =====

    [Fact]
    public void Kind_OnDerivedToken_Lalr()
    {
        var result = TokExprParser.Parse("42");
        var num = Assert.IsType<TokNum>(result.Ast);
        Assert.Equal("number", num.CapturedKind);
    }

    [Fact]
    public void Kind_OnDerivedToken_LightGlr()
    {
        var result = TokExprLGParser.Parse("42");
        var num = Assert.IsType<TokNumLG>(result.Ast);
        Assert.Equal("number", num.CapturedKind);
    }

    // ===== IsInserted (演算子挿入: 非派生 Token) =====

    [Fact]
    public void IsInserted_OnOperator_Lalr()
    {
        // 入力 "1 2" (演算子欠落) → ER1 が "+" を挿入 → TokAdd.Op.IsInserted == true
        var result = TokExprParser.Parse("1 2");
        var add = Assert.IsType<TokAdd>(result.Ast);
        Assert.True(add.OpWasInserted);
    }

    // ===== IsInserted (派生型 NumberToken への挿入) =====

    [Fact]
    public void IsInserted_OnDerivedToken_Lalr()
    {
        // 入力 "+1": 先頭で数字が期待されるが "+" → ER1 が数字 (NumberToken) を挿入。
        // __ct_NumberToken で再構築され IsInserted == true を引き継ぐ。
        var result = TokExprParser.Parse("+1");
        var add = Assert.IsType<TokAdd>(result.Ast);
        var leftNum = Assert.IsType<TokNum>(add.Left);
        Assert.True(leftNum.TokenWasInserted);
    }

    [Fact]
    public void IsInserted_OnDerivedToken_LightGlr()
    {
        var result = TokExprLGParser.Parse("+1");
        var add = Assert.IsType<TokAddLG>(result.Ast);
        var leftNum = Assert.IsType<TokNumLG>(add.Left);
        Assert.True(leftNum.TokenWasInserted);
    }
}
