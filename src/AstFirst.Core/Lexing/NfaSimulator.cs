using System.Collections.Generic;

namespace AstFirst.Core.Lexing;

/// <summary>
/// NFA を直接シミュレートして入力が受理されるかを判定する。
/// テスト用、および後の部分集合構成法 (DFA 構築) の参考実装。
/// 入力をすべて消費して受理状態に到達できれば真 (完全一致)。
/// </summary>
public static class NfaSimulator
{
    public static bool Matches(Nfa nfa, string input)
    {
        var current = EpsilonClosure(nfa, nfa.Start);
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            var next = new HashSet<int>();
            foreach (int s in current)
            {
                var transitions = nfa.States[s].Transitions;
                for (int t = 0; t < transitions.Count; t++)
                {
                    var tr = transitions[t];
                    if (tr.Label is CharSet label && label.Contains(c))
                        next.Add(tr.Target);
                }
            }
            if (next.Count == 0) return false;
            current = EpsilonClosure(nfa, next);
        }
        foreach (int s in current)
            if (nfa.States[s].IsAccept) return true;
        return false;
    }

    // 指定状態集合から ε 遷移のみで到達できる全状態 (自身を含む)。
    public static HashSet<int> EpsilonClosure(Nfa nfa, IEnumerable<int> seeds)
    {
        var closure = new HashSet<int>();
        var stack = new Stack<int>();
        foreach (int s in seeds) stack.Push(s);
        while (stack.Count > 0)
        {
            int s = stack.Pop();
            if (!closure.Add(s)) continue;
            var transitions = nfa.States[s].Transitions;
            for (int t = 0; t < transitions.Count; t++)
            {
                var tr = transitions[t];
                if (tr.IsEpsilon) stack.Push(tr.Target);
            }
        }
        return closure;
    }

    private static HashSet<int> EpsilonClosure(Nfa nfa, int seed) => EpsilonClosure(nfa, new[] { seed });
}
