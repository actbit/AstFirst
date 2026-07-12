using AstFirst;

namespace AstFirst.Tests.EndToEnd;

/// <summary>
/// OnReduce の ctx が読み取り専用 (SemanticContext) であることを検証。
/// ctx.Symbols.Lookup は可能、ctx.Diagnostics.Error はコンパイルエラー。
/// </summary>
public class SemanticContextIntegrationTests
{
    [Fact]
    public void ParsesWithReadOnlyCtx()
    {
        var result = SymStmtParser.Parse("let x;");
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void CustomContext_WorksAsBasicSemanticContext()
    {
        var ctx = new RecordingContext();
        var result = SymStmtParser.Parse("use x;", ctx);
        Assert.False(result.HasErrors);
    }

    /// <summary>独自 BasicSemanticContext。</summary>
    private sealed class RecordingContext : BasicSemanticContext { }
}
