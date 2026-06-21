using AstFirst.Core.Parsing;

namespace AstFirst.Tests.Parsing;

public class GrammarTests
{
    // 電卓文法: E -> E + T | T , T -> T * F | F , F -> ( E ) | num
    private static Grammar BuildCalcGrammar(out Symbol E, out Symbol T, out Symbol F,
        out Symbol plus, out Symbol star, out Symbol lparen, out Symbol rparen, out Symbol num)
    {
        var b = new GrammarBuilder();
        E = b.NonTerminal("E");
        T = b.NonTerminal("T");
        F = b.NonTerminal("F");
        plus = b.Terminal("+");
        star = b.Terminal("*");
        lparen = b.Terminal("(");
        rparen = b.Terminal(")");
        num = b.Terminal("num");
        b.Production(E, E, plus, T);
        b.Production(E, T);
        b.Production(T, T, star, F);
        b.Production(T, F);
        b.Production(F, lparen, E, rparen);
        b.Production(F, num);
        return b.Build(E);
    }

    [Fact]
    public void TerminalsAndNonTerminalsRegistered()
    {
        var g = BuildCalcGrammar(out var E, out var T, out var F,
            out var plus, out var star, out var lparen, out var rparen, out var num);

        Assert.False(E.IsTerminal);
        Assert.True(plus.IsTerminal);
        Assert.Equal("+", plus.Name);
        Assert.Equal("E", E.Name);
    }

    [Fact]
    public void AugmentedProductionAdded()
    {
        var g = BuildCalcGrammar(out var E, out _, out _, out _, out _, out _, out _, out _);

        // ユーザー規則 6 個 + 拡張 S' -> E $ の 1 個 = 7。
        Assert.Equal(7, g.Productions.Count);
        Assert.Equal("E'", g.AugmentedStart.Name);
        Assert.False(g.AugmentedStart.IsTerminal);
        Assert.True(g.EndOfFile.IsTerminal);
        Assert.Equal("$", g.EndOfFile.Name);

        var aug = g.AugmentedProduction;
        Assert.Equal(g.AugmentedStart, aug.Lhs);
        Assert.Equal(2, aug.Length);
        Assert.Equal(E, aug.Rhs[0]);
        Assert.Equal(g.EndOfFile, aug.Rhs[1]);
    }

    [Fact]
    public void LeftSideMustBeNonTerminal()
    {
        var b = new GrammarBuilder();
        var plus = b.Terminal("+");
        Assert.Throws<ArgumentException>(() => b.Production(plus));
    }

    [Fact]
    public void SymbolsAreUniqueByName()
    {
        var b = new GrammarBuilder();
        var n1 = b.NonTerminal("E");
        var n2 = b.NonTerminal("E");
        Assert.Equal(n1, n2);
        Assert.Equal(n1.Id, n2.Id);
    }

    [Fact]
    public void ProductionHasTag()
    {
        var b = new GrammarBuilder();
        var E = b.NonTerminal("E");
        var num = b.Terminal("num");
        var p = new Production(99, E, new[] { num }, tag: "NumExpr");
        Assert.Equal("NumExpr", p.Tag);
        Assert.Equal(99, p.Id);
        Assert.Equal(1, p.Length);
    }
}
