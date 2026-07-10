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
}
