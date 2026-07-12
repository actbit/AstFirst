using AstFirst.Tests.EndToEnd.GlrTest;

namespace AstFirst.Tests.EndToEnd;

/// <summary>軽量 GLR (LightGlr) モードの End-to-End テスト。
/// LightGlr 文法を Generator に生成させ、LightGlrDriver でパースして単一 AST を得る。</summary>
public class GlrTests
{
    [Fact]
    public void SingleNumber_ProducesNum()
    {
        var result = GlrExprParser.Parse("42");
        Assert.NotNull(result.Ast);
        Assert.Empty(result.Errors);
        Assert.Equal(42, Assert.IsType<GlrNum>(result.Ast).Value);
    }

    [Fact]
    public void Addition_ProducesSingleAst()
    {
        var result = GlrExprParser.Parse("1+2");
        Assert.NotNull(result.Ast);
        Assert.Empty(result.Errors);
        var add = Assert.IsType<GlrAdd>(result.Ast);
        Assert.Equal(1, Assert.IsType<GlrNum>(add.Left).Value);
        Assert.Equal(2, Assert.IsType<GlrNum>(add.Right).Value);
    }

    [Fact]
    public void SyntaxError_IsReported()
    {
        var result = GlrExprParser.Parse("1+");
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void OnAccepted_Called_ForCtxLessNode()
    {
        // GlrNum は ctx なし ([Rule] に SemanticContext なし)。
        // NotifyAccepted override が生成され、OnAccepted が呼ばれることを検証。
        var result = GlrExprParser.Parse("42");
        var num = Assert.IsType<GlrNum>(result.Ast);
        Assert.True(num.OnAcceptedCalled);
    }

    [Fact]
    public void OnAccepted_Called_WhenRouteConverges()
    {
        // "42" → GlrNum が単一ルートとして確定 → OnAccepted が呼ばれる。
        // "1+2" → GlrNum(1) が一度確定 (shift + の前) → OnAccepted。
        //   その後 GlrNum(2) は即座に GlrAdd に還元されるため、単独では確定しない。
        //   GlrAdd が最終的に確定 → GlrAdd の OnAccepted は呼ばれる。
        var result = GlrExprParser.Parse("1+2");
        var add = Assert.IsType<GlrAdd>(result.Ast);
        // Left (GlrNum(1)) は + の shift 前に単独ルートとして存在した → OnAccepted 呼ばれる
        Assert.True(Assert.IsType<GlrNum>(add.Left).OnAcceptedCalled);
    }

    [Fact]
    public void ErrorRepair_DoesNotCrash_OnMalformedInput()
    {
        // int.Parse(Num.Text) を OnReduce で呼ぶ文法でエラー回復を試す。
        // ダミートークン (Text="") が reduce されてもクラッシュしないことを検証。
        // "1++2" → 最初の + の後でエラー → ErrorRepair が挿入/削除を試す
        var result = GlrExprParser.Parse("1++2");
        // クラッシュせず結果が返ること (エラーがあっても OK)
        Assert.NotNull(result);
    }

    [Fact]
    public void LALR_ErrorRepair_DoesNotCrash_WithIntParseInOnReduce()
    {
        // LALR (Calc) で "1++2" → エラー回復が int.Parse でクラッシュしないことを検証。
        var result = Calc.ExprParser.Parse("1++2");
        Assert.NotNull(result);
    }
}
