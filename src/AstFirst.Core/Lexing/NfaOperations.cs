using System.Collections.Generic;

namespace AstFirst.Core.Lexing;

/// <summary>
/// NFA の基本操作。DFA 構築 (部分集合構成法) など本番コードが使用する。
/// テスト用シミュレータ (<see cref="NfaSimulator"/>) もこれを利用する。
/// </summary>
public static class NfaOperations
{
    /// <summary>指定状態集合から ε 遷移のみで到達できる全状態 (自身を含む)。</summary>
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

    /// <summary>単一状態からの ε-closure。</summary>
    public static HashSet<int> EpsilonClosure(Nfa nfa, int seed) => EpsilonClosure(nfa, new[] { seed });
}
