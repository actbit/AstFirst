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

        // 演算子優先度/結合性: [Precedence] を AST クラスに付けた場合、規則の右辺の全終端に設定。
        // 全終端に伝播することで、後置演算 (expr.id / expr(args) / expr++) や三項 (cond?t:f) のように
        // 演算子が「最後の終端」でない規則の shift-reduce も優先度/結合性で解決できる。
        foreach (var n in model.Nodes)
        {
            if (n.IsAbstract || n.PrecedencePriority == 0) continue;
            foreach (var rule in n.Rules)
            {
                foreach (var p in rule.Parameters)
                {
                    if (p.IsContext) continue;
                    if (p.Pattern is not null && terminals.TryGetValue(p.Pattern, out var t))
                        b.SetPrecedence(t, n.PrecedencePriority, n.PrecedenceAssoc);
                }
            }
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

        var listNonTerminals = new Dictionary<(string, int), Symbol>();  // (要素型, RepeatMin) -> リスト非終端

        // [Repeat] 引数をリスト非終端に展開。
        // Plus (Min=1): List_T → item | List_T item。
        // Star (Min=0): + ε (空リスト)。
        // 要素記号は ParamToSymbol で解決 (AstNode 派生の非終端)。
        Symbol GetOrCreateList(ParamModel p)
        {
            var key = (p.TypeFullName, p.RepeatMin);
            if (listNonTerminals.TryGetValue(key, out var existing)) return existing;
            var elemSym = ParamToSymbol(p);
            var suffix = p.RepeatMin == 0 ? ":star" : ":plus";
            var list = b.NonTerminal("list:" + p.TypeFullName + suffix);
            b.Production(list, new[] { elemSym }, new ListReduceActionModel(p.TypeFullName, false));
            b.Production(list, new[] { list, elemSym }, new ListReduceActionModel(p.TypeFullName, true));
            if (p.RepeatMin == 0)
                b.Production(list, Array.Empty<Symbol>(), new ListReduceActionModel(p.TypeFullName, false, isEmpty: true));
            listNonTerminals[key] = list;
            return list;
        }

        if (!nonTerminals.TryGetValue(model.RootTypeFullName, out var root))
            throw new InvalidOperationException("開始記号 " + model.RootTypeFullName + " が見つかりません。");

        foreach (var n in model.Nodes)
        {
            if (n.IsAbstract) continue;
            // 左辺は直接の親 (非終端)。NumExpr : Expr → 規則 Expr -> ... (AST は NumExpr)。
            // 継承ツリーで具象クラスが親非終端の生成規則を表す。
            // 親が非終端でない (AstNode 直系の具象クラス等) 場合は、
            // そのクラス自身を非終端の左辺にする (他の規則から引数型で参照されるケース)。
            // 例: JsonMember : AstNode (cons 引数 JsonMember head) → 規則 JsonMember -> STRING : Json。
            if (!nonTerminals.TryGetValue(n.BaseFullName, out var lhs))
            {
                if (!nonTerminals.TryGetValue(n.FullName, out lhs))
                    continue; // 自身も非終端でなければ文法記号にならない (念のため)
            }
            foreach (var rule in n.Rules)
            {
                var rhs = new List<Symbol>();
                var reduceParams = new List<ReduceParamModel>();
                int childIndex = 0;
                foreach (var p in rule.Parameters)
                {
                    if (p.IsContext)
                    {
                        reduceParams.Add(new ReduceParamModel(true, p.TypeFullName, -1));
                        continue;
                    }
                    if (p.IsRepeat)
                    {
                        // [Repeat] は List_T (List_T → item | List_T item) に展開。
                        // reduce 時に List_T の値 (List<T>) を IReadOnlyList<T> として渡す。
                        var list = GetOrCreateList(p);
                        reduceParams.Add(new ReduceParamModel(false,
                            "System.Collections.Generic.IReadOnlyList<" + p.TypeFullName + ">", childIndex));
                        rhs.Add(list);
                        childIndex++;
                        continue;
                    }
                    reduceParams.Add(new ReduceParamModel(false, p.TypeFullName, childIndex));
                    rhs.Add(ParamToSymbol(p));
                    childIndex++;
                }
                var action = new ReduceActionModel(n.FullName, rule.MethodName, reduceParams);
                // [Precedence] を Production に直接設定（%prec 相当）。
                // トークン経由でなく規則単位で precedence を持つことで、同じ終端を含む複数規則
                // （generic の > と比較の > など）で別々の優先度を設定できる。
                Precedence? rulePrec = n.PrecedencePriority == 0 ? null
                    : new Precedence(n.PrecedencePriority, n.PrecedenceAssoc);
                b.Production(lhs, rhs.ToArray(), action, rulePrec);
            }
        }

        // 抽象クラス経由の単位規則: 抽象 N : Base (Base も非終端) → Base → N (値をそのまま渡す)。
        // 中間抽象 (Root → Mid → Leaf) で Mid を文法非終端として機能させ、Root から到達可能にする。
        // N は抽象なので自身の [Rule] は上でスキップ済み。Mid の具象サブクラス (Leaf) の reduce 結果が
        // そのまま Mid → Root へ伝播する (新規 AST は作らない)。
        foreach (var n in model.Nodes)
        {
            if (!n.IsAbstract) continue;
            if (!nonTerminals.TryGetValue(n.FullName, out var nt)) continue;
            if (!nonTerminals.TryGetValue(n.BaseFullName, out var baseNt)) continue;
            b.Production(baseNt, new[] { nt }, new PassThroughActionModel());
        }

        return b.Build(root);
    }
}
