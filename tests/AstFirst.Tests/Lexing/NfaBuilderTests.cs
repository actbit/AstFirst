using AstFirst.Core.Lexing;

namespace AstFirst.Tests.Lexing;

public class NfaBuilderTests
{
    // pattern が input 全体にマッチするか (完全一致)。
    private static bool Matches(string pattern, string input)
    {
        var ast = RegexParser.Parse(pattern);
        var nfa = NfaBuilder.Build(ast);
        return NfaSimulator.Matches(nfa, input);
    }

    [Fact]
    public void LiteralMatch()
    {
        Assert.True(Matches("a", "a"));
        Assert.False(Matches("a", "b"));
    }

    [Fact]
    public void ConcatMatch()
    {
        Assert.True(Matches("ab", "ab"));
        Assert.False(Matches("ab", "a"));
        Assert.False(Matches("ab", "abc")); // 完全一致
    }

    [Fact]
    public void AlternateMatch()
    {
        Assert.True(Matches("a|b", "a"));
        Assert.True(Matches("a|b", "b"));
        Assert.False(Matches("a|b", "c"));
    }

    [Fact]
    public void StarMatch()
    {
        Assert.True(Matches("a*", ""));
        Assert.True(Matches("a*", "aaa"));
        Assert.False(Matches("a*", "aab"));
    }

    [Fact]
    public void PlusMatch()
    {
        Assert.False(Matches("a+", ""));
        Assert.True(Matches("a+", "a"));
        Assert.True(Matches("a+", "aaa"));
    }

    [Fact]
    public void OptionalMatch()
    {
        Assert.True(Matches("a?", ""));
        Assert.True(Matches("a?", "a"));
        Assert.False(Matches("a?", "aa"));
    }

    [Fact]
    public void CharClassMatch()
    {
        Assert.True(Matches("[0-9]+", "123"));
        Assert.False(Matches("[0-9]+", "12a"));
    }

    [Fact]
    public void DigitEscapeMatch()
    {
        Assert.True(Matches("\\d+", "42"));
        Assert.False(Matches("\\d+", "x"));
    }

    [Fact]
    public void GroupRepeatMatch()
    {
        Assert.True(Matches("(ab)+", "abab"));
        Assert.False(Matches("(ab)+", "aba"));
    }

    [Fact]
    public void GroupAlternateConcat()
    {
        Assert.True(Matches("(a|b)c", "ac"));
        Assert.True(Matches("(a|b)c", "bc"));
        Assert.False(Matches("(a|b)c", "cc"));
    }

    [Fact]
    public void AnyCharMatch()
    {
        Assert.True(Matches("a.c", "abc"));
        Assert.True(Matches("a.c", "axc"));
        Assert.False(Matches("a.c", "a\nc")); // '.' は改行を含まない
    }

    [Fact]
    public void NegatedClassMatch()
    {
        Assert.True(Matches("[^0-9]+", "abc"));
        Assert.False(Matches("[^0-9]+", "ab1"));
    }
}
