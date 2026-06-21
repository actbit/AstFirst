using System;
using System.Collections.Generic;

namespace AstFirst.Core.Lexing;

/// <summary>
/// 部分集合構成法 (subset construction) で NFA を DFA に変換する。
/// 文字は <see cref="AlphabetPartition"/> のクラス単位で扱う。
/// <paramref name="resolveTokenId"/> で、複数受理状態を含む DFA 状態の
/// トークンID を優先度で解決できる (レクサ統合用)。
/// </summary>
public static class DfaBuilder
{
    public static Dfa Build(Nfa nfa, Func<HashSet<int>, int>? resolveTokenId = null)
    {
        var alphabet = AlphabetPartition.Build(nfa);
        return Build(nfa, alphabet, resolveTokenId);
    }

    public static Dfa Build(Nfa nfa, AlphabetPartition alphabet, Func<HashSet<int>, int>? resolveTokenId = null)
    {
        var setCmp = SetComparer.Instance;
        var stateMap = new Dictionary<HashSet<int>, int>(setCmp);
        var states = new List<DfaState>();
        var work = new Queue<HashSet<int>>();
        int classCount = alphabet.ClassCount;

        int GetOrAdd(HashSet<int> set)
        {
            if (stateMap.TryGetValue(set, out int id)) return id;
            id = states.Count;
            var ds = new DfaState(id, classCount);
            bool accept = false;
            foreach (int s in set)
                if (nfa.States[s].IsAccept) { accept = true; break; }
            ds.IsAccept = accept;
            if (accept && resolveTokenId != null)
                ds.AcceptTokenId = resolveTokenId(set);
            states.Add(ds);
            stateMap[set] = id;
            work.Enqueue(set);
            return id;
        }

        var startSet = NfaOperations.EpsilonClosure(nfa, nfa.Start);
        GetOrAdd(startSet);

        while (work.Count > 0)
        {
            var current = work.Dequeue();
            int currentId = stateMap[current];
            var ds = states[currentId];
            for (int cls = 0; cls < classCount; cls++)
            {
                char rep = alphabet.RepresentativeChar(cls);
                var next = new HashSet<int>();
                foreach (int s in current)
                {
                    var trs = nfa.States[s].Transitions;
                    for (int j = 0; j < trs.Count; j++)
                    {
                        var tr = trs[j];
                        if (tr.Label is CharSet label && label.Contains(rep))
                            next.Add(tr.Target);
                    }
                }
                if (next.Count == 0) continue;
                var closure = NfaOperations.EpsilonClosure(nfa, next);
                int nextId = GetOrAdd(closure);
                ds.Transitions[cls] = nextId;
            }
        }

        return new Dfa(states, 0, alphabet);
    }

    /// <summary>HashSet&lt;int&gt; を集合として比較する (要素の順序に依存しない)。</summary>
    private sealed class SetComparer : IEqualityComparer<HashSet<int>>
    {
        public static readonly SetComparer Instance = new SetComparer();

        public bool Equals(HashSet<int>? x, HashSet<int>? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null || y is null) return false;
            if (x.Count != y.Count) return false;
            foreach (int e in x)
                if (!y.Contains(e)) return false;
            return true;
        }

        public int GetHashCode(HashSet<int> obj)
        {
            int h = 0;
            foreach (int e in obj) h ^= e;
            return h;
        }
    }
}
