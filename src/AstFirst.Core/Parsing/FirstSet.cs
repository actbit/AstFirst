using System.Collections.Generic;

namespace AstFirst.Core.Parsing;

/// <summary>
/// 文法の FIRST 集合と NULLABLE を計算したもの。
/// 終端 X の FIRST は {X}。非終端 A は A -> X1..Xn から
/// X1 の FIRST、X1 が NULLABLE なら X2 の FIRST、… を集める。
/// 全体が NULLABLE なら A も NULLABLE。不動点で収束させる。
/// </summary>
public sealed class FirstSet
{
    private readonly Grammar _grammar;
    private readonly HashSet<int>[] _first;
    private readonly bool[] _nullable;

    public FirstSet(Grammar grammar)
    {
        _grammar = grammar;
        int n = grammar.Symbols.Count;
        _first = new HashSet<int>[n];
        _nullable = new bool[n];
        for (int i = 0; i < n; i++) _first[i] = new HashSet<int>();
        Compute();
    }

    private void Compute()
    {
        int n = _grammar.Symbols.Count;
        // 終端 X の FIRST は {X} 自身。
        for (int i = 0; i < n; i++)
        {
            var s = _grammar.Symbols[i];
            if (s.IsTerminal) _first[i].Add(s.Id);
        }

        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int p = 0; p < _grammar.Productions.Count; p++)
            {
                var prod = _grammar.Productions[p];
                var lhs = prod.Lhs;
                bool allNullable = true;
                for (int r = 0; r < prod.Rhs.Length; r++)
                {
                    var sym = prod.Rhs[r];
                    foreach (var t in _first[sym.Id])
                        if (_first[lhs.Id].Add(t)) changed = true;
                    if (!_nullable[sym.Id]) { allNullable = false; break; }
                }
                // 空右辺 (ε) の場合はループに入らず allNullable=true。
                if (allNullable && !_nullable[lhs.Id])
                {
                    _nullable[lhs.Id] = true;
                    changed = true;
                }
            }
        }
    }

    /// <summary>記号 sym の FIRST (終端 id の集合)。</summary>
    public IEnumerable<int> FirstOf(Symbol sym) => _first[sym.Id];

    public bool IsNullable(Symbol sym) => _nullable[sym.Id];

    /// <summary>
    /// 記号列 seq の FIRST と、seq 全体が NULLABLE かを返す。
    /// LALR の FIRST(β a) 計算で使う: allNullable が真なら末尾の a を追加する。
    /// </summary>
    public (HashSet<int> firsts, bool allNullable) FirstOfSequence(IReadOnlyList<Symbol> seq)
    {
        var result = new HashSet<int>();
        bool allNullable = true;
        for (int i = 0; i < seq.Count; i++)
        {
            var sym = seq[i];
            foreach (var t in _first[sym.Id])
                result.Add(t);
            if (!_nullable[sym.Id]) { allNullable = false; break; }
        }
        return (result, allNullable);
    }
}
