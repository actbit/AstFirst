using System.Collections.Generic;
using AstFirst;

namespace MiniC;

/// <summary>
/// MiniC の意味解析。Generator が生成した <see cref="ProgramListener"/> を継承し、
/// Enter/Exit でスコープ管理・シンボル解決・型チェックを行う。<see cref="ProgramListener.Walk"/>
/// を呼ぶと Enter→子再帰→Exit の順に回り、式の型伝播 (Exit) → 条件/代入の型チェック (Exit) が正しく順序付く。
/// </summary>
public sealed class SemanticAnalyzer : ProgramListener
{
    private readonly ScopedSymbolTable _symbols = new();
    private readonly DiagnosticBag _diagnostics = new();
    private readonly TypeContext _types = new();

    private static readonly TypeSymbol Int = new("int");
    private static readonly TypeSymbol Bool = new("bool");

    public IReadOnlyList<Diagnostic> Analyze(MiniC.Program? program)
    {
        if (program is not null) Walk(program);
        return _diagnostics.Items;
    }

    // --- スコープ ---
    public override void EnterBlockStmt(BlockStmt node) => _symbols.PushScope();
    public override void ExitBlockStmt(BlockStmt node) => _symbols.PopScope();

    // --- 宣言 ---
    public override void EnterDeclStmt(DeclStmt node)
    {
        if (!_symbols.TryDeclare(node.Name, node.Span, null, out _))
            _diagnostics.Error($"変数 '{node.Name}' は既に宣言されています", node.Span);
    }
    public override void ExitDeclStmt(DeclStmt node)
    {
        // MiniC の変数は int 型。初期化式も int が必要。
        if (node.Init is not null)
            CheckAssignable(node.Init, Int, $"int 変数 '{node.Name}' に ", node.Span);
    }

    // --- 代入 ---
    public override void EnterAssignStmt(AssignStmt node)
    {
        var sym = _symbols.ResolveOrError(node.Name, node.Span, _diagnostics);
        if (sym is not null) node.SetAnnotation("symbol", sym); // 束縛: ノードにシンボルを紐付け
    }
    public override void ExitAssignStmt(AssignStmt node)
        => CheckAssignable(node.Value, Int, $"int 変数 '{node.Name}' に ", node.Span);

    // --- 変数参照 ---
    public override void EnterVarExpr(VarExpr node)
    {
        var sym = _symbols.ResolveOrError(node.Name, node.Span, _diagnostics);
        if (sym is not null)
        {
            node.SetAnnotation("symbol", sym); // 束縛
            _types.SetType(node, Int);          // MiniC の変数は int
        }
    }

    // --- リテラル ---
    public override void ExitNumExpr(NumExpr node) => _types.SetType(node, Int);
    public override void ExitBoolExpr(BoolExpr node) => _types.SetType(node, Bool);

    // --- 算術 (簡易: 子が int なら結果も int) ---
    public override void ExitAddExpr(AddExpr node) => _types.SetType(node, Int);
    public override void ExitSubExpr(SubExpr node) => _types.SetType(node, Int);
    public override void ExitMulExpr(MulExpr node) => _types.SetType(node, Int);
    public override void ExitDivExpr(DivExpr node) => _types.SetType(node, Int);
    public override void ExitNegExpr(NegExpr node)
    {
        if (_types.TypeOf(node.Inner) is { } t) _types.SetType(node, t);
    }
    public override void ExitParenExpr(ParenExpr node)
    {
        if (_types.TypeOf(node.Inner) is { } t) _types.SetType(node, t);
    }

    // --- 条件の型チェック ---
    public override void ExitIfStmt(IfStmt node) => CheckCondition(node.Cond, node.Cond.Span, "if");
    public override void ExitWhileStmt(WhileStmt node) => CheckCondition(node.Cond, node.Cond.Span, "while");

    private void CheckCondition(Expr cond, SourceSpan span, string construct)
    {
        if (_types.TypeOf(cond) is { } t && !Bool.IsAssignableFrom(t))
            _diagnostics.Error($"{construct} の条件は bool が必要です (実際: {t.Name})", span);
    }

    private void CheckAssignable(Expr value, TypeSymbol expected, string prefix, SourceSpan span)
    {
        if (_types.TypeOf(value) is { } t && !expected.IsAssignableFrom(t))
            _diagnostics.Error($"{prefix}{t.Name} を代入できません", span);
    }
}
