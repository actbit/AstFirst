using AstFirst.Core.Lexing;

namespace AstFirst.Tests.Lexing;

public class DfaMinimizerTests
{
    private static Dfa BuildOriginal(string pattern)
    {
        var ast = RegexParser.Parse(pattern);
        var nfa = NfaBuilder.Build(ast);
        return DfaBuilder.Build(nfa);
    }

    private static Dfa BuildMinimized(string pattern) => DfaMinimizer.Minimize(BuildOriginal(pattern));

    [Fact]
    public void MinimizedMatchesOriginalAcrossInputs()
    {
        string[] patterns = { "a*b", "(a|b)*c", "[0-9]+\\.[0-9]+", "x+", "ab|cd", "a*", "[A-Za-z_][A-Za-z0-9_]*" };
        string[] inputs =
        {
            "", "a", "b", "ab", "aaab", "ac", "bc", "abc", "abcd",
            "12.5", "12.", "x", "xxx", "y", "cd", "ef", "hello_42", "_x", "123abc"
        };
        foreach (var p in patterns)
        {
            var orig = BuildOriginal(p);
            var min = DfaMinimizer.Minimize(orig);
            foreach (var inp in inputs)
                Assert.Equal(DfaSimulator.Matches(orig, inp), DfaSimulator.Matches(min, inp));
        }
    }

    [Fact]
    public void StarIsSingleState()
    {
        // (a|b)* は最小 DFA で 1 状態 (開始=受理、a/b で自己ループ)。
        var min = BuildMinimized("(a|b)*");
        Assert.Single(min.States);
        Assert.True(min.States[min.Start].IsAccept);
    }

    [Fact]
    public void MinimizedNeverLarger()
    {
        string[] patterns = { "a*b", "(a|b)*c", "x+", "ab|cd", "[0-9]+" };
        foreach (var p in patterns)
        {
            var orig = BuildOriginal(p);
            var min = DfaMinimizer.Minimize(orig);
            Assert.True(min.States.Count <= orig.States.Count, $"pattern {p}: {min.States.Count} > {orig.States.Count}");
        }
    }

    [Fact]
    public void StartAndAcceptPreserved()
    {
        var min = BuildMinimized("ab");
        Assert.True(DfaSimulator.Matches(min, "ab"));
        Assert.False(DfaSimulator.Matches(min, "a"));
        Assert.False(DfaSimulator.Matches(min, "ba"));
        Assert.False(DfaSimulator.Matches(min, "abc"));
    }

    [Fact]
    public void IdentifierLikePattern()
    {
        var min = BuildMinimized("[A-Za-z_][A-Za-z0-9_]*");
        Assert.True(DfaSimulator.Matches(min, "hello_42"));
        Assert.True(DfaSimulator.Matches(min, "_x"));
        Assert.False(DfaSimulator.Matches(min, "9lives"));
        Assert.False(DfaSimulator.Matches(min, ""));
    }
}
