using AstFirst;

namespace AstFirst.Tests.EndToEnd;

/// <summary>
/// コンストラクタに注入された SemanticContext の診断が ParseResult.Diagnostics に
/// 伝わることを検証する E2E テスト (C2 の核心経路)。
/// </summary>
public class SemanticContextIntegrationTests
{
    [Fact]
    public void CtorDiagnostic_DuplicateDeclaration_FlowsToParseResult()
    {
        var result = SymStmtParser.Parse("let x; let x;");
        Assert.Contains(result.Diagnostics, d => d.Severity == Severity.Error && d.Message.Contains("既に宣言"));
    }

    [Fact]
    public void CtorDiagnostic_UndeclaredUse_FlowsToParseResult()
    {
        var result = SymStmtParser.Parse("use x;");
        Assert.Contains(result.Diagnostics, d => d.Severity == Severity.Error && d.Message.Contains("宣言されていません"));
    }

    [Fact]
    public void NoDiagnostic_WhenDeclaredBeforeUse()
    {
        var result = SymStmtParser.Parse("let x; use x;");
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void HasErrors_True_WhenSemanticError()
    {
        var result = SymStmtParser.Parse("use x;");
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void CustomContext_ReceivesDiagnosticsAndFlowsToResult()
    {
        // 独自 SemanticContext 派生を Parse(string, ctx) で渡す
        var ctx = new RecordingContext();
        var result = SymStmtParser.Parse("use x;", ctx);
        Assert.NotEmpty(ctx.Diagnostics.Items); // ctx 側に蓄積
        Assert.NotEmpty(result.Diagnostics);    // ParseResult にも伝播
    }

    /// <summary>診断の蓄積を観測するための独自 SemanticContext。</summary>
    private sealed class RecordingContext : SemanticContext
    {
        public override ScopedSymbolTable Symbols { get; } = new ScopedSymbolTable();
        public override DiagnosticBag Diagnostics { get; } = new DiagnosticBag();
    }
}
