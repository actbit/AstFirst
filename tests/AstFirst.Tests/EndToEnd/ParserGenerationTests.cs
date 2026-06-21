using System;
using Calc;

namespace AstFirst.Tests.EndToEnd;

/// <summary>
/// 本体に書いた [Grammar] DSL から生成された ExprParser のエンドツーエンドテスト。
/// フェーズ3c-3 時点では値はスタブ (null) だが、LALR 駆動で受理/拒否が判定できる。
/// </summary>
public class ParserGenerationTests
{
    [Fact]
    public void ParsesToAst()
    {
        // "1+2" → AddExpr(NumExpr(1), +, NumExpr(2))
        var ast = ExprParser.Parse("1+2");
        var add = Assert.IsType<AddExpr>(ast);
        var left = Assert.IsType<NumExpr>(add.Left);
        var right = Assert.IsType<NumExpr>(add.Right);
        Assert.Equal(1, left.Value);
        Assert.Equal(2, right.Value);
    }

    [Fact]
    public void RejectsIncompleteExpression()
    {
        // "1+" は + の後に式がない → 構文エラー。
        Assert.ThrowsAny<Exception>(() => ExprParser.Parse("1+"));
    }

    [Fact]
    public void RejectsUnexpectedToken()
    {
        // "+1" は先頭が + → 構文エラー。
        Assert.ThrowsAny<Exception>(() => ExprParser.Parse("+1"));
    }
}
