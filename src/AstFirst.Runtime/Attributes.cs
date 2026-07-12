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
    public int Priority { get; set; }
    /// <summary>トークンの種別 (例: "number", "keyword", "operator")。Token.Kind に設定される。</summary>
    public string? Kind { get; set; }
}

/// <summary>パーサの実行モード。</summary>
public enum ParseMode
{
    /// <summary>LALR(1) 確定パーサ (既定)。コンフリクトは優先度/結合性で解決し、解決不能分は警告 (ASTF001)。</summary>
    Lalr,
    /// <summary>軽量 GLR: コンフリクトセルで並行 fork し、収束でマージ。本質的曖昧性 (cast/paren, generic の型/式 等) を扱う。</summary>
    LightGlr,
}

/// <summary>文法の開始記号 (ルート非終端) のクラスに付ける。Generator の抽出開始点。</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class GrammarAttribute : Attribute
{
    /// <summary>複数フォーマット/方言時のモード名。</summary>
    public string? Mode { get; set; }
    /// <summary>パーサの実行モード。既定は <see cref="ParseMode.Lalr"/> (確定 LALR(1))。
    /// <see cref="ParseMode.LightGlr"/> は軽量 GLR (コンフリクトを並行 fork で解決)。</summary>
    public ParseMode ParseMode { get; set; } = ParseMode.Lalr;
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
    /// <summary>トークンの種別。Token.Kind に設定される。</summary>
    public string? Kind { get; set; }
}

/// <summary>
/// 引数がリスト（繰り返し）であることを示す。付いた引数は IReadOnlyList&lt;T&gt; として展開される。
/// Min=1 (既定) は1回以上 (Plus)、Min=0 は0回以上 (Star、空リスト可)。
/// 例: [Rule] static void Body([Repeat] Stmt statements) → Program → Stmt+ (List_T → Stmt | List_T Stmt)
/// 例: [Rule] static void Body([Repeat(Min=0)] Stmt statements) → Stmt* (List_T → Stmt | List_T Stmt | ε)
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class RepeatAttribute : Attribute
{
    /// <summary>最小繰り返し回数。1 = 1回以上 (Plus、既定)、0 = 0回以上 (Star、空リスト可)。</summary>
    public int Min { get; set; } = 1;
}

/// <summary>
/// 意味解析ルール: 2パス目 (トップダウン) でノードに入る時に呼ぶ static メソッドに付ける。
/// [Grammar] ルートクラス内に宣言し、第1引数に対象ノード型、第2引数に <see cref="SemanticContext"/> 派生の ctx を取る。
/// Generator が Walker の Enter フェーズで自動呼出し、ctx のキャストも自動で挿入する (ボイラープレート不要)。
/// </summary>
/// <remarks>
/// [Grammar]
/// partial class MyGrammar
/// {
///     [Enter]
///     static void EnterBlock(Block b, MyCtx ctx) => ctx.Symbols.PushScope();
/// }
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class EnterAttribute : Attribute { }

/// <summary>
/// 意味解析ルール: 2パス目でノードを出る時に呼ぶ static メソッドに付ける。
/// <see cref="EnterAttribute"/> と対。スコープの Pop や、子ノードの型が揃った後の型チェックに使う。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ExitAttribute : Attribute { }

/// <summary>
/// 意味解析ルール: 1パス目 (reduce 時・ボトムアップ) に呼ぶ static メソッドに付ける。
/// [Grammar] ルートクラスに宣言し、第1引数に対象ノード、第2引数に ctx。
/// 各ノードの partial OnReduce の直後に呼ばれる (partial OnReduce と共存)。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class OnReduceAttribute : Attribute { }
