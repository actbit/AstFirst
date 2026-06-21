using System.Collections.Generic;

namespace AstFirst.Core.Parsing;

/// <summary>LR アクションの種類。</summary>
public enum LrActionKind { Error, Shift, Reduce, Accept }

/// <summary>LR アクション。Shift は Value=遷移先状態、Reduce は Value=規則 id。</summary>
public readonly struct LrAction
{
    public LrActionKind Kind { get; }
    public int Value { get; }

    private LrAction(LrActionKind kind, int value) { Kind = kind; Value = value; }

    public static readonly LrAction Error = new LrAction(LrActionKind.Error, -1);
    public static LrAction Shift(int state) => new LrAction(LrActionKind.Shift, state);
    public static LrAction Reduce(int prodId) => new LrAction(LrActionKind.Reduce, prodId);
    public static readonly LrAction Accept = new LrAction(LrActionKind.Accept, -1);

    public bool Equals(LrAction other) => Kind == other.Kind && Value == other.Value;
    public override bool Equals(object? obj) => obj is LrAction a && Equals(a);
    public override int GetHashCode() => (int)Kind * 31 + Value;

    public override string ToString() => Kind switch
    {
        LrActionKind.Shift => "s" + Value,
        LrActionKind.Reduce => "r" + Value,
        LrActionKind.Accept => "acc",
        _ => ""
    };
}

/// <summary>解析テーブル上の衝突 (shift-reduce / reduce-reduce 等)。</summary>
public sealed class LrConflict
{
    public int State { get; }
    public int SymbolId { get; }
    public LrAction Existing { get; }
    public LrAction Attempted { get; }
    public string Description { get; }

    public LrConflict(int state, int symbolId, LrAction existing, LrAction attempted, string description)
    {
        State = state;
        SymbolId = symbolId;
        Existing = existing;
        Attempted = attempted;
        Description = description;
    }

    public override string ToString() => Description;
}

/// <summary>LALR(1) 解析テーブル。ACTION[状態, 記号] と GOTO[状態, 記号]。</summary>
public sealed class LalrTable
{
    private readonly LrAction[,] _action;
    private readonly int[,] _goto;

    public Grammar Grammar { get; }
    public int StateCount { get; }
    public int SymbolCount { get; }
    public IReadOnlyList<LrConflict> Conflicts { get; }

    public LalrTable(Grammar grammar, LrAction[,] action, int[,] gotoTable, IReadOnlyList<LrConflict> conflicts)
    {
        Grammar = grammar;
        _action = action;
        _goto = gotoTable;
        StateCount = action.GetLength(0);
        SymbolCount = action.GetLength(1);
        Conflicts = conflicts;
    }

    public LrAction Action(int state, int symbolId) => _action[state, symbolId];
    public int Goto(int state, int symbolId) => _goto[state, symbolId];
    public bool HasConflicts => Conflicts.Count > 0;
}

/// <summary>
/// LR(0) オートマトンと LALR(1) ルックアヘッドから ACTION/GOTO テーブルを構築。
/// 衝突 (shift-reduce / reduce-reduce) を検出し、デフォルトで shift 優先
/// (reduce-reduce は先の規則優先) で解決する。衝突は <see cref="LalrTable.Conflicts"/> に残る。
/// </summary>
public static class LalrTableBuilder
{
    public static LalrTable Build(Grammar grammar, Lr0Automaton auto, LalrLookahead lookahead)
    {
        int states = auto.StateCount;
        int symbols = auto.SymbolCount;
        var action = new LrAction[states, symbols];
        var gotoT = new int[states, symbols];
        for (int s = 0; s < states; s++)
            for (int c = 0; c < symbols; c++)
            {
                action[s, c] = LrAction.Error;
                gotoT[s, c] = -1;
            }

        var conflicts = new List<LrConflict>();

        for (int state = 0; state < states; state++)
        {
            var items = auto.States[state].Items;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var prod = grammar.Productions[item.ProductionId];

                if (item.Dot >= prod.Length)
                {
                    // 還元項目 [A -> α.]
                    if (item.ProductionId == grammar.AugmentedProduction.Id)
                    {
                        // S' -> S $ の受け入れ ($ で Accept)。
                        SetAction(action, conflicts, grammar, state, grammar.EndOfFile.Id, LrAction.Accept, prod);
                    }
                    else
                    {
                        foreach (var a in lookahead.Lookahead(state, item))
                            SetAction(action, conflicts, grammar, state, a, LrAction.Reduce(item.ProductionId), prod);
                    }
                }
                else
                {
                    var sym = prod.Rhs[item.Dot];
                    int target = auto.Goto(state, sym.Id);
                    if (target < 0) continue;
                    if (sym.IsTerminal)
                        SetAction(action, conflicts, grammar, state, sym.Id, LrAction.Shift(target), prod);
                    else
                        gotoT[state, sym.Id] = target;
                }
            }
        }

