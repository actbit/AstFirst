using System;
using System.Collections.Generic;

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

    public Grammar(
        IReadOnlyList<Production> productions,
        IReadOnlyList<Symbol> symbols,
        Symbol startSymbol,
        Symbol augmentedStart,
        Symbol endOfFile,
        Production augmentedProduction,
        IReadOnlyDictionary<int, Precedence>? terminalPrecedence = null)
    {
        Productions = productions;
        Symbols = symbols;
        StartSymbol = startSymbol;
        AugmentedStart = augmentedStart;
        EndOfFile = endOfFile;
        AugmentedProduction = augmentedProduction;
        TerminalPrecedence = terminalPrecedence ?? new Dictionary<int, Precedence>();
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
        return new Grammar(_productions, _symbolList, startSymbol, augStart, eof, augProd, _terminalPrecedence);
    }
}
