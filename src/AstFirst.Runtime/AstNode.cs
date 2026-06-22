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
    public bool HasErrors => _items.Any(d => d.Severity == Severity.Error);

    public void Error(string message, SourceSpan span) => _items.Add(new Diagnostic(message, span, Severity.Error));
    public void Warning(string message, SourceSpan span) => _items.Add(new Diagnostic(message, span, Severity.Warning));
}

/// <summary>
/// 意味解析コンテキスト。<see cref="SemanticContext"/> 派生型のコンストラクタ引数として宣言すると、
/// Generator がパーサからインスタンスを注入する (<see cref="ScopedSymbolTable"/> と
/// <see cref="DiagnosticBag"/> を提供)。
/// </summary>
public abstract class SemanticContext
{
    public abstract ScopedSymbolTable Symbols { get; }
    public abstract DiagnosticBag Diagnostics { get; }
}

/// <summary>SemanticContext の標準実装。生成コードが既定で使う。</summary>
public sealed class BasicSemanticContext : SemanticContext
{
    public override ScopedSymbolTable Symbols { get; } = new ScopedSymbolTable();
    public override DiagnosticBag Diagnostics { get; } = new DiagnosticBag();
}
