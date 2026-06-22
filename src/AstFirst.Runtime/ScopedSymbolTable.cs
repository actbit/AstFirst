using System.Collections.Generic;

namespace AstFirst;

/// <summary>
/// 宣言されたシンボル (変数・関数・型等) のエントリ。名前・宣言位置・宣言スコープ深さ・ユーザー任意データを持つ。
/// <see cref="Value"/> は型情報など言語固有のメタデータを載せる拡張点。
/// <para>
/// <strong>注意:</strong> <c>AstFirst.Core.Parsing.Symbol</c> (文法記号) とは無関係。
/// 名前衝突を避けるため <c>SymbolEntry</c> としている。
/// </para>
/// </summary>
public sealed class SymbolEntry
{
    public string Name { get; }
    public object? Value { get; set; }
    public SourceSpan Span { get; }
    public int Depth { get; }

    public SymbolEntry(string name, SourceSpan span, int depth, object? value = null)
    {
        Name = name;
        Span = span;
        Depth = depth;
        Value = value;
    }
}

/// <summary>
/// レキシカルスコープ。親スコープへのリンクを持ち、ネストした宣言の可視性を表現する。
/// ルートスコープの深さは 0、子スコープは親の深さ + 1。
/// </summary>
public sealed class Scope
{
    private readonly Dictionary<string, SymbolEntry> _symbols = new();

    public Scope? Parent { get; }
    public int Depth { get; }

    /// <summary>このスコープで直接宣言されたシンボル (外側スコープは含まない)。</summary>
    public IEnumerable<SymbolEntry> Symbols => _symbols.Values;

    internal Scope(Scope? parent, int depth)
    {
        Parent = parent;
        Depth = depth;
    }

    internal bool TryGetLocal(string name, out SymbolEntry? symbol) => _symbols.TryGetValue(name, out symbol);

    internal void Add(SymbolEntry symbol) => _symbols[symbol.Name] = symbol;
}

/// <summary>
/// スコープ付きシンボル表。<see cref="PushScope"/>/<see cref="PopScope"/> でスコープスタックを操作し、
/// <see cref="Lookup"/> は内側スコープ優先で解決する。意味解析で使用。
/// <para>
/// LALR のボトムアップ reduce では親スコープを子ノードに伝えられない (親のコンストラクタは
/// 子の後に呼ばれる) ため、正確なブロックスコープには Parse 後の AST ウォーク (2パス) を推奨する。
/// </para>
/// </summary>
public sealed class ScopedSymbolTable
{
    /// <summary>現在の (最も内側の) スコープ。</summary>
    public Scope Current { get; private set; }

    public ScopedSymbolTable()
    {
        Current = new Scope(null, 0);
    }

    /// <summary>新しい子スコープを開き、それを <see cref="Current"/> にする。</summary>
    public Scope PushScope()
    {
        Current = new Scope(Current, Current.Depth + 1);
        return Current;
    }

    /// <summary>現在のスコープを閉じて親に戻る。ルートスコープでは何もしない。</summary>
    public void PopScope()
    {
        if (Current.Parent is not null) Current = Current.Parent;
    }

    /// <summary>名前を解決。現在のスコープから外側へ探し、最初に見つかったものを返す。未宣言なら null。</summary>
    public SymbolEntry? Lookup(string name)
    {
        for (Scope? s = Current; s is not null; s = s.Parent)
            if (s.TryGetLocal(name, out var symbol)) return symbol;
        return null;
    }

    /// <summary>
    /// 現在のスコープにシンボルを宣言する。同一スコープ内の重複は拒否し <paramref name="existing"/>
    /// に既存宣言を返す。外側スコープの同名宣言 (シャドウイング) は許可される。
    /// </summary>
    public bool TryDeclare(string name, SourceSpan span, object? value, out SymbolEntry? existing)
    {
        if (Current.TryGetLocal(name, out existing)) return false;
        var symbol = new SymbolEntry(name, span, Current.Depth, value);
        Current.Add(symbol);
        return true;
    }

    /// <summary>
    /// 名前を解決し、未宣言なら <paramref name="bag"/> に Error 診断を追加して null を返す。
    /// シンボル解決の標準ヘルパー (意味解析で参照の都度呼ぶ)。
    /// </summary>
    public SymbolEntry? ResolveOrError(string name, SourceSpan span, DiagnosticBag bag)
    {
        var symbol = Lookup(name);
        if (symbol is null) bag.Error("'" + name + "' は宣言されていません", span);
        return symbol;
    }
}
