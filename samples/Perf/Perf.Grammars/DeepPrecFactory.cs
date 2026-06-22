namespace Perf.Grammars;

/// <summary>
/// パターン: 深い優先度階層。
/// <para>20 段階の二項演算子 (各優先度に対応するノードクラス)。shift-reduce 衝突解決が多数。
/// 生成で効く: LALR 状態数・生成コードサイズ・ビルド時間。実行で効く: 演算子の多い数式のパース。</para>
/// </summary>
public static class DeepPrecFactory
{
    public const string Namespace = "PerfDeepPrec";
    public const string Root = "PrecExpr"; // Parser 名を PrecExprParser に (他パターンと衝突回避)
    public const int Levels = 20;

    public static GrammarSpec Create()
    {
        var spec = new GrammarSpec(Namespace, Root, skipRegex: @"\s+");
        spec.AddAbstract(Root, "AstNode");

        // リーフ: 数値
        spec.AddSealed("NumExpr", Root).Ctor(new ParamSpec("Token", "num", "[0-9]+"));

        // 20 段階の二項演算子。優先度 1..20 (大きいほど高優先)。
        for (int i = 0; i < Levels; i++)
        {
            int level = i + 1;
            var (_, regex) = OpTable.BinaryOps[i];
            spec.AddSealed("Op" + level + "Expr", Root, precedence: level).Ctor(
                new ParamSpec(Root, "left"),
                new ParamSpec("Token", "op", regex),
                new ParamSpec(Root, "right"));
        }
        return spec;
    }
}
