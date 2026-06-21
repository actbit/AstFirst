using System.Collections.Generic;
using System.Linq;
using AstFirst.Core.Parsing;

namespace AstFirst.Tests.Parsing;

public class FirstSetTests
{
    // E -> E + T | T , T -> T * F | F , F -> ( E ) | num
    private static (Grammar g, Symbol E, Symbol T, Symbol F, Symbol plus, Symbol star,
        Symbol lparen, Symbol rparen, Symbol num) BuildCalc()
    {
        var b = new GrammarBuilder();
        var E = b.NonTerminal("E");
        var T = b.NonTerminal("T");
        var F = b.NonTerminal("F");
        var plus = b.Terminal("+");
        var star = b.Terminal("*");
        var lparen = b.Terminal("(");
        var rparen = b.Terminal(")");
        var num = b.Terminal("num");
        b.Production(E, E, plus, T);
        b.Production(E, T);
        b.Production(T, T, star, F);
        b.Production(T, F);
        b.Production(F, lparen, E, rparen);
        b.Production(F, num);
        return (b.Build(E), E, T, F, plus, star, lparen, rparen, num);
    }

    private static HashSet<int> FirstOf(FirstSet first, Symbol s) => new HashSet<int>(first.FirstOf(s));

    [Fact]
    public void TerminalFirstIsItself()
    {
        var (g, _, _, _, plus, _, _, _, _) = BuildCalc();
        var first = new FirstSet(g);
        var fp = FirstOf(first, plus);
        Assert.Single(fp);
        Assert.Contains(plus.Id, fp);
    }

    [Fact]
    public void NonTerminalFirstDerived()
    {
        // FIRST(E) = FIRST(T) = FIRST(F) = { '(', num }
        var (g, E, T, F, _, _, lparen, _, num) = BuildCalc();
        var first = new FirstSet(g);
        var fe = FirstOf(first, E);
        Assert.Equal(2, fe.Count);
        Assert.Contains(lparen.Id, fe);
        Assert.Contains(num.Id, fe);

        Assert.Equal(fe, FirstOf(first, T));
        Assert.Equal(fe, FirstOf(first, F));
    }

    [Fact]
    public void CalcGrammarHasNoNullable()
    {
        var (g, E, T, F, plus, star, lparen, rparen, num) = BuildCalc();
        var first = new FirstSet(g);
        Assert.False(first.IsNullable(E));
        Assert.False(first.IsNullable(T));
        Assert.False(first.IsNullable(F));
        Assert.False(first.IsNullable(plus));
    }

    [Fact]
    public void FirstOfSequenceStopsAtFirstNonNullable()
    {
        // ( E + ) の FIRST は { '(' }
        var (g, E, _, _, plus, _, lparen, _, _) = BuildCalc();
        var first = new FirstSet(g);
        var (firsts, allNullable) = first.FirstOfSequence(new[] { lparen, E, plus });
        Assert.Contains(lparen.Id, firsts);
        Assert.False(allNullable);
    }

    [Fact]
    public void NullableProduction()
    {
        // S -> A a , A -> a | ε  (ε = 空右辺)
        var b = new GrammarBuilder();
        var S = b.NonTerminal("S");
        var A = b.NonTerminal("A");
        var a = b.Terminal("a");
        b.Production(S, A, a);
        b.Production(A, a);
        b.Production(A); // A -> ε
        var g = b.Build(S);

        var first = new FirstSet(g);
        Assert.True(first.IsNullable(A));
        var fa = FirstOf(first, A);
        Assert.Contains(a.Id, fa);
        Assert.False(first.IsNullable(S));
    }
}
