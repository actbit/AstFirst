using System;
using System.Collections.Generic;
using AstFirst.Core.Parsing;

namespace AstFirst.Generator;

/// <summary>
/// <see cref="GrammarModel"/> を Core の <see cref="Grammar"/> に変換する。
/// Generator がコンパイル時に呼ぶ。
/// </summary>
public static class ModelToGrammar
{
    public static Grammar Build(GrammarModel model)
    {
        var b = new GrammarBuilder();
        var terminals = new Dictionary<string, Symbol>();     // key: pattern
        var nonTerminals = new Dictionary<string, Symbol>();  // key: type full name
        var patternByTokenType = new Dictionary<string, string>(); // Token 派生型名 -> pattern

        // 終端: 各 [Pattern] ごとに。Token 派生型のマップも作る。
        foreach (var td in model.TokenDefs)
        {
            if (!terminals.ContainsKey(td.Pattern))
                terminals[td.Pattern] = b.Terminal("token:" + td.Pattern);
            // Key が共通 Token 型 ("AstFirst.Token") でなければ Token 派生クラス。
            if (td.Key != "AstFirst.Token" && !patternByTokenType.ContainsKey(td.Key))
                patternByTokenType[td.Key] = td.Pattern;
        }

        // 非終端: AstNode 派生クラスごとに。
        foreach (var n in model.Nodes)
        {
            if (!nonTerminals.ContainsKey(n.FullName))
                nonTerminals[n.FullName] = b.NonTerminal(n.FullName);
        }

        Symbol ParamToSymbol(ParamModel p)
        {
            if (p.Pattern is not null && terminals.TryGetValue(p.Pattern, out var t)) return t;
            if (nonTerminals.TryGetValue(p.TypeFullName, out var nt)) return nt;
            if (patternByTokenType.TryGetValue(p.TypeFullName, out var pat))
                return terminals[pat];
            throw new InvalidOperationException(
                "引数 '" + (p.Name ?? "?") + "' (型 " + p.TypeFullName
                + ") を文法記号に解決できません。[Pattern] またはトークン/AST の派生が必要です。");
        }

        if (!nonTerminals.TryGetValue(model.RootTypeFullName, out var root))
            throw new InvalidOperationException("開始記号 " + model.RootTypeFullName + " が見つかりません。");

        foreach (var n in model.Nodes)
        {
            if (n.IsAbstract) continue;
            // 左辺は直接の親 (非終端)。NumExpr : Expr → 規則 Expr -> ... (AST は NumExpr)。
            // 継承ツリーで具象クラスが親非終端の生成規則を表す。
            if (!nonTerminals.TryGetValue(n.BaseFullName, out var lhs))
                continue; // 親が非終端でない (AstNode 等) は対象外
            foreach (var ctor in n.Constructors)
            {
                var rhs = new List<Symbol>();
                foreach (var p in ctor.Parameters)
                {
                    if (p.IsContext) continue;
                    rhs.Add(ParamToSymbol(p));
                }
                b.Production(lhs, rhs.ToArray(), n.FullName);
            }
        }

        return b.Build(root);
    }
}
