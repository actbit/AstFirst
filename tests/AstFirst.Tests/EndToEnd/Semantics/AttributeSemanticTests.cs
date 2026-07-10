using AstFirst;

namespace AstFirst.Tests.EndToEnd.Semantics;

/// <summary>
/// [OnReduce]/[Enter]/[Exit] 属性ベース意味解析の E2E テスト。
/// Generator が [Grammar] ルートクラスの属性付き static メソッドを収集し、
/// Walker/コンストラクタに dispatch すること、ctx が自動注入されることを検証する。
/// </summary>
public class AttributeSemanticTests
{
    [Fact]
    public void OnReduce_Declare_NoDiagnostic()
    {
        var result = AttrStmtParser.Parse("let x;");
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void OnReduce_DuplicateDeclaration_Diagnostic()
    {
        var result = AttrStmtParser.Parse("let x; let x;");
        Assert.Contains(result.Diagnostics, d => d.Severity == Severity.Error && d.Message.Contains("既に宣言"));
    }

    [Fact]
    public void Enter_DeclaredUse_NoDiagnostic()
    {
        // [OnReduce] で let x を宣言 → 2パス目 [Enter] で use x を解決 (見つかる)
        var result = AttrStmtParser.Parse("let x; use x;");
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Enter_UndeclaredUse_Diagnostic()
    {
        var result = AttrStmtParser.Parse("use x;");
        Assert.Contains(result.Diagnostics, d => d.Severity == Severity.Error && d.Message.Contains("宣言されていません"));
    }

    [Fact]
    public void HasErrors_True_WhenSemanticError()
    {
        var result = AttrStmtParser.Parse("use x;");
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void CustomContext_AttributeRule_ReceivesCtx()
    {
        // 独自 ctx 派生を渡し、[OnReduce]/[Enter] 属性メソッドに注入されることを確認
        var ctx = new AttrCountingContext();
        var result = AttrStmtParser.Parse("let x; use x;", ctx);
        // [OnReduce] Declare が ctx に注入されて呼ばれ、Symbols に登録される
        Assert.NotNull(ctx.Symbols.Lookup("x"));
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>カスタム ctx の注入を確認するための BasicSemanticContext 派生。</summary>
    public sealed class AttrCountingContext : BasicSemanticContext { }
}
