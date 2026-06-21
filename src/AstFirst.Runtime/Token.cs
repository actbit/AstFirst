namespace AstFirst;

/// <summary>
/// 終端記号 (トークン) の基底。ユーザーは直接使うか、派生クラスで
/// トークン種別を表現する。字面とソース範囲を持つ。
/// </summary>
public abstract class Token
{
    /// <summary>トークンの字面。</summary>
    public string Text { get; }

    /// <summary>ソース上の範囲。</summary>
    public SourceSpan Span { get; }

    protected Token(string text, SourceSpan span)
    {
        Text = text;
        Span = span;
    }

    public override string ToString() => Text;
}

/// <summary>Token の単純な具象実装。生成コードが LexToken から変換して使う。</summary>
public sealed class BasicToken : Token
{
    public BasicToken(string text, SourceSpan span) : base(text, span) { }
}
