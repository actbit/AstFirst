using System;
using System.Collections.Generic;
using System.Linq;

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

/// <summary>スコープの種類。PushScope で指定し、PopScope で対応付けを検証する。</summary>
public enum ScopeKind { Root, Block, Function, Loop, Other }

/// <summary>
/// レキシカルスコープ。親スコープへのリンク・キー・種類・深さを持つ。
/// ルートスコープの深さは 0、子スコープは親の深さ + 1。
/// </summary>
public sealed class Scope
{
    private readonly Dictionary<string, SymbolEntry> _symbols = new();

    public Scope? Parent { get; }
    public int Depth { get; }
    /// <summary>スコープを識別するキー (辞書風)。同名種類の複数スコープも区別する。</summary>
    public string? Key { get; }
    /// <summary>スコープの種類 (Block/Function/Loop 等)。</summary>
    public ScopeKind Kind { get; }

    /// <summary>このスコープで直接宣言されたシンボル (外側スコープは含まない)。</summary>
    public IEnumerable<SymbolEntry> Symbols => _symbols.Values;

    internal Scope(Scope? parent, int depth, string? key, ScopeKind kind)
    {
        Parent = parent;
        Depth = depth;
        Key = key;
        Kind = kind;
    }

    internal bool TryGetLocal(string name, out SymbolEntry? symbol) => _symbols.TryGetValue(name, out symbol);

    internal void Add(SymbolEntry symbol) => _symbols[symbol.Name] = symbol;

    public override string ToString() => Kind + ":" + (Key ?? "?");
}

/// <summary>
/// スコープ付きシンボル表。<see cref="PushScope(string, ScopeKind)"/>/<see cref="PopScope(string)"/> で
/// スコープスタックを操作し、<see cref="Lookup"/> は内側スコープ優先で解決する。
/// 辞書風: キーで個別スコープを識別・対応付け (同名種類の複数スコープも区別)。
/// <see cref="Stack"/>/<see cref="StackTrace"/> で全体の階層とキーの階層をスタックぽく可視化できる。
/// </summary>
/// <remarks>
/// LALR のボトムアップ reduce では親スコープを子ノードに伝えられないため、正確なブロックスコープには
/// Parse 後の AST ウォーク (2パス) を推奨します。
/// </remarks>
public sealed class ScopedSymbolTable
{
    /// <summary>現在の (最も内側の) スコープ。</summary>
    public Scope Current { get; private set; }

    public ScopedSymbolTable()
    {
        Current = new Scope(null, 0, null, ScopeKind.Root);
    }

    /// <summary>キー+種類付きの子スコープを開き、それを <see cref="Current"/> にする。同名種類の複数スコープもキーで区別。</summary>
    public Scope PushScope(string key, ScopeKind kind)
    {
        Current = new Scope(Current, Current.Depth + 1, key, kind);
        return Current;
    }

    /// <summary>子スコープを開く (後方互換: キー/種類なし)。</summary>
    public Scope PushScope() => PushScope(null, ScopeKind.Other);

    /// <summary>キーを検証して現在のスコープを閉じ、親に戻る (ミスマッチは例外)。辞書風の対応付け。</summary>
    public void PopScope(string key)
    {
        if (Current.Parent is null) return; // ルートスコープは閉じない
        if (Current.Key != key)
            throw new InvalidOperationException($"スコープ Pop のキー不一致: 期待 '{key}', 実際 '{Current.Key ?? "(null)"}'");
        Current = Current.Parent;
    }

    /// <summary>現在のスコープを閉じて親に戻る (後方互換: キー検証なし)。</summary>
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

    /// <summary>ルート→現在のスコープ列 (全体の階層)。インデックス 0 がルート、末尾が現在。</summary>
    public IReadOnlyList<Scope> Stack
    {
        get
        {
            var list = new List<Scope>();
            for (Scope? s = Current; s is not null; s = s.Parent) list.Add(s);
            list.Reverse();
            return list;
        }
    }

    /// <summary>スタックを "Root:? > Function:main > Block:if" のように表現 (全体の階層とキーの階層)。</summary>
    public string StackTrace => string.Join(" > ", Stack);
}
