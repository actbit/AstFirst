using System;
using System.Collections.Generic;

namespace AstFirst.Core.Parsing;

/// <summary>LR(0) 項目: (生成規則, 点の位置)。値型。</summary>
public readonly struct Lr0Item : IEquatable<Lr0Item>
{
    public int ProductionId { get; }
    public int Dot { get; }

    public Lr0Item(int productionId, int dot)
    {
        ProductionId = productionId;
        Dot = dot;
    }

    public bool IsAtEnd(Production p) => Dot >= p.Length;

    public bool Equals(Lr0Item other) => ProductionId == other.ProductionId && Dot == other.Dot;
    public override bool Equals(object? obj) => obj is Lr0Item i && Equals(i);
    public override int GetHashCode() => ProductionId * 31 + Dot;
    public override string ToString() => $"p{ProductionId}@{Dot}";
}

/// <summary>LR(0) 項目集合。項目の内容で等価判定 (Id は無視)。canonical (ソート済み)。</summary>
public sealed class Lr0ItemSet
{
    private readonly Lr0Item[] _items;

    public int Id { get; }
    public IReadOnlyList<Lr0Item> Items => _items;
    public int Count => _items.Length;

    public Lr0ItemSet(int id, IEnumerable<Lr0Item> items)
    {
        Id = id;
        var set = new HashSet<Lr0Item>(items);
        _items = new Lr0Item[set.Count];
        set.CopyTo(_items);
        Array.Sort(_items, CompareItems);
    }

    private static int CompareItems(Lr0Item a, Lr0Item b) =>
        a.ProductionId != b.ProductionId ? a.ProductionId.CompareTo(b.ProductionId) : a.Dot.CompareTo(b.Dot);

    public bool Equals(Lr0ItemSet? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;
        if (_items.Length != other._items.Length) return false;
        for (int i = 0; i < _items.Length; i++)
            if (!_items[i].Equals(other._items[i])) return false;
        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as Lr0ItemSet);

    public override int GetHashCode()
    {
        int h = 0;
        for (int i = 0; i < _items.Length; i++)
            h = unchecked(h * 31 + _items[i].GetHashCode());
        return h;
    }
}

/// <summary>LR(0) オートマトン。状態と goto 遷移表。</summary>
public sealed class Lr0Automaton
{
    public IReadOnlyList<Lr0ItemSet> States { get; }
    public int Start { get; }
    private readonly int[,] _goto;

    public Lr0Automaton(IReadOnlyList<Lr0ItemSet> states, int start, int[,] gotoTable)
    {
        States = states;
        Start = start;
        _goto = gotoTable;
    }

    /// <summary>状態 state で記号 symbolId を読んだ遷移先。未遷移は -1。</summary>
    public int Goto(int state, int symbolId) => _goto[state, symbolId];

    public int StateCount => States.Count;
    public int SymbolCount => _goto.GetLength(1);
}

/// <summary>
/// LR(0) オートマトンを構築する。closure で項目を閉じ、goto で状態を生成。
/// 拡張開始 S' -> . S $ から広がる。
/// </summary>
public static class Lr0AutomatonBuilder
{
    public static Lr0Automaton Build(Grammar grammar)
    {
        // 非終端ごとの生成規則 id リスト。
        var prodsByLhs = new Dictionary<int, List<int>>();
        for (int i = 0; i < grammar.Productions.Count; i++)
        {
            var p = grammar.Productions[i];
            if (!prodsByLhs.TryGetValue(p.Lhs.Id, out var list))
            {
                list = new List<int>();
                prodsByLhs[p.Lhs.Id] = list;
            }
            list.Add(p.Id);
        }

        HashSet<Lr0Item> Closure(IEnumerable<Lr0Item> seeds)
        {
            var closure = new HashSet<Lr0Item>();
            var queue = new Queue<Lr0Item>();
            foreach (var s in seeds)
                if (closure.Add(s)) queue.Enqueue(s);
            while (queue.Count > 0)
            {
                var item = queue.Dequeue();
                var prod = grammar.Productions[item.ProductionId];
                if (item.Dot >= prod.Length) continue;
                var sym = prod.Rhs[item.Dot];
                if (sym.IsTerminal) continue;
                if (prodsByLhs.TryGetValue(sym.Id, out var pids))
                    foreach (var pid in pids)
                    {
                        var ni = new Lr0Item(pid, 0);
                        if (closure.Add(ni)) queue.Enqueue(ni);
                    }
            }
            return closure;
        }

        var startItems = Closure(new[] { new Lr0Item(grammar.AugmentedProduction.Id, 0) });
        var startSet = new Lr0ItemSet(0, startItems);

        var states = new List<Lr0ItemSet> { startSet };
        var index = new Dictionary<Lr0ItemSet, int> { [startSet] = 0 };
        var work = new Queue<int>();
        work.Enqueue(0);

        int symCount = grammar.Symbols.Count;
        var gotoRows = new List<int[]>(Math.Max(8, 1));

        while (work.Count > 0)
        {
            int currentId = work.Dequeue();
            var current = states[currentId];
            var row = new int[symCount];
            for (int i = 0; i < symCount; i++) row[i] = -1;

            // 点の右にある記号を集め、それらだけで goto を計算。
            var afterDot = new HashSet<int>();
            for (int i = 0; i < current.Items.Count; i++)
            {
                var item = current.Items[i];
                var prod = grammar.Productions[item.ProductionId];
                if (item.Dot < prod.Length) afterDot.Add(prod.Rhs[item.Dot].Id);
            }

            foreach (var symId in afterDot)
            {
                var moved = new HashSet<Lr0Item>();
                for (int i = 0; i < current.Items.Count; i++)
                {
                    var item = current.Items[i];
                    var prod = grammar.Productions[item.ProductionId];
                    if (item.Dot < prod.Length && prod.Rhs[item.Dot].Id == symId)
                        moved.Add(new Lr0Item(item.ProductionId, item.Dot + 1));
                }
                if (moved.Count == 0) continue;
                var closure = Closure(moved);
                var probe = new Lr0ItemSet(-1, closure);
                if (!index.TryGetValue(probe, out int targetId))
                {
                    targetId = states.Count;
                    var real = new Lr0ItemSet(targetId, closure);
                    states.Add(real);
                    index[real] = targetId;
                    work.Enqueue(targetId);
                }
                row[symId] = targetId;
            }
            gotoRows.Add(row);
        }

        var gotoTable = new int[states.Count, symCount];
        for (int s = 0; s < states.Count; s++)
            for (int c = 0; c < symCount; c++)
                gotoTable[s, c] = gotoRows[s][c];

        return new Lr0Automaton(states, 0, gotoTable);
    }
}
