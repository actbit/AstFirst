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
}

/// <summary>文法の開始記号 (ルート非終端) のクラスに付ける。Generator の抽出開始点。</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class GrammarAttribute : Attribute
{
    /// <summary>複数フォーマット/方言時のモード名 (フェーズ7)。</summary>
    public string? Mode { get; set; }
}

/// <summary>スキップパターン (空白・コメント等)。クラスまたはアセンブリに付ける。</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly)]
public sealed class SkipAttribute(string regex) : Attribute
{
    public string Regex { get; } = regex;
}

/// <summary>
/// 演算子優先度/結合性。演算ノード (AST クラス) に付ける。
/// shift-reduce 衝突を解決する。大きいほど高優先。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class PrecedenceAttribute(int priority) : Attribute
{
    public int Priority { get; } = priority;
    /// <summary>右結合 (= や **)。既定は左結合。</summary>
    public bool IsRightAssociative { get; set; }
    /// <summary>非結合 (&lt; &gt; 等)。IsRightAssociative より優先。</summary>
    public bool IsNonAssociative { get; set; }
}

/// <summary>
/// 生成規則（右辺）を定義する static メソッドに付ける。1クラスにつき1つ。
/// メソッド名は任意（属性で識別）。引数が右辺（型ベースで分類）、親非終端はクラスの継承元から推論。
/// 本体は構文定義の宣言のみで実行されない（意味アクションは OnReduce に書く）。
/// </summary>
/// <remarks>
/// public sealed partial class AddExpr : Expr
/// {
///     [Rule]
///     public static void Plus(Expr left, [Token("+")] Token op, Expr right, MyCtx ctx) { }
///     partial void OnReduce(MyCtx ctx) { ... }
/// }
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RuleAttribute : Attribute { }

/// <summary>
/// 終端記号 (Token) の字句ルール (正規表現)。Token 型引数に付ける。<see cref="PatternAttribute"/> の別名。
/// Priority でレクサ優先度 (大きいほど高優先。キーワード > 識別子 等)。
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class TokenAttribute(string regex) : Attribute
{
    public string Regex { get; } = regex;
    /// <summary>演算子/トークン優先度。同一入力で複数トークン受理時のレクサ優先度。</summary>
    public int Priority { get; set; }
}
