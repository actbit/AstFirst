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

    // --- 量指定子 {m} {m,} {m,n} (RegexParser.ParseRange) ---

    [Fact]
    public void RepeatExact() => Assert.Equal("a{2}", Canon("a{2}"));

    [Fact]
    public void RepeatAtLeast() => Assert.Equal("a{2,}", Canon("a{2,}"));

    [Fact]
    public void RepeatRange() => Assert.Equal("a{2,4}", Canon("a{2,4}"));

    [Fact]
    public void RepeatExactSameMinMax()
    {
        // {3} は Min=Max=3。
        var ast = RegexParser.Parse("a{3}") as RepeatAst;
        Assert.NotNull(ast);
        Assert.Equal(3, ast!.Min);
        Assert.Equal(3, ast.Max);
    }

    [Fact]
    public void RepeatRangeCapturesMinMax()
    {
        // {2,5} は Min=2, Max=5。
        var ast = RegexParser.Parse("[0-9]{2,5}") as RepeatAst;
        Assert.NotNull(ast);
        Assert.Equal(2, ast!.Min);
        Assert.Equal(5, ast.Max);
    }

    [Fact]
    public void RepeatAtLeastCapturesNullMax()
    {
        // {1,} は Min=1, Max=null。
        var ast = RegexParser.Parse("a{1,}") as RepeatAst;
        Assert.NotNull(ast);
        Assert.Equal(1, ast!.Min);
        Assert.Null(ast.Max);
    }

    [Fact]
    public void RepeatCanBeChained()
    {
        // a{2}b{3} → Concat。
        Assert.Equal("(a{2}b{3})", Canon("a{2}b{3}"));
    }
}
