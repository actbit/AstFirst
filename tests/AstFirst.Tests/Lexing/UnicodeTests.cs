using AstFirst.Core.Lexing;

namespace AstFirst.Tests.Lexing;

public class UnicodeTests
{
    [Fact]
    public void UnicodeEscapeBmp()
    {
        // A = 'A'
        var ast = RegexParser.Parse(@"A");
        Assert.IsType<LiteralAst>(ast);
        Assert.Equal('A', ((LiteralAst)ast).Ch);
    }

    [Fact]
    public void UnicodeEscapeSupplementaryMatchesEmoji()
    {
        // \U0001F600 = 😀 (補助面、サロゲートペア)
        var nfa = NfaBuilder.Build(RegexParser.Parse(@"\U0001F600"));
        Assert.True(NfaSimulator.Matches(nfa, "😀"));
        Assert.False(NfaSimulator.Matches(nfa, "a"));
    }

    [Fact]
    public void DfaMatchesSupplementary()
    {
        var dfa = DfaBuilder.Build(NfaBuilder.Build(RegexParser.Parse(@"\U0001F600")));
        Assert.True(DfaSimulator.Matches(dfa, "😀"));
        Assert.False(DfaSimulator.Matches(dfa, "a"));
    }

    [Fact]
    public void DirectEmojiInPattern()
    {
        // パターンに直接絵文字を書く (サロゲートペア)
        var dfa = DfaBuilder.Build(NfaBuilder.Build(RegexParser.Parse("😀")));
        Assert.True(DfaSimulator.Matches(dfa, "😀"));
    }

    [Fact]
    public void SupplementaryWithRepeat()
    {
        // 😀+ (1回以上)
        var dfa = DfaBuilder.Build(NfaBuilder.Build(RegexParser.Parse(@"\U0001F600+")));
        Assert.True(DfaSimulator.Matches(dfa, "😀"));
        Assert.True(DfaSimulator.Matches(dfa, "😀😀😀"));
        Assert.False(DfaSimulator.Matches(dfa, ""));
    }

    [Fact]
    public void SupplementaryInLexer()
    {
        var rules = new[] { new LexerRule(@"\U0001F600", 1) };
        var dfa = LexerBuilder.BuildDfa(rules);
        var lex = new Lexer(dfa, rules, "😀😀");
        var toks = lex.Tokenize();
        Assert.Equal(2, toks.Count);
    }

    [Fact]
    public void UnicodeEscapeInCharClassThrows()
    {
        // 文字クラス内の \U はエラー
        Assert.Throws<RegexParseException>(() => RegexParser.Parse(@"[\U0001F600]"));
    }
}
