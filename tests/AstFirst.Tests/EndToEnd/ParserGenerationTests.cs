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
    public void AcceptsValidExpression()
    {
        // 例外が飛ばなければ受理。
        ExprParser.Parse("1+2");
        ExprParser.Parse("1+2+3");
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
