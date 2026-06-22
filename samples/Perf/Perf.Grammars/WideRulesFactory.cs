namespace Perf.Grammars;

/// <summary>
/// パターン: 幅広の規則数。
/// <para>1つの非終端 (Stmt) に多数の代替規則 (100種の文)。生成で効く: LALR 状態数。
/// 実行で効く: 多数の文のシーケンスのパース。</para>
/// </summary>
public static class WideRulesFactory
{
    public const string Namespace = "PerfWideRules";
    public const string Root = "WideProgram"; // Parser 名を WideProgramParser に
    public const int RuleCount = 100;

    public static GrammarSpec Create()
    {
        var spec = new GrammarSpec(Namespace, Root, skipRegex: @"\s+");
        spec.AddAbstract(Root, "AstNode");

        // リスト (Cons/Nil イディオム)
        spec.AddSealed("ConsStmt", Root).Ctor(
            new ParamSpec("Stmt", "first"),
            new ParamSpec(Root, "rest"));
        spec.AddSealed("NilProgram", Root).Ctor(); // ε

        // Stmt 抽象 (非終端)
        spec.AddAbstract("Stmt", "AstNode");

        // 100種の文: 各1規則 (キーワード s{i} + セミコロン)。
        // キーワードは最長一致で弁別 (s1 と s10 等)。識別子パターンを持たないので Priority 不要。
        for (int i = 0; i < RuleCount; i++)
        {
            spec.AddSealed("Stmt" + i, "Stmt").Ctor(
                new ParamSpec("Token", "kw", "s" + i),
                new ParamSpec("Token", "semi", ";"));
        }
        return spec;
    }
}
