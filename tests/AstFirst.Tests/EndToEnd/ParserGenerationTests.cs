using Calc;

namespace AstFirst.Tests.EndToEnd;

/// <summary>
/// 本体に書いた文法クラスから生成された ExprParser のエンドツーエンドテスト。
/// Parse は ParseResult (AST + エラー) を返す。
/// </summary>
public class ParserGenerationTests
{
    [Fact]
    public void ParsesToAst()
    {
        // "1+2" → AddExpr(NumExpr(1), +, NumExpr(2))、エラーなし。
        var result = ExprParser.Parse("1+2");
        Assert.False(result.HasErrors);
        var add = Assert.IsType<AddExpr>(result.Ast);
        var left = Assert.IsType<NumExpr>(add.Left);
        var right = Assert.IsType<NumExpr>(add.Right);
        Assert.Equal(1, left.Value);
        Assert.Equal(2, right.Value);
    }

    [Fact]
    public void IncompleteExpressionReportsError()
    {
        // "1+" は + の後に式がない → panic mode でエラー記録して回復。
        var result = ExprParser.Parse("1+");
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void UnexpectedTokenReportsError()
    {
        // "+1" は先頭が + → エラー。
        var result = ExprParser.Parse("+1");
        Assert.True(result.HasErrors);
    }
}
