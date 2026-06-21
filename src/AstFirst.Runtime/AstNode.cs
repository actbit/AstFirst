using System.Collections.Generic;

namespace AstFirst;

/// <summary>
/// AST ノードの基底。非終端記号の具象形 (= 1つの生成規則) はこれを継承する。
/// ノードの構築 (コンストラクタ) が還元時のアクション＋意味解析を兼ねる。
/// </summary>
public abstract class AstNode
{
    /// <summary>このノードが覆うソース範囲。コンストラクタで子から計算して設定する。</summary>
    public SourceSpan Span { get; protected set; }
}

public enum Severity { Error, Warning }

/// <summary>診断メッセージ (エラー/警告)。</summary>
public sealed class Diagnostic(string message, SourceSpan span, Severity severity)
{
    public string Message { get; } = message;
    public SourceSpan Span { get; } = span;
    public Severity Severity { get; } = severity;
    public override string ToString() => Severity + " " + Span + ": " + Message;
}

/// <summary>診断の蓄積。</summary>
public sealed class DiagnosticBag
{
    private readonly List<Diagnostic> _items = new();

    public IReadOnlyList<Diagnostic> Items => _items;
    public bool HasErrors => _items.Count > 0;

    public void Error(string message, SourceSpan span) => _items.Add(new Diagnostic(message, span, Severity.Error));
    public void Warning(string message, SourceSpan span) => _items.Add(new Diagnostic(message, span, Severity.Warning));
}

/// <summary>シンボル表 (変数・型等)。ユーザーが実装可能。</summary>
public interface ISymbolTable
{
    bool Contains(string name);
    void Declare(string name, object? value = null);
    object? this[string name] { get; set; }
}

/// <summary>
/// 意味解析コンテキスト。<see cref="ContextAttribute"/> を付けたコンストラクタ引数に
/// Generator がパーサから注入する。
/// </summary>
public abstract class SemanticContext
{
    public abstract ISymbolTable Symbols { get; }
    public abstract DiagnosticBag Diagnostics { get; }
}

/// <summary>ISymbolTable の単純な実装。BasicSemanticContext が使う。</summary>
public sealed class SymbolTable : ISymbolTable
{
    private readonly Dictionary<string, object?> _symbols = new();

    public bool Contains(string name) => _symbols.ContainsKey(name);
    public void Declare(string name, object? value = null) => _symbols[name] = value;
    public object? this[string name]
    {
        get => _symbols.TryGetValue(name, out var v) ? v : null;
        set => _symbols[name] = value;
    }
}

/// <summary>SemanticContext の標準実装。生成コードが既定で使う。</summary>
public sealed class BasicSemanticContext : SemanticContext
{
    public override ISymbolTable Symbols { get; } = new SymbolTable();
    public override DiagnosticBag Diagnostics { get; } = new DiagnosticBag();
}
