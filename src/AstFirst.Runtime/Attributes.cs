using System;

namespace AstFirst;

/// <summary>
/// コンストラクタ引数の字句ルール (正規表現)。Token 派生クラスの引数、
/// または AST クラスのトークン引数に付ける。優先度/結合性もここで指定する。
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class PatternAttribute(string regex) : Attribute
{
    public string Regex { get; } = regex;

    /// <summary>演算子優先度 (大きいほど高優先。* を + より大きくする等)。
    /// 同一入力で複数トークンが受理した際のレクサ優先度と shift-reduce 衝突解決に使う。</summary>
    public int Priority { get; set; }

    /// <summary>右結合 (代入 = や べき乗 ** 等)。既定は左結合。</summary>
    public bool IsRightAssociative { get; set; }
}

/// <summary>コンストラクタ引数に意味解析コンテキストを注入することを示す。</summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class ContextAttribute : Attribute { }

/// <summary>文法の開始記号 (ルート非終端) のクラスに付ける。Generator の抽出開始点。</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class GrammarAttribute : Attribute
{
    /// <summary>複数フォーマット/方言時のモード名 (フェーズ7)。</summary>
    public string? Mode { get; set; }
}

/// <summary>トークン種別の絞り込み。</summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class ExpectAttribute : Attribute
{
    public object Token { get; }
    public ExpectAttribute(object token) { Token = token; }
}

/// <summary>スキップパターン (空白・コメント等)。クラスまたはアセンブリに付ける。</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly)]
public sealed class SkipAttribute(string regex) : Attribute
{
    public string Regex { get; } = regex;
}
