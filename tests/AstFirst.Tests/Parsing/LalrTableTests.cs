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
    public void DanglingElseResolvesToShift()
    {
        // S -> i E t S | i E t S e S | a , E -> b
        // (古典的 shift-reduce: else の e で shift か reduce か)
        // bison 互換: 優先度未設定の shift-reduce は shift 優先で解決 (報告なし)。
        // dangling else は shift (else は最内 if に結合) となる。
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

        // shift 優先で解決されるため shift-reduce コンフリクトは残らない。
        Assert.DoesNotContain(table.Conflicts, c => c.Description.Contains("shift-reduce"));
    }

    [Fact]
    public void GotoOnNonTerminal()
    {
        var (table, g, E, _, _, _, _, _, _, _) = BuildCalc();
        // 状態0 の E で GOTO > 0。
        int gt = table.Goto(0, E.Id);
        Assert.True(gt > 0);
    }

    // --- コンフリクト解決 (優先度/結合性)。yacc 互換 (LalrTable.cs:182-198) ---

    private static (LalrTable table, Grammar g, Symbol E, Symbol plus, Lr0Automaton auto)
        BuildAmbiguousPlus(Associativity assoc)
    {
        // E -> E + E | num (+ は指定の結合性、priority 1)。
        var b = new GrammarBuilder();
        var E = b.NonTerminal("E");
        var plus = b.Terminal("+");
        var num = b.Terminal("num");
        b.SetPrecedence(plus, 1, assoc);
        b.Production(E, E, plus, E);
        b.Production(E, num);
        var g = b.Build(E);
        var first = new FirstSet(g);
        var auto = Lr0AutomatonBuilder.Build(g);
        var la = new LalrLookahead(g, auto, first);
        return (LalrTableBuilder.Build(g, auto, la), g, E, plus, auto);
    }

    private static int ReduceDot3State(Lr0Automaton auto, Grammar g, Symbol op)
    {
        // [E -> E + E .] (dot=3) の状態を探す。
        var prod = g.Productions.First(p => p.Length == 3 && p.Rhs[1].Equals(op));
        return StateWith(auto, prod.Id, 3);
    }

    [Fact]
    public void LeftAssociativeResolvesShiftReduceToReduce()
    {
        var (table, g, _, plus, auto) = BuildAmbiguousPlus(Associativity.Left);
        Assert.False(table.HasConflicts);
        // [E -> E + E .] で + を見たら Reduce (左結合)。
        Assert.Equal(LrActionKind.Reduce, table.Action(ReduceDot3State(auto, g, plus), plus.Id).Kind);
    }

    [Fact]
    public void RightAssociativeResolvesShiftReduceToShift()
    {
        var (table, g, _, plus, auto) = BuildAmbiguousPlus(Associativity.Right);
        Assert.False(table.HasConflicts);
        Assert.Equal(LrActionKind.Shift, table.Action(ReduceDot3State(auto, g, plus), plus.Id).Kind);
    }

    [Fact]
    public void NonAssociativeResolvesShiftReduceToError()
    {
        var (table, g, _, plus, auto) = BuildAmbiguousPlus(Associativity.NonAssoc);
        Assert.False(table.HasConflicts);
        Assert.Equal(LrActionKind.Error, table.Action(ReduceDot3State(auto, g, plus), plus.Id).Kind);
    }

    [Fact]
    public void HigherPrecedenceShiftsLowerReduces()
    {
        // * (priority 2) は + (priority 1) より強い: a+b*c, a*b+c。
        var b = new GrammarBuilder();
        var E = b.NonTerminal("E");
        var plus = b.Terminal("+");
        var star = b.Terminal("*");
        var num = b.Terminal("num");
        b.SetPrecedence(plus, 1, Associativity.Left);
        b.SetPrecedence(star, 2, Associativity.Left);
        b.Production(E, E, plus, E);
        b.Production(E, E, star, E);
        b.Production(E, num);
        var g = b.Build(E);
        var first = new FirstSet(g);
        var auto = Lr0AutomatonBuilder.Build(g);
        var la = new LalrLookahead(g, auto, first);
        var table = LalrTableBuilder.Build(g, auto, la);
        Assert.False(table.HasConflicts);
        // [E -> E + E .] で * を見たら Shift (* が強い)。
        Assert.Equal(LrActionKind.Shift, table.Action(ReduceDot3State(auto, g, plus), star.Id).Kind);
        // [E -> E * E .] で + を見たら Reduce (+ が弱い)。
        Assert.Equal(LrActionKind.Reduce, table.Action(ReduceDot3State(auto, g, star), plus.Id).Kind);
    }

    [Fact]
    public void UnresolvedShiftReduceDefaultsToShift()
    {
        // 優先度未設定の E -> E+E | num。
        // bison 互換: 優先度未設定の shift-reduce は shift 優先で解決 (報告なし)。
        var b = new GrammarBuilder();
        var E = b.NonTerminal("E");
        var plus = b.Terminal("+");
        var num = b.Terminal("num");
        b.Production(E, E, plus, E);
        b.Production(E, num);
        var g = b.Build(E);
        var first = new FirstSet(g);
        var auto = Lr0AutomatonBuilder.Build(g);
        var la = new LalrLookahead(g, auto, first);
        var table = LalrTableBuilder.Build(g, auto, la);
        // shift 優先で解決されるため shift-reduce コンフリクトは残らない。
        Assert.DoesNotContain(table.Conflicts, c => c.Description.Contains("shift-reduce"));
        // [E -> E + E .] で + を見たら Shift (bison 互換の既定)。
        Assert.Equal(LrActionKind.Shift, table.Action(ReduceDot3State(auto, g, plus), plus.Id).Kind);
    }

    [Fact]
    public void ReduceReduceConflictDetected()
    {
        // S -> A | B; A -> x; B -> x。x の状態で A->x と B->x が競合。
        var b = new GrammarBuilder();
        var S = b.NonTerminal("S");
        var A = b.NonTerminal("A");
        var B = b.NonTerminal("B");
        var x = b.Terminal("x");
        b.Production(S, A);
        b.Production(S, B);
        b.Production(A, x);
        b.Production(B, x);
        var g = b.Build(S);
        var first = new FirstSet(g);
        var auto = Lr0AutomatonBuilder.Build(g);
        var la = new LalrLookahead(g, auto, first);
        var table = LalrTableBuilder.Build(g, auto, la);
        Assert.Contains(table.Conflicts, c => c.Description.Contains("reduce-reduce"));
    }
}
