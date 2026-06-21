using System.Collections.Generic;

namespace AstFirst.Core.Lexing;

/// <summary>
/// 複数の <see cref="LexerRule"/> を1つの NFA/DFA に統合する。
/// 各ルールの受理状態に TokenId を付与し、部分集合構成法で DFA を組む。
/// 複数受理の DFA 状態は優先度最小の TokenId で解決する。
/// </summary>
public static class LexerBuilder
{
    public static Dfa BuildDfa(IReadOnlyList<LexerRule> rules)
    {
        var nfa = BuildCombinedNfa(rules);
        var tokenIdPriority = new Dictionary<int, int>();
        for (int i = 0; i < rules.Count; i++)
            tokenIdPriority[rules[i].TokenId] = rules[i].Priority;

        // 集合に含まれる受理状態の TokenId のうち、優先度最小 (同優先度は TokenId 最小) を返す。
        int Resolve(HashSet<int> set)
        {
            int best = -1;
            int bestPriority = -1;
            foreach (int s in set)
            {
                if (!nfa.States[s].IsAccept) continue;
                int tid = nfa.States[s].AcceptTokenId;
                int pri = tokenIdPriority.TryGetValue(tid, out var p) ? p : int.MaxValue;
                if (pri > bestPriority || (pri == bestPriority && (best < 0 || tid < best)))
                {
                    bestPriority = pri;
                    best = tid;
                }
            }
            return best;
        }

        var dfa = DfaBuilder.Build(nfa, Resolve);
        return DfaMinimizer.Minimize(dfa);
    }

    private static Nfa BuildCombinedNfa(IReadOnlyList<LexerRule> rules)
    {
        var states = new List<NfaState>();
        int startId = NewState(states);
        for (int r = 0; r < rules.Count; r++)
        {
            var rule = rules[r];
            var ast = RegexParser.Parse(rule.Pattern);
            var ruleNfa = NfaBuilder.Build(ast);
            int offset = states.Count;
            for (int i = 0; i < ruleNfa.States.Count; i++)
            {
                var s = ruleNfa.States[i];
                var ns = new NfaState(states.Count);
                ns.IsAccept = s.IsAccept;
                ns.AcceptTokenId = s.IsAccept ? rule.TokenId : 0;
                for (int j = 0; j < s.Transitions.Count; j++)
                {
                    var t = s.Transitions[j];
                    ns.Transitions.Add(new NfaTransition(t.Label, t.Target + offset));
                }
                states.Add(ns);
            }
            states[startId].Transitions.Add(new NfaTransition(null, ruleNfa.Start + offset));
        }
        return new Nfa(states, startId, -1);
    }

    private static int NewState(List<NfaState> states)
    {
        int id = states.Count;
        states.Add(new NfaState(id));
        return id;
    }
}
