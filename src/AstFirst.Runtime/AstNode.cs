using System.Collections.Generic;

namespace AstFirst;

/// <summary>
/// Base class for AST nodes. A concrete nonterminal (one production) derives from this.
/// The constructor is the reduce action and may perform semantic analysis.
/// </summary>
/// <remarks>
/// AST ノードの基底。非終端記号の具象形 (= 1つの生成規則) はこれを継承します。
/// ノードの構築 (コンストラクタ) が還元時のアクション＋意味解析を兼ねます。
/// </remarks>
public abstract class AstNode
{
    /// <summary>このノードが覆うソース範囲。コンストラクタで子から計算して設定する。</summary>
    public SourceSpan Span { get; protected set; }

    private Dictionary<string, object?>? _annotations;

    /// <summary>ノードに任意の注釈 (解決したシンボル、型など) を紐付ける。束縛解析で使用。</summary>
    public void SetAnnotation(string key, object? value)
        => (_annotations ??= new Dictionary<string, object?>())[key] = value;

    /// <summary>注釈を取得 (未設定や型不一致なら null)。</summary>
    public T? GetAnnotation<T>(string key) where T : class
    {
        if (_annotations is null || !_annotations.TryGetValue(key, out var v)) return null;
        return v as T;
    }
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
/// Semantic analysis context. Declare a constructor parameter of a <see cref="SemanticContext"/>-derived type
/// and the generator injects an instance from the parser (providing a <see cref="ScopedSymbolTable"/> and <see cref="DiagnosticBag"/>).
/// </summary>
/// <remarks>
/// 意味解析コンテキスト。<see cref="SemanticContext"/> 派生型のコンストラクタ引数として宣言すると、
/// Generator がパーサからインスタンスを注入します (<see cref="ScopedSymbolTable"/> と <see cref="DiagnosticBag"/> を提供)。
/// </remarks>
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
