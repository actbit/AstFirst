using AstFirst.Core.Lexing;

namespace AstFirst.Tests.Lexing;

public class DfaBuilderTests
{
    private static bool DfaMatch(string pattern, string input)
    {
        var ast = RegexParser.Parse(pattern);
        var nfa = NfaBuilder.Build(ast);
        var dfa = DfaBuilder.Build(nfa);
        return DfaSimulator.Matches(dfa, input);
    }

    [Theory]
    [InlineData("a", "a", true)]
    [InlineData("a", "b", false)]
    [InlineData("a", "", false)]
    [InlineData("a*", "", true)]
    [InlineData("a*", "aaa", true)]
    [InlineData("a*", "aab", false)]
    [InlineData("a+b", "aaab", true)]
    [InlineData("a+b", "b", false)]
    [InlineData("(a|b)+c", "abac", true)]
    [InlineData("(a|b)+c", "abc", true)]
    [InlineData("(a|b)+c", "c", false)]
    [InlineData("[0-9]+", "123", true)]
    [InlineData("[0-9]+", "12a", false)]
    [InlineData("\\d+", "42", true)]
    [InlineData("\\w+", "hello_123", true)]
    [InlineData("\\w+", "hello 123", false)]
    [InlineData("a.c", "abc", true)]
    [InlineData("a.c", "axc", true)]
    [InlineData("(ab)*", "ababab", true)]
    [InlineData("(ab)*", "aba", false)]
    [InlineData("[^0-9]+", "abc", true)]
    [InlineData("[^0-9]+", "ab1", false)]
    public void Matches(string pattern, string input, bool expected)
    {
        Assert.Equal(expected, DfaMatch(pattern, input));
    }

    [Fact]
    public void DfaAndNfaAgreeAcrossInputs()
    {
        // DFA と NFA の受理判定が様々な入力で完全に一致すること。
        string[] patterns = { "a*b", "(a|b)*c", "[0-9]+\\.[0-9]+", "x+", "ab|cd" };
        string[] inputs =
        {
            "", "a", "b", "ab", "aaab", "ac", "bc", "abc", "abcd",
            "12.5", "12.", "1.2.3", "x", "xxx", "y", "cd", "ef"
        };
        foreach (var p in patterns)
        {
            var ast = RegexParser.Parse(p);
            var nfa = NfaBuilder.Build(ast);
            var dfa = DfaBuilder.Build(nfa);
            foreach (var inp in inputs)
                Assert.Equal(
                    NfaSimulator.Matches(nfa, inp),
                    DfaSimulator.Matches(dfa, inp));
        }
    }
}
