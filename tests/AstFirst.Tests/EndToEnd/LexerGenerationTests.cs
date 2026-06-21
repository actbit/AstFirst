using Calc;

namespace AstFirst.Tests.EndToEnd;

/// <summary>
/// 本体 (AstFirst) に書いた [Grammar] DSL から Generator が生成した
/// ExprLexer を実際に呼び出すエンドツーエンドテスト。
/// </summary>
public class LexerGenerationTests
{
    [Fact]
    public void GeneratedLexerTokenizesNumbersAndOperators()
    {
        var toks = ExprLexer.Tokenize("123+456*7");
        Assert.Equal(5, toks.Count);
        Assert.Equal("123", toks[0].Text);
        Assert.Equal("+", toks[1].Text);
        Assert.Equal("456", toks[2].Text);
        Assert.Equal("*", toks[3].Text);
        Assert.Equal("7", toks[4].Text);
    }

    [Fact]
    public void GeneratedLexerRejectsUnknownChar()
    {
        // '@' はどの [Pattern] にもマッチしない → LexException。
        Assert.Throws<AstFirst.Core.Lexing.LexException>(() => ExprLexer.Tokenize("1@2"));
    }

    [Fact]
    public void GeneratedLexerHandlesEmptyInput()
    {
        var toks = ExprLexer.Tokenize("");
        Assert.Empty(toks);
    }
}
