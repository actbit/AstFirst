using System;
using System.Collections.Generic;
using System.Linq;

namespace AstFirst.Core.Parsing;

/// <summary>文法。生成規則の集合と開始記号。拡張開始規則 S' -> S $ を保持。</summary>
public sealed class Grammar
{
    public IReadOnlyList<Production> Productions { get; }
    public IReadOnlyList<Symbol> Symbols { get; }
    public Symbol StartSymbol { get; }       // ユーザー指定の開始非終端
    public Symbol AugmentedStart { get; }    // S'
    public Symbol EndOfFile { get; }         // $
    public Production AugmentedProduction { get; } // S' -> S $
    /// <summary>終端の優先度/結合性 (終端 Symbol.Id -> Precedence)。</summary>
    public IReadOnlyDictionary<int, Precedence> TerminalPrecedence { get; }

    /// <summary>到達不能非終端 (規則はあるが開始記号から到達不能)。空なら正常。</summary>
    public IReadOnlyList<Symbol> UnreachableNonTerminals { get; }
    /// <summary>未定義非終端 (右辺で参照されるが LHS に規則がない)。空なら正常。</summary>
    public IReadOnlyList<Symbol> UndefinedNonTerminals { get; }

    public Grammar(
        IReadOnlyList<Production> productions,
        IReadOnlyList<Symbol> symbols,
        Symbol startSymbol,
        Symbol augmentedStart,
        Symbol endOfFile,
        Production augmentedProduction,
        IReadOnlyDictionary<int, Precedence>? terminalPrecedence = null,
        IReadOnlyList<Symbol>? unreachableNonTerminals = null,
        IReadOnlyList<Symbol>? undefinedNonTerminals = null)
    {
        Productions = productions;
        Symbols = symbols;
        StartSymbol = startSymbol;
        AugmentedStart = augmentedStart;
        EndOfFile = endOfFile;
        AugmentedProduction = augmentedProduction;
        TerminalPrecedence = terminalPrecedence ?? new Dictionary<int, Precedence>();
        UnreachableNonTerminals = unreachableNonTerminals ?? Array.Empty<Symbol>();
        UndefinedNonTerminals = undefinedNonTerminals ?? Array.Empty<Symbol>();
    }
}

/// <summary>
/// 文法を構築するビルダー。終端/非終端を登録し、生成規則を追加したのち
/// <see cref="Build"/> で拡張開始規則 S' -> S $ を自動付与した <see cref="Grammar"/> を得る。
/// </summary>
public sealed class GrammarBuilder
{
    private readonly Dictionary<string, Symbol> _symbols = new Dictionary<string, Symbol>();
    private readonly List<Symbol> _symbolList = new List<Symbol>();
    private readonly List<Production> _productions = new List<Production>();
    private readonly Dictionary<int, Precedence> _terminalPrecedence = new Dictionary<int, Precedence>();
    private int _nextSymbolId;

    /// <summary>終端の優先度/結合性を設定 (shift-reduce 衝突の解決に使用)。</summary>
    public void SetPrecedence(Symbol terminal, int priority, Associativity assoc)
        => _terminalPrecedence[terminal.Id] = new Precedence(priority, assoc);

    public Symbol Terminal(string name) => GetOrAdd(name, isTerminal: true);

    public Symbol NonTerminal(string name) => GetOrAdd(name, isTerminal: false);

    private Symbol GetOrAdd(string name, bool isTerminal)
    {
        if (_symbols.TryGetValue(name, out var s)) return s;
        s = new Symbol(_nextSymbolId++, name, isTerminal);
        _symbols[name] = s;
        _symbolList.Add(s);
        return s;
    }

    public Production Production(Symbol lhs, params Symbol[] rhs)
    {
        if (lhs.IsTerminal)
            throw new ArgumentException("生成規則の左辺は非終端である必要があります: " + lhs.Name, nameof(lhs));
        var p = new Core.Parsing.Production(_productions.Count, lhs, rhs);
        _productions.Add(p);
        return p;
    }

    /// <summary>Tag (AST クラス名等) 付きの生成規則。モデル→文法変換で使用。</summary>
    public Production Production(Symbol lhs, Symbol[] rhs, object? tag)
    {
        if (lhs.IsTerminal)
            throw new ArgumentException("生成規則の左辺は非終端である必要があります: " + lhs.Name, nameof(lhs));
        var p = new Core.Parsing.Production(_productions.Count, lhs, rhs, tag);
        _productions.Add(p);
        return p;
    }

    /// <summary>拡張開始規則 S' -> S $ を付与して文法を完成させる。</summary>
    public Grammar Build(Symbol startSymbol)
    {
        var augStart = GetOrAdd(startSymbol.Name + "'", isTerminal: false);
        var eof = GetOrAdd("$", isTerminal: true);
        var augProd = new Core.Parsing.Production(_productions.Count, augStart, new[] { startSymbol, eof });
        _productions.Add(augProd);

        // 到達不能/未定義非終端を検出。
        var reachable = ComputeReachable(startSymbol);
        var lhsNonTerminals = new HashSet<Symbol>();
        foreach (var p in _productions) lhsNonTerminals.Add(p.Lhs);
        // 到達不能 = LHS に現れるが開始記号から到達できない。拡張開始 S' は対象外。
        var unreachable = lhsNonTerminals.Where(nt => !reachable.Contains(nt) && !nt.Equals(augStart)).ToList();
        var rightSideNonTerminals = new HashSet<Symbol>();
        foreach (var p in _productions)
            foreach (var s in p.Rhs)
                if (!s.IsTerminal) rightSideNonTerminals.Add(s);
        var undefined = rightSideNonTerminals.Where(nt => !lhsNonTerminals.Contains(nt)).ToList();

        return new Grammar(_productions, _symbolList, startSymbol, augStart, eof, augProd, _terminalPrecedence, unreachable, undefined);
    }

    /// <summary>開始記号から到達可能な非終端を BFS で集める。</summary>
    private HashSet<Symbol> ComputeReachable(Symbol start)
    {
        var reachable = new HashSet<Symbol>();
        var queue = new Queue<Symbol>();
        queue.Enqueue(start);
        reachable.Add(start);
        while (queue.Count > 0)
        {
            var n = queue.Dequeue();
            foreach (var p in _productions)
            {
                if (!p.Lhs.Equals(n)) continue;
                foreach (var s in p.Rhs)
                {
                    if (!s.IsTerminal && reachable.Add(s))
                        queue.Enqueue(s);
                }
            }
        }
        return reachable;
    }
}
