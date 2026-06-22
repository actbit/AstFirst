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

    // --- 実用シナリオ ---

    [Fact]
    public void NestedBlock_DeclarationScopedToBlock()
    {
        // if 本体のブロック内で宣言した変数は、ブロック外では見えない
        Assert.Equal(1, ErrorCount("""
            if (1) {
                int y;
            }
            print(y);
        """));
    }

    [Fact]
    public void NestedBlock_ReferenceInsideBlock_OK()
    {
        // ブロック内で宣言してブロック内で使うのは OK
        Assert.Equal(0, ErrorCount("""
            if (1) {
                int y;
                print(y);
            }
        """));
    }

    [Fact]
    public void SiblingBlocks_SameNameAllowed()
    {
        // 兄弟ブロックで同名宣言は各スコープ独立 → エラーなし
        Assert.Equal(0, ErrorCount("""
            {
                int x;
            }
            {
                int x;
            }
        """));
    }

    [Fact]
    public void MultipleErrors_Accumulated()
    {
        // 1パースで複数の未宣言参照を取りこぼさず検出する
        Assert.Equal(2, ErrorCount("""
            print(a);
            print(b);
        """));
    }

    [Fact]
    public void IfCondition_UndeclaredReference()
    {
        // if / while の条件式の変数も未宣言チェックの対象
        Assert.Equal(1, ErrorCount("if (x) print(1);"));
        Assert.Equal(1, ErrorCount("while (x) print(1);"));
    }

    [Fact]
    public void Initializer_UndeclaredReference()
    {
        // 初期化式で未宣言変数を参照 → エラー
        Assert.Equal(1, ErrorCount("int x = y;"));
    }

    [Fact]
    public void Initializer_SelfReference_Allowed()
    {
        // int x = x; は C 風に「宣言後に初期化式を評価」→ x は見える (エラーにならない)
        Assert.Equal(0, ErrorCount("int x = x;"));
    }
}
