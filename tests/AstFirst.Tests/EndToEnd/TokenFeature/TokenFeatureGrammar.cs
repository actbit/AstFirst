using AstFirst;

namespace AstFirst.Tests.EndToEnd.TokenFeature;

/// <summary>Token 派生型。コンストラクタで int.Parse しない (Text="" の挿入トークンでも例外なし)。
/// LALR・LightGlr 両文法で [Token] 引数に使い、reduce 時の引き継ぎを検証する。</summary>
public sealed class NumberToken : Token
{
    public NumberToken(string text) : base(text, default) { }
}
