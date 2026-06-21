using System.Collections.Generic;

namespace AstFirst.Core.Lexing;

/// <summary>
/// NFA を直接シミュレートして入力が受理されるか (完全一致) を判定する。
/// <b>テスト専用</b>。本番の ε-closure 操作は <see cref="NfaOperations"/> にある。
/// </summary>
public static class NfaSimulator
{
    public static bool Matches(Nfa nfa, string input)
    {
        var current = NfaOperations.EpsilonClosure(nfa, nfa.Start);
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
            current = NfaOperations.EpsilonClosure(nfa, next);
        }
        foreach (int s in current)
            if (nfa.States[s].IsAccept) return true;
        return false;
    }
}
