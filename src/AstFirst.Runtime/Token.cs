using System;

namespace AstFirst;

/// <summary>
/// 終端記号 (トークン) の基底。ユーザーは直接使うか、派生クラスで
/// トークン種別を表現する。字面とソース範囲を持つ。
/// 字面は元ソースのスライスで遅延保持し、Text は必要時だけ生成する
/// (トークン化/構文解析時の文字列アロケーションを抑える)。
/// </summary>
public abstract class Token
{
    private readonly ReadOnlyMemory<char> _textSpan;
    private string? _text;

    /// <summary>ソース上の範囲。</summary>
    public SourceSpan Span { get; }

    protected Token(string text, SourceSpan span)
    {
        _text = text;
        _textSpan = default;
        Span = span;
    }

    /// <summary>字面をスライスで持つ (Substring を遅延)。生成コードが LexToken.Span から変換して使う。</summary>
    protected Token(ReadOnlyMemory<char> textSpan, SourceSpan span)
    {
        _textSpan = textSpan;
        _text = null;
        Span = span;
    }

    /// <summary>トークンの字面。スライスから必要時だけ生成する (Substring を遅延)。</summary>
    public string Text => _text ??= _textSpan.ToString();

    /// <summary>ErrorRepair で挿入されたトークンか (ユーザーが書いていない)。</summary>
    public bool IsInserted { get; set; }

    /// <summary>[Token]/[Pattern] の Kind 属性で指定された種別 (例: "number", "keyword")。</summary>
    public string? Kind { get; set; }

    public override string ToString() => Text;
}

/// <summary>Token の単純な具象実装。生成コードが LexToken から変換して使う。</summary>
public sealed class BasicToken : Token
{
    public BasicToken(string text, SourceSpan span) : base(text, span) { }

    /// <summary>字面をスライスで持つ (Substring を遅延)。</summary>
    public BasicToken(ReadOnlyMemory<char> textSpan, SourceSpan span) : base(textSpan, span) { }
}
