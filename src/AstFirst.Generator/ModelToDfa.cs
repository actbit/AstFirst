using System.Collections.Generic;
using AstFirst.Core.Lexing;

namespace AstFirst.Generator;

/// <summary>GrammarModel からレクサ DFA と LexerRule 一覧を構築する。</summary>
public static class ModelToDfa
{
    /// <summary>DFA と、Pattern -> TokenId の対応 (LexerRule 一覧) を返す。</summary>
    public static Dfa Build(GrammarModel model, out IReadOnlyList<LexerRule> rules)
    {
        var list = new List<LexerRule>();
        var seen = new HashSet<string>();
        int id = 1;
        foreach (var td in model.TokenDefs)
        {
            if (!seen.Add(td.Pattern)) continue;
            list.Add(new LexerRule(td.Pattern, id, td.Priority, td.IsHidden));
            id++;
        }
        rules = list;
        return LexerBuilder.BuildDfa(list);
    }

    /// <summary>Pattern 文字列から TokenId への写像。</summary>
    public static Dictionary<string, int> PatternToTokenId(IReadOnlyList<LexerRule> rules)
    {
        var map = new Dictionary<string, int>();
        foreach (var r in rules) map[r.Pattern] = r.TokenId;
        return map;
    }
}
