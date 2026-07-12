using AstFirst;

namespace AstFirst.Tests.EndToEnd.Semantics;

/// <summary>
/// [Enter]/[Exit] 属性ベース意味解析の E2E テスト。
/// OnReduce は読み取り専用 ctx、宣言・参照解決は [Enter] (2パス目 Walker) で行う。
/// </summary>
public class AttributeSemanticTests
{
    [Fact]
    public void Declare_NoDiagnostic()
    {
        var result = AttrStmtParser.Parse("let x;");
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
        // 独自 ctx 派生を渡し、[Enter] 属性メソッドに注入されることを確認
        var ctx = new AttrCountingContext();
        var result = AttrStmtParser.Parse("let x;", ctx);
        // [Enter] Declare が呼ばれ、Symbols に登録される
        Assert.NotNull(ctx.WritableSymbols.Lookup("x"));
        Assert.Empty(result.Diagnostics);
    }

    /// <summary>カスタム ctx の注入を確認するための BasicSemanticContext 派生。</summary>
    public sealed class AttrCountingContext : BasicSemanticContext { }
}
