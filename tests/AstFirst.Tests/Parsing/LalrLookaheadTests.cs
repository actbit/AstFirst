using System.Collections.Generic;
using System.Linq;
using AstFirst.Core.Parsing;

namespace AstFirst.Tests.Parsing;

public class LalrLookaheadTests
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

    private static LalrLookahead Build(Grammar g)
    {
        var first = new FirstSet(g);
        var auto = Lr0AutomatonBuilder.Build(g);
        return new LalrLookahead(g, auto, first);
    }

    private static int FindState(Grammar g, Lr0Automaton auto, int prodId, int dot)
    {
        for (int s = 0; s < auto.StateCount; s++)
            if (auto.States[s].Items.Any(i => i.ProductionId == prodId && i.Dot == dot))
                return s;
        return -1;
    }

    [Fact]
    public void AugmentedStartHasDollarLookahead()
    {
        var (g, _, _, _, _, _, _, _, _) = BuildCalc();
        var auto = Lr0AutomatonBuilder.Build(g);
        var lalr = Build(g);
        var la = lalr.Lookahead(0, new Lr0Item(g.AugmentedProduction.Id, 0));
        Assert.Contains(g.EndOfFile.Id, la);
    }

    [Fact]
    public void ReduceItemFNumHasFollowOfF()
    {
        // [F -> num .] の LALR ルックアヘッド = FOLLOW(F) = { +, *, ), $ }
        var (g, _, _, _, plus, star, _, rparen, _) = BuildCalc();
        var auto = Lr0AutomatonBuilder.Build(g);
        var fProdNum = g.Productions.First(p => p.Lhs.Name == "F" && p.Length == 1 && p.Rhs[0].Name == "num");
        var lalr = Build(g);

        int state = FindState(g, auto, fProdNum.Id, 1);
        Assert.True(state >= 0);
        var la = new HashSet<int>(lalr.Lookahead(state, new Lr0Item(fProdNum.Id, 1)));

        Assert.Contains(plus.Id, la);
        Assert.Contains(star.Id, la);
        Assert.Contains(rparen.Id, la);
        Assert.Contains(g.EndOfFile.Id, la);
        Assert.Equal(4, la.Count);
    }

    [Fact]
    public void ReduceItemETHasFollowOfE()
    {
        // [E -> T .] の LALR ルックアヘッド = FOLLOW(E) = { +, ), $ }  (* は含まない)
        var (g, _, _, _, plus, star, _, rparen, _) = BuildCalc();
        var auto = Lr0AutomatonBuilder.Build(g);
        var eProdT = g.Productions.First(p => p.Lhs.Name == "E" && p.Length == 1 && p.Rhs[0].Name == "T");
        var lalr = Build(g);

        int state = FindState(g, auto, eProdT.Id, 1);
        Assert.True(state >= 0);
        var la = new HashSet<int>(lalr.Lookahead(state, new Lr0Item(eProdT.Id, 1)));

        Assert.Contains(plus.Id, la);
        Assert.Contains(rparen.Id, la);
        Assert.Contains(g.EndOfFile.Id, la);
        Assert.DoesNotContain(star.Id, la); // FOLLOW(E) に * は無い
    }

    [Fact]
    public void ShiftItemLookaheadInStartState()
    {
        // 状態0 の [E -> . E + T]: $ (S'->.E$ の closure) と + (β=+T の FIRST)。
        // 開始状態なので ) は来ない (状態0 に ) の文脈はない)。
        var (g, _, _, _, plus, _, _, rparen, _) = BuildCalc();
        var auto = Lr0AutomatonBuilder.Build(g);
        var eProdEPlusT = g.Productions.First(p => p.Lhs.Name == "E" && p.Length == 3);
        var lalr = Build(g);

        int state = FindState(g, auto, eProdEPlusT.Id, 0);
        Assert.True(state >= 0);
        var la = new HashSet<int>(lalr.Lookahead(state, new Lr0Item(eProdEPlusT.Id, 0)));
        Assert.Contains(plus.Id, la);
        Assert.Contains(g.EndOfFile.Id, la);
        Assert.DoesNotContain(rparen.Id, la);
    }
}