        return new LalrTable(grammar, action, gotoT, conflicts);
    }

    private static void SetAction(LrAction[,] action, List<LrConflict> conflicts, Grammar grammar,
        int state, int symbolId, LrAction newAction, Production prod)
    {
        var existing = action[state, symbolId];
        if (existing.Kind == LrActionKind.Error)
        {
            action[state, symbolId] = newAction;
            return;
        }
        if (existing.Equals(newAction)) return;

        bool shiftReduce = (existing.Kind == LrActionKind.Shift && newAction.Kind == LrActionKind.Reduce)
                        || (existing.Kind == LrActionKind.Reduce && newAction.Kind == LrActionKind.Shift);

        // shift-reduce を優先度/結合性で解決 (yacc 互換)。
        if (shiftReduce)
        {
            var shiftAction = existing.Kind == LrActionKind.Shift ? existing : newAction;
            var reduceAction = existing.Kind == LrActionKind.Reduce ? existing : newAction;
            var reduceProd = grammar.Productions[reduceAction.Value];
            var tokenPrec = grammar.TerminalPrecedence.TryGetValue(symbolId, out var tp) ? (Precedence?)tp : null;
            var rulePrec = RulePrecedence(grammar, reduceProd);
            if (tokenPrec is { } tpv && rulePrec is { } rpv && !tpv.IsDefault && !rpv.IsDefault)
            {
                if (tpv.Priority > rpv.Priority) { action[state, symbolId] = shiftAction; return; }
                if (rpv.Priority > tpv.Priority) return; // reduce 維持
                // 同優先度 → 結合性
                if (tpv.Associativity == Associativity.Left) return; // reduce
                if (tpv.Associativity == Associativity.Right) { action[state, symbolId] = shiftAction; return; }
                action[state, symbolId] = LrAction.Error; // NonAssoc
                return;
            }
        }

        bool reduceReduce = existing.Kind == LrActionKind.Reduce && newAction.Kind == LrActionKind.Reduce;
        string kind = shiftReduce ? "shift-reduce" : reduceReduce ? "reduce-reduce" : "accept";
        var symName = grammar.Symbols[symbolId].Name;
        conflicts.Add(new LrConflict(state, symbolId, existing, newAction,
            $"{kind} conflict at state {state} on '{symName}' (existing {existing}, new {newAction}, rule {prod})"));

        // デフォルト解決: shift を優先。reduce-reduce は既存(先の規則)を維持。
        if (newAction.Kind == LrActionKind.Shift)
            action[state, symbolId] = newAction;
    }

    /// <summary>規則の優先度 = 右辺の最後の終端の優先度 (設定されていなければ null)。</summary>
    private static Precedence? RulePrecedence(Grammar grammar, Production prod)
    {
        for (int i = prod.Rhs.Length - 1; i >= 0; i--)
            if (prod.Rhs[i].IsTerminal && grammar.TerminalPrecedence.TryGetValue(prod.Rhs[i].Id, out var p))
                return p;
        return null;
    }
}
