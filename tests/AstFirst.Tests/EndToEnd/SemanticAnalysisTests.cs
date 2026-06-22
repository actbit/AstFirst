using MiniC;
using AstFirst;

namespace AstFirst.Tests.EndToEnd;

public class SemanticAnalysisTests
{
    private static IReadOnlyList<Diagnostic> Analyze(string code)
    {
        var result = ProgramParser.Parse(code);
        return new SemanticAnalyzer().Analyze(result.Ast as Program);
    }

    private static int ErrorCount(string code) => Analyze(code).Count(d => d.Severity == Severity.Error);

    [Fact]
    public void ValidProgram_NoDiagnostics()
    {
        Assert.Equal(0, ErrorCount("""
            int x = 10;
            int y = x + 1;
            print(y);
        """));
    }

    [Fact]
    public void UndeclaredReference_InExpr()
    {
        Assert.Equal(1, ErrorCount("print(x);"));
    }

    [Fact]
    public void UndeclaredReference_InAssignment()
    {
        Assert.Equal(1, ErrorCount("x = 1;"));
    }

    [Fact]
    public void DuplicateDeclaration_SameScope()
    {
        Assert.Equal(1, ErrorCount("""
            int x;
            int x;
        """));
    }

    [Fact]
    public void OutOfScope_ReferenceAfterBlock()
    {
        Assert.Equal(1, ErrorCount("""
            {
                int inner;
            }
            print(inner);
        """));
    }

    [Fact]
    public void Shadowing_Allowed_NoDiagnostic()
    {
        Assert.Equal(0, ErrorCount("""
            int x;
            {
                int x;
                print(x);
            }
        """));
    }

    [Fact]
    public void OuterVisible_FromInnerScope()
    {
        Assert.Equal(0, ErrorCount("""
            int x;
            {
                print(x);
            }
        """));
    }

    [Fact]
    public void Diagnostics_CarrySourceSpan()
    {
        var diags = Analyze("print(x);");
        Assert.Single(diags);
        // 宣言位置の offset は token 化で設定される (行・列は現在 0,0)
        Assert.True(diags[0].Span.Start.Offset >= 0);
    }
}
