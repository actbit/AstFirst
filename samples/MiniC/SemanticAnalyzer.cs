using AstFirst;

namespace MiniC;

/// <summary>
/// MiniC の意味解析ヘルパー。各ノードの OnSecondPassEnter/Exit から呼ばれる (旧 ProgramListener モデルに代わる)。
/// Generator が Parse 後にトップダウンで OnSecondPassEnter→子再帰→OnSecondPassExit を回すため、
/// 式の型伝播 (Exit) → 条件/代入の型チェック (Exit) が正しく順序付く。
/// </summary>
public static class SemanticAnalyzer
{
    public static readonly TypeSymbol Int = new("int");
    public static readonly TypeSymbol Bool = new("bool");

    // --- 宣言: int x; / int x = expr; ---
    public static void EnterDecl(DeclStmt n, MiniCContext ctx)
    {
        if (!ctx.Symbols.TryDeclare(n.Name, n.Span, null, out _))
            ctx.Diagnostics.Error($"変数 '{n.Name}' は既に宣言されています", n.Span);
    }
    public static void EnterDeclInit(DeclStmtInit n, MiniCContext ctx)
    {
        if (!ctx.Symbols.TryDeclare(n.Name, n.Span, null, out _))
            ctx.Diagnostics.Error($"変数 '{n.Name}' は既に宣言されています", n.Span);
    }
    public static void ExitDeclInit(DeclStmtInit n, MiniCContext ctx)
        => CheckAssignable(n.Init, Int, $"int 変数 '{n.Name}' に ", n.Span, ctx);

    // --- 代入 ---
    public static void EnterAssign(AssignStmt n, MiniCContext ctx)
    {
        var sym = ctx.Symbols.ResolveOrError(n.Name, n.Span, ctx.Diagnostics);
        if (sym is not null) n.SetAnnotation("symbol", sym); // 束縛: ノードにシンボルを紐付け
    }
    public static void ExitAssign(AssignStmt n, MiniCContext ctx)
        => CheckAssignable(n.Value, Int, $"int 変数 '{n.Name}' に ", n.Span, ctx);

    // --- 変数参照 ---
    public static void EnterVar(VarExpr n, MiniCContext ctx)
    {
        var sym = ctx.Symbols.ResolveOrError(n.Name, n.Span, ctx.Diagnostics);
        if (sym is not null)
        {
            n.SetAnnotation("symbol", sym); // 束縛
            ctx.Types.SetType(n, Int);      // MiniC の変数は int
        }
    }

    // --- 型伝播ヘルパ (各 Expr ノードの Exit から呼ばれる) ---
    public static void SetType(AstNode node, TypeSymbol t, MiniCContext ctx) => ctx.Types.SetType(node, t);
    public static void PropagateType(AstNode node, AstNode inner, MiniCContext ctx)
    {
        if (ctx.Types.TypeOf(inner) is { } t) ctx.Types.SetType(node, t);
    }

    // --- 条件の型チェック (if/while の Exit) ---
    public static void ExitCondition(Expr cond, SourceSpan span, string construct, MiniCContext ctx)
    {
        if (ctx.Types.TypeOf(cond) is { } t && !Bool.IsAssignableFrom(t))
            ctx.Diagnostics.Error($"{construct} の条件は bool が必要です (実際: {t.Name})", span);
    }

    private static void CheckAssignable(Expr value, TypeSymbol expected, string prefix, SourceSpan span, MiniCContext ctx)
    {
        if (ctx.Types.TypeOf(value) is { } t && !expected.IsAssignableFrom(t))
            ctx.Diagnostics.Error($"{prefix}{t.Name} を代入できません", span);
    }
}
