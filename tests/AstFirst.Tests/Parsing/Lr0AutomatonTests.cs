using System.Linq;
using AstFirst.Core.Parsing;

namespace AstFirst.Tests.Parsing;

public class Lr0AutomatonTests
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

    [Fact]
    public void BuildsNonTrivialAutomaton()
    {
        var (g, _, _, _, _, _, _, _, _) = BuildCalc();
        var auto = Lr0AutomatonBuilder.Build(g);
        Assert.True(auto.StateCount > 5);
    }

    [Fact]
    public void StartStateHasAugmentedItemAtDot0()
    {
        var (g, _, _, _, _, _, _, _, _) = BuildCalc();
        var auto = Lr0AutomatonBuilder.Build(g);
        var start = auto.States[0];
        Assert.Contains(start.Items, i => i.ProductionId == g.AugmentedProduction.Id && i.Dot == 0);
    }

    [Fact]
    public void StartStateClosureIncludesAllReachableProductions()
    {
        // S' -> .E$ の closure で E(2)/T(2)/F(2) も . で入る = 計 7 項目。
        var (g, _, _, _, _, _, _, _, _) = BuildCalc();
        var auto = Lr0AutomatonBuilder.Build(g);
        var start = auto.States[0];
        Assert.True(start.Count >= 7);
    }

    [Fact]
    public void GotoOnNumLeadsToReduceState()
    {
        var (g, _, _, _, _, _, _, _, num) = BuildCalc();
        var fProdNum = g.Productions.First(p => p.Lhs.Name == "F" && p.Length == 1 && p.Rhs[0].Name == "num");
        var auto = Lr0AutomatonBuilder.Build(g);

        int target = auto.Goto(0, num.Id);
        Assert.True(target > 0);
        var dest = auto.States[target];
        // F -> num . (点が末尾 = 還元項目)
        Assert.Contains(dest.Items, i => i.ProductionId == fProdNum.Id && i.Dot == 1);
    }

    [Fact]
    public void AcceptingStateExists()
    {
        // S' -> E . $ を含む状態がある。
        var (g, _, _, _, _, _, _, _, _) = BuildCalc();
        var auto = Lr0AutomatonBuilder.Build(g);
        bool found = false;
        for (int s = 0; s < auto.StateCount; s++)
        {
            if (auto.States[s].Items.Any(i => i.ProductionId == g.AugmentedProduction.Id && i.Dot == 1))
            {
                found = true;
                break;
            }
        }
        Assert.True(found);
    }

    [Fact]
    public void ItemSetEqualityIgnoresId()
    {
        var items = new[] { new Lr0Item(0, 0), new Lr0Item(1, 0) };
        var a = new Lr0ItemSet(0, items);
        var b = new Lr0ItemSet(99, items);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
