using System.Collections.Generic;

namespace AstFirst.Core.Parsing;

/// <summary>
/// LALR(1) ルックアヘッドを DeRemer-Pennello (1982) / Dragon Book
/// Algorithm 4.63・4.64 で計算する。LR(0) オートマトン上で
/// spontaneous generation (自発的生成) と propagation (伝播リンク) を求め、
/// 不動点で伝播させて各項目の LALR(1) ルックアヘッドを得る。
/// </summary>
public sealed class LalrLookahead
{
    private readonly Grammar _grammar;
    private readonly Lr0Automaton _auto;
    private readonly FirstSet _first;
    private readonly Dictionary<(int state, Lr0Item item), HashSet<int>> _lookahead;

    public LalrLookahead(Grammar grammar, Lr0Automaton auto, FirstSet first)
    {
        _grammar = grammar;
        _auto = auto;
        _first = first;
        _lookahead = new Dictionary<(int, Lr0Item), HashSet<int>>();
        Compute();
    }

    /// <summary>状態 state の項目 item の LALR(1) ルックアヘッド (終端 id の集合)。</summary>
    public IReadOnlyCollection<int> Lookahead(int state, Lr0Item item)
    {
        return _lookahead.TryGetValue((state, item), out var set) ? set : Empty;
    }

    private static readonly int[] Empty = new int[0];

    private void Compute()
    {
        var spontaneous = new Dictionary<(int, Lr0Item), HashSet<int>>();
        var propagation = new Dictionary<(int, Lr0Item), List<(int, Lr0Item)>>();

        HashSet<int> SpontOf((int, Lr0Item) key)
        {
            if (!spontaneous.TryGetValue(key, out var s))
            {
                s = new HashSet<int>();
                spontaneous[key] = s;
            }
            return s;
        }

        List<(int, Lr0Item)> PropOf((int, Lr0Item) key)
        {
            if (!propagation.TryGetValue(key, out var l))
            {
                l = new List<(int, Lr0Item)>();
                propagation[key] = l;
            }
            return l;
        }

        // 拡張開始: 状態0 の [S' -> . S $] に $ を自発的生成。
        SpontOf((0, new Lr0Item(_grammar.AugmentedProduction.Id, 0))).Add(_grammar.EndOfFile.Id);

        // Step 1: 各状態・各項目で spontaneous と propagation リンクを決定。
        for (int state = 0; state < _auto.StateCount; state++)
        {
            var items = _auto.States[state].Items;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var prod = _grammar.Productions[item.ProductionId];
                if (item.Dot >= prod.Length) continue;
                var B = prod.Rhs[item.Dot]; // 点の右の記号 (終端/非終端)

                int J = _auto.Goto(state, B.Id);
                if (J < 0) continue;

                // (1) 同じ規則の点を進めた項目 [A -> αB.β] へ lookahead を伝播。
                //     goto で lookahead は維持される。
                PropOf((state, item)).Add((J, new Lr0Item(item.ProductionId, item.Dot + 1)));

                if (B.IsTerminal) continue; // 終端なら (1) の伝播のみ。

                // (2) 非終端 B: β = 点の次以降。同じ状態の closure 項目 [B -> .γ] に
                //     spontaneous (FIRST(β)) と、β が NULLABLE なら propagation を追加。
                var beta = new List<Symbol>(prod.Length - item.Dot - 1);
                for (int k = item.Dot + 1; k < prod.Length; k++) beta.Add(prod.Rhs[k]);

                var (firsts, allNullable) = _first.FirstOfSequence(beta);

                var iItems = _auto.States[state].Items;
                for (int j = 0; j < iItems.Count; j++)
                {
                    var jItem = iItems[j];
                    if (jItem.Dot != 0) continue;
                    var jProd = _grammar.Productions[jItem.ProductionId];
                    if (jProd.Lhs.Id != B.Id) continue;

                    var target = (state, jItem);
                    foreach (var b in firsts) SpontOf(target).Add(b);
                    if (allNullable) PropOf((state, item)).Add(target);
                }
            }
        }

        // Step 2: spontaneous で初期化し、propagation リンクで不動点まで伝播。
        // Worklist アルゴリズム: 変化があった箇所のみを処理（全走査を回避）。
        foreach (var kv in spontaneous)
            _lookahead[kv.Key] = new HashSet<int>(kv.Value);

        // Worklist: 変化が伝播された可能性のある項目（ソース側）
        var worklist = new Queue<(int, Lr0Item)>();
        var inWorklist = new HashSet<(int, Lr0Item)>();

        foreach (var kv in spontaneous)
        {
            worklist.Enqueue(kv.Key);
            inWorklist.Add(kv.Key);
        }

        while (worklist.Count > 0)
        {
            var current = worklist.Dequeue();
            inWorklist.Remove(current);

            if (!_lookahead.TryGetValue(current, out var sourceLa) || sourceLa.Count == 0) continue;
            if (!propagation.TryGetValue(current, out var targets)) continue;

            foreach (var target in targets)
            {
                if (!_lookahead.TryGetValue(target, out var targetLa))
                {
                    targetLa = new HashSet<int>();
                    _lookahead[target] = targetLa;
                }

                // ターゲットの lookahead が変化したら worklist に追加
                int countBefore = targetLa.Count;
                foreach (var a in sourceLa)
                    targetLa.Add(a);

                if (targetLa.Count > countBefore && inWorklist.Add(target))
                    worklist.Enqueue(target);
            }
        }
    }
}
