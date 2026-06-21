using System.Linq;
using AstFirst.Core.Parsing;

namespace AstFirst.Tests.Parsing;

public class LalrTableTests
{
    private static (LalrTable table, Grammar g, Symbol E, Symbol T, Symbol F,
        Symbol plus, Symbol star, Symbol lparen, Symbol rparen, Symbol num) BuildCalc()
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
        var g = b.Build(E);

        var first = new FirstSet(g);
        var auto = Lr0AutomatonBuilder.Build(g);
        var la = new LalrLookahead(g, auto, first);
        var table = LalrTableBuilder.Build(g, auto, la);
        return (table, g, E, T, F, plus, star, lparen, rparen, num);
    }

    private static int StateWith(Lr0Automaton auto, int prodId, int dot)
    {
        for (int s = 0; s < auto.StateCount; s++)
            if (auto.States[s].Items.Any(i => i.ProductionId == prodId && i.Dot == dot))
                return s;
        return -1;
    }

    [Fact]
    public void CalcGrammarHasNoConflicts()
    {
        var (table, _, _, _, _, _, _, _, _, _) = BuildCalc();
        Assert.False(table.HasConflicts, string.Join("\n", table.Conflicts.Select(c => c.Description)));
    }

    [Fact]
    public void StartStateShiftsOnNumAndLparen()
    {
        var (table, g, _, _, _, _, _, lparen, _, num) = BuildCalc();
        var aNum = table.Action(0, num.Id);
        Assert.Equal(LrActionKind.Shift, aNum.Kind);
        var aLparen = table.Action(0, lparen.Id);
        Assert.Equal(LrActionKind.Shift, aLparen.Kind);
    }

    [Fact]
    public void FNumReducesOnFollow()
    {
        // [F -> num .] の状態で、FOLLOW(F)={+,*,),$} で Reduce(F->num)。
        var (table, g, _, _, _, plus, star, _, rparen, num) = BuildCalc();
        var first = new FirstSet(g);
        var auto = Lr0AutomatonBuilder.Build(g);
        var fProdNum = g.Productions.First(p => p.Lhs.Name == "F" && p.Length == 1 && p.Rhs[0].Name == "num");
        int state = StateWith(auto, fProdNum.Id, 1);

        foreach (var symId in new[] { plus.Id, star.Id, rparen.Id, g.EndOfFile.Id })
        {
            var a = table.Action(state, symId);
            Assert.Equal(LrActionKind.Reduce, a.Kind);
            Assert.Equal(fProdNum.Id, a.Value);
        }
    }

    [Fact]
    public void AugmentedAcceptOnDollar()
    {
        // [S' -> E $ .] (dot=2, 還元) の状態で $ で Accept。
        var (table, g, _, _, _, _, _, _, _, _) = BuildCalc();
        var auto = Lr0AutomatonBuilder.Build(g);
        int state = StateWith(auto, g.AugmentedProduction.Id, 2);
        var a = table.Action(state, g.EndOfFile.Id);
        Assert.Equal(LrActionKind.Accept, a.Kind);
    }

    [Fact]
    public void DanglingElseDetectsShiftReduceConflict()
    {
        // S -> i E t S | i E t S e S | a , E -> b
        // (古典的 shift-reduce 衝突: else の e で shift か reduce か)
        var b = new GrammarBuilder();
        var S = b.NonTerminal("S");
        var E = b.NonTerminal("E");
        var i = b.Terminal("i");
        var t = b.Terminal("t");
        var e = b.Terminal("e");
        var aT = b.Terminal("a");
        var bT = b.Terminal("b");
        b.Production(S, i, E, t, S);
        b.Production(S, i, E, t, S, e, S);
        b.Production(S, aT);
        b.Production(E, bT);
        var g = b.Build(S);

        var first = new FirstSet(g);
        var auto = Lr0AutomatonBuilder.Build(g);
        var la = new LalrLookahead(g, auto, first);
        var table = LalrTableBuilder.Build(g, auto, la);

        Assert.True(table.HasConflicts);
        Assert.Contains(table.Conflicts, c => c.Description.Contains("shift-reduce"));
    }

    [Fact]
    public void GotoOnNonTerminal()
    {
        var (table, g, E, _, _, _, _, _, _, _) = BuildCalc();
        // 状態0 の E で GOTO > 0。
        int gt = table.Goto(0, E.Id);
        Assert.True(gt > 0);
    }
}
