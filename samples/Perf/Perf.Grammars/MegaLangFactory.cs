namespace Perf.Grammars;

/// <summary>
/// パターン: 総合大規模 (実プログラミング言語規模)。
/// <para>Program/Stmt(多数の代替)/Expr(深い優先度階層 + ネスト + 代入)/多数のキーワード を統合。
/// 生成・実行ともに最大の負荷。DeclStmt×30 + ExprStmt/If/While/Block + Expr(Num/Var/Paren/Assign + 17段階演算子)。</para>
/// </summary>
public static class MegaLangFactory
{
    public const string Namespace = "PerfMegaLang";
    public const string Root = "MegaProgram"; // Parser 名を MegaProgramParser に
    public const int DeclCount = 30;          // 宣言キーワード数

    // 構文記号 (=, :, ;) と衝突しない BinaryOps のインデックス (= 演算子を優先度 2..18 に割当)。
    private static readonly int[] OpIndices = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 12, 13, 14, 15, 16, 19 };

    public static GrammarSpec Create()
    {
        var spec = new GrammarSpec(Namespace, Root, skipRegex: @"(\s|//[^\n]*)+");
        spec.AddAbstract(Root, "AstNode");

        // --- 文リスト ---
        spec.AddSealed("ConsStmt", Root).Ctor(
            new ParamSpec("Stmt", "first"),
            new ParamSpec(Root, "rest"));
        spec.AddSealed("NilProgram", Root).Ctor();

        // --- 文 ---
        spec.AddAbstract("Stmt", "AstNode");
        for (int i = 0; i < DeclCount; i++)
        {
            spec.AddSealed("DeclStmt" + i, "Stmt").Ctor(
                new ParamSpec("Token", "kw", "decl" + i, priority: 1),
                new ParamSpec("Token", "semi", ";"));
        }
        spec.AddSealed("ExprStmt", "Stmt").Ctor(
            new ParamSpec("Expr", "expr"),
            new ParamSpec("Token", "semi", ";"));
        spec.AddSealed("IfStmt", "Stmt").Ctor(
            new ParamSpec("Token", "kwIf", "if", priority: 2),
            new ParamSpec("Token", "lp", @"\("),
            new ParamSpec("Expr", "cond"),
            new ParamSpec("Token", "rp", @"\)"),
            new ParamSpec("Stmt", "body"));
        spec.AddSealed("WhileStmt", "Stmt").Ctor(
            new ParamSpec("Token", "kwWhile", "while", priority: 2),
            new ParamSpec("Token", "lp", @"\("),
            new ParamSpec("Expr", "cond"),
            new ParamSpec("Token", "rp", @"\)"),
            new ParamSpec("Stmt", "body"));
        spec.AddSealed("BlockStmt", "Stmt").Ctor(
            new ParamSpec("Token", "lb", @"\{"),
            new ParamSpec(Root, "body"),
            new ParamSpec("Token", "rb", @"\}"));

        // --- 式 ---
        spec.AddAbstract("Expr", "AstNode");
        spec.AddSealed("NumExpr", "Expr").Ctor(new ParamSpec("Token", "num", "[0-9]+"));
        spec.AddSealed("VarExpr", "Expr").Ctor(new ParamSpec("Token", "name", @"[A-Za-z_]\w*", priority: 0));
        spec.AddSealed("ParenExpr", "Expr").Ctor(
            new ParamSpec("Token", "lp", @"\("),
            new ParamSpec("Expr", "inner"),
            new ParamSpec("Token", "rp", @"\)"));
        // 代入 (優先度1・右結合)
        spec.AddSealed("AssignExpr", "Expr", precedence: 1, isRightAssoc: true).Ctor(
            new ParamSpec("Expr", "left"),
            new ParamSpec("Token", "op", "="),
            new ParamSpec("Expr", "right"));
        // 17 段階の二項演算子 (優先度 2..18)
        int prec = 2;
        foreach (var idx in OpIndices)
        {
            var (_, regex) = OpTable.BinaryOps[idx];
            spec.AddSealed("Op" + prec + "Expr", "Expr", precedence: prec).Ctor(
                new ParamSpec("Expr", "left"),
                new ParamSpec("Token", "op", regex),
                new ParamSpec("Expr", "right"));
            prec++;
        }
        return spec;
    }
}
