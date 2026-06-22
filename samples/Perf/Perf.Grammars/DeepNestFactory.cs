namespace Perf.Grammars;

/// <summary>
/// パターン: 深いネスト。
/// <para>括弧式 (((...)))。文法は小さい (3ノード, 2規則)。実行で効く: ネスト深さ → スタック深さ・パース時間。</para>
/// </summary>
public static class DeepNestFactory
{
    public const string Namespace = "PerfDeepNest";
    public const string Root = "NestExpr"; // Parser 名を NestExprParser に

    public static GrammarSpec Create()
    {
        var spec = new GrammarSpec(Namespace, Root, skipRegex: @"\s+");
        spec.AddAbstract(Root, "AstNode");
        spec.AddSealed("NumExpr", Root).Ctor(new ParamSpec("Token", "num", "[0-9]+"));
        spec.AddSealed("ParenExpr", Root).Ctor(
            new ParamSpec("Token", "lp", @"\("),
            new ParamSpec(Root, "inner"),
            new ParamSpec("Token", "rp", @"\)"));
        return spec;
    }
}
