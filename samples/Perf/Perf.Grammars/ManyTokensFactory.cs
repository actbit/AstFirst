namespace Perf.Grammars;

/// <summary>
/// パターン: 多数のトークン。
/// <para>100種のキーワード (kw0..kw99, Priority=1) + 識別子 ([A-Za-z_]\w*, Priority=0)。
/// 生成で効く: 終端数 → DFA 状態数・テーブル幅 (シンボル数)。実行で効く: 多キーワード入力の字句解析。</para>
/// </summary>
public static class ManyTokensFactory
{
    public const string Namespace = "PerfManyTokens";
    public const string Root = "TokenProgram"; // Parser 名を TokenProgramParser に
    public const int KeywordCount = 100;

    public static GrammarSpec Create()
    {
        var spec = new GrammarSpec(Namespace, Root, skipRegex: @"\s+");
        spec.AddAbstract(Root, "AstNode");

        spec.AddSealed("NilProgram", Root).Ctor(); // ε

        // 100種のキーワード: Priority=1 で識別子に勝つ。各規則は (キーワード, 再帰)。
        for (int i = 0; i < KeywordCount; i++)
        {
            spec.AddSealed("Kw" + i, Root).Ctor(
                new ParamSpec("Token", "kw", "kw" + i, priority: 1),
                new ParamSpec(Root, "rest"));
        }

        // 識別子 (Priority=0): キーワードでない [A-Za-z_]\w*。
        spec.AddSealed("IdentNode", Root).Ctor(
            new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0),
            new ParamSpec(Root, "rest"));

        return spec;
    }
}
