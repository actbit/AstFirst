using AstFirst.Core.Lexing;

namespace AstFirst.Tests.Lexing;

public class RegexParserTests
{
    private static string Canon(string pattern) => RegexParser.Parse(pattern).ToCanonicalString();

    [Fact]
    public void SingleLiteral() => Assert.Equal("a", Canon("a"));

    [Fact]
    public void Concat() => Assert.Equal("(abc)", Canon("abc"));

    [Fact]
    public void Alternate() => Assert.Equal("(a|b)", Canon("a|b"));

    [Fact]
    public void AlternateThree() => Assert.Equal("(a|b|c)", Canon("a|b|c"));

    [Fact]
    public void Star() => Assert.Equal("a*", Canon("a*"));

    [Fact]
    public void Plus() => Assert.Equal("a+", Canon("a+"));

    [Fact]
    public void Optional() => Assert.Equal("a?", Canon("a?"));

    [Fact]
    public void RepeatChain() => Assert.Equal("(a*b+)", Canon("a*b+"));

    [Fact]
    public void Group() => Assert.Equal("a", Canon("(a)"));

    [Fact]
    public void GroupWithAlternate() => Assert.Equal("(a(b|c))", Canon("a(b|c)"));

    [Fact]
    public void AlternationPrecedence() => Assert.Equal("((ab)|(cd))", Canon("ab|cd"));

    [Fact]
    public void CharClassRange() => Assert.Equal("[a-z]", Canon("[a-z]"));

    [Fact]
    public void DigitEscape() => Assert.Equal("[0-9]", Canon("\\d"));

    [Fact]
    public void WordEscape() => Assert.Equal("[0-9A-Z_a-z]", Canon("\\w"));

    [Fact]
    public void EscapedMeta() => Assert.Equal("+", Canon("\\+"));

    [Fact]
    public void AnyChar() => Assert.Equal(".", Canon("."));

    [Fact]
    public void ComplexExpr() => Assert.Equal("((ab)*|c)", Canon("(ab)*|c"));

    [Fact]
    public void EmptyPattern() => Assert.Equal("<e>", Canon(""));

    [Fact]
    public void CharClassNegate()
    {
        var ast = RegexParser.Parse("[^a]") as CharSetAst;
        Assert.NotNull(ast);
        Assert.False(ast!.Set.Contains('a'));
        Assert.True(ast.Set.Contains('b'));
        Assert.True(ast.Set.Contains('0'));
    }

    [Fact]
    public void CharClassWithRangeAndLiteral()
    {
        var ast = RegexParser.Parse("[0-9_]") as CharSetAst;
        Assert.NotNull(ast);
        Assert.True(ast!.Set.Contains('5'));
        Assert.True(ast.Set.Contains('_'));
        Assert.False(ast.Set.Contains('a'));
    }

    [Fact]
    public void UnbalancedParenThrows() =>
        Assert.Throws<RegexParseException>(() => RegexParser.Parse("(a"));

    [Fact]
    public void UnexpectedMetaThrows() =>
        Assert.Throws<RegexParseException>(() => RegexParser.Parse("*"));

    [Fact]
    public void UnclosedCharClassThrows() =>
        Assert.Throws<RegexParseException>(() => RegexParser.Parse("[abc"));
}
