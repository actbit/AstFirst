namespace AstFirst.Core.Lexing;

/// <summary>レクサの字句規則。1つのトークン種別に対応する正規表現パターン。</summary>
public sealed class LexerRule
{
    public string Pattern { get; }
    public int TokenId { get; }
    /// <summary>小さいほど高優先。同長で複数受理したとき勝つ。</summary>
    public int Priority { get; }
    /// <summary>true のときトークン列から除外 (コメント・空白等)。</summary>
    public bool IsHidden { get; }

    public LexerRule(string pattern, int tokenId, int priority = 0, bool isHidden = false)
    {
        Pattern = pattern;
        TokenId = tokenId;
        Priority = priority;
        IsHidden = isHidden;
    }
}
