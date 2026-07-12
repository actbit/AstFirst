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
public abstract partial class AstNode
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

    /// <summary>受領状態。OnReduce で Accept()/Reject() を呼んで設定。既定 Undecided (= Accept 扱い)。</summary>
    public AcceptState AcceptState { get; private set; } = AcceptState.Undecided;

    /// <summary>この構文を受領する (既定)。OnReduce 内で呼ぶ。何も呼ばなければ受領扱い。</summary>
    protected void Accept() => AcceptState = AcceptState.Accepted;

    /// <summary>この構文を受領しない。優先度順の別候補 (別規則/shift) へフォールバックする。</summary>
    protected void Reject() => AcceptState = AcceptState.Rejected;

    /// <summary>ルートが1つに確定した時に呼ばれる (GLR の fork 収束、または LALR の reduce 時)。
    /// 派生クラスの partial 生成コードが override して OnAccepted(ctx) を呼ぶ。</summary>
    public virtual void NotifyAccepted(SemanticContext? ctx) { }

    /// <summary>受領されたか (Accepted、または既定の Undecided)。パーサ生成コードが参照。</summary>
    public bool IsAccepted => AcceptState != AcceptState.Rejected;
}

/// <summary>2パス目: 子の前 (トップダウン) に呼ばれる。実装したいノードだけこのインターフェースを実装する。
/// Generator は1つでも実装があれば Walker を生成し、未実装なら走査そのものを省く (空呼び回避)。</summary>
public interface IOnSecondPassEnter
{
    void OnSecondPassEnter(SemanticContext ctx);
}

/// <summary>2パス目: 子の後に呼ばれる。実装したいノードだけこのインターフェースを実装する。</summary>
public interface IOnSecondPassExit
{
    void OnSecondPassExit(SemanticContext ctx);
}

public enum Severity { Error, Warning }

/// <summary>reduce 時の受領状態。OnReduce で Accept()/Reject() を呼んで設定。</summary>
public enum AcceptState { Undecided, Accepted, Rejected }

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
/// 意味解析コンテキスト (読み取り専用)。OnReduce に渡される。
/// シンボル表の読み取り (Lookup) のみ可能。宣言 (Declare) や診断追加 (Error) は不可。
/// 書き込みは <see cref="BasicSemanticContext"/> ([Enter]/[Exit] Walker 用) を使う。
/// </summary>
public abstract class SemanticContext
{
    /// <summary>読み取り専用のシンボル表 (Lookup のみ)。OnReduce で宣言を防ぐ。</summary>
    public abstract IReadOnlySymbolTable Symbols { get; }
}

/// <summary>SemanticContext の標準実装。[Enter]/[Exit] Walker で書き込み可能。</summary>
public class BasicSemanticContext : SemanticContext
{
    private readonly ScopedSymbolTable _symbols = new ScopedSymbolTable();
    /// <summary>読み取り専用ビュー (基底 API)。</summary>
    public override IReadOnlySymbolTable Symbols => _symbols;
    /// <summary>書き込み可能なシンボル表 ([Enter]/[Exit] で宣言・スコープ操作に使用)。</summary>
    public ScopedSymbolTable WritableSymbols => _symbols;
    /// <summary>診断バグ ([Enter]/[Exit] でエラー・警告の追加に使用)。</summary>
    public DiagnosticBag Diagnostics { get; } = new DiagnosticBag();
    /// <summary>ノード→型 の対応 (型推論・型チェックの結果)。</summary>
    public TypeContext Types { get; } = new TypeContext();
}
