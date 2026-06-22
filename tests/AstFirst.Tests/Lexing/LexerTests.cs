using AstFirst.Core.Lexing;

namespace AstFirst.Tests.Lexing;

public class LexerTests
{
    private static List<LexToken> Lex(string source, params (string pattern, int id)[] rules)
        => LexWithPriority(source, rules.Select(r => new LexerRule(r.pattern, r.id)).ToArray());

    private static List<LexToken> LexWithPriority(string source, params LexerRule[] rules)
    {
        var dfa = LexerBuilder.BuildDfa(rules);
        return new Lexer(dfa, rules, source).Tokenize();
    }

    [Fact]
    public void SimpleTokens()
    {
        var toks = Lex("1+2", ("[0-9]+", 1), ("\\+", 2));
        Assert.Equal(3, toks.Count);
        Assert.Equal((1, "1"), (toks[0].TokenId, toks[0].Text));
        Assert.Equal((2, "+"), (toks[1].TokenId, toks[1].Text));
        Assert.Equal((1, "2"), (toks[2].TokenId, toks[2].Text));
    }

    [Fact]
    public void LineColumn_SingleLine()
    {
        var toks = Lex("1+2", ("[0-9]+", 1), ("\\+", 2));
        Assert.Equal((1, 1), (toks[0].StartLine, toks[0].StartColumn));
        Assert.Equal((1, 2), (toks[1].StartLine, toks[1].StartColumn));
        Assert.Equal((1, 3), (toks[2].StartLine, toks[2].StartColumn));
    }

    [Fact]
    public void LineColumn_MultiLine()
    {
        var toks = LexWithPriority("1 2\n3",
            new LexerRule("[0-9]+", 1),
            new LexerRule("[ \n]+", 2, isHidden: true));
        // "1"(1,1), "2"(1,3), "3"(2,1) — hidden の空白/改行も行・列を進める
        Assert.Equal((1, 1), (toks[0].StartLine, toks[0].StartColumn));
        Assert.Equal((1, 3), (toks[1].StartLine, toks[1].StartColumn));
        Assert.Equal((2, 1), (toks[2].StartLine, toks[2].StartColumn));
    }

    [Fact]
    public void LongestMatch()
    {
        // "123" は [0-9]+ 全体で1トークン ([0-9] 単発より最長一致)。
        var toks = Lex("123", ("[0-9]", 1), ("[0-9]+", 2));
        Assert.Single(toks);
        Assert.Equal(2, toks[0].TokenId);
        Assert.Equal("123", toks[0].Text);
    }

    [Fact]
    public void SameLengthLowerIdWins()
    {
        // "if" は [a-z]+(1) と "if"(2) の両方にマッチ、同長・同優先度 → TokenId 小さい方(1)。
        var toks = Lex("if", ("[a-z]+", 1), ("if", 2));
        Assert.Single(toks);
        Assert.Equal(1, toks[0].TokenId);
    }

    [Fact]
    public void KeywordPriorityWins()
    {
        // "if" をキーワード(2, 優先度高=1) が識別子(1, 優先度0) に勝つ。
        var toks = LexWithPriority("if",
            new LexerRule("[a-z]+", 1, priority: 0),
            new LexerRule("if", 2, priority: 1));
        Assert.Single(toks);
        Assert.Equal(2, toks[0].TokenId);
    }

    [Fact]
    public void HiddenTokenSkipped()
    {
        var toks = LexWithPriority("1 2  3",
            new LexerRule("[0-9]+", 1),
            new LexerRule("[ \t]+", 2, isHidden: true));
        Assert.Equal(3, toks.Count);
        Assert.All(toks, t => Assert.Equal(1, t.TokenId));
        Assert.Equal("1", toks[0].Text);
        Assert.Equal("2", toks[1].Text);
        Assert.Equal("3", toks[2].Text);
    }

    [Fact]
    public void UnknownCharThrows()
    {
        Assert.Throws<LexException>(() => Lex("1@2", ("[0-9]+", 1)));
    }

    [Fact]
    public void IdentifierWithDigits()
    {
        var toks = Lex("abc123", ("[a-z][a-z0-9]*", 1));
        Assert.Single(toks);
        Assert.Equal("abc123", toks[0].Text);
    }

    [Fact]
    public void ArithmeticExpression()
    {
        var toks = LexWithPriority("12 + 34 * 5",
            new LexerRule("[0-9]+", 1),
            new LexerRule("\\+", 2),
            new LexerRule("\\*", 3),
            new LexerRule("[ ]+", 4, isHidden: true));
        Assert.Equal(new[] { 1, 2, 1, 3, 1 }, toks.Select(t => t.TokenId).ToArray());
        Assert.Equal("12", toks[0].Text);
        Assert.Equal("5", toks[4].Text);
    }

    [Fact]
    public void TokenPositions()
    {
        var toks = LexWithPriority("ab cd",
            new LexerRule("[a-z]+", 1),
            new LexerRule("[ ]+", 2, isHidden: true));
        Assert.Equal(0, toks[0].Start);
        Assert.Equal(2, toks[0].End);
        Assert.Equal(3, toks[1].Start);
        Assert.Equal(5, toks[1].End);
    }
}
