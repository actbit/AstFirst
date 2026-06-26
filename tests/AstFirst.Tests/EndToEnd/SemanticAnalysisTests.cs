using MiniC;
using AstFirst;

namespace AstFirst.Tests.EndToEnd;

public class SemanticAnalysisTests
{
    private static IReadOnlyList<Diagnostic> Analyze(string code)
    {
        var result = ProgramParser.Parse(code, new MiniCContext());
        return result.Diagnostics;
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
        Assert.True(diags[0].Span.Start.Offset >= 0);
    }

    // --- 実用シナリオ ---

    [Fact]
    public void NestedBlock_DeclarationScopedToBlock()
    {
        Assert.Equal(1, ErrorCount("""
            if (true) {
                int y;
            }
            print(y);
        """));
    }

    [Fact]
    public void NestedBlock_ReferenceInsideBlock_OK()
    {
        Assert.Equal(0, ErrorCount("""
            if (true) {
                int y;
                print(y);
            }
        """));
    }

    [Fact]
    public void SiblingBlocks_SameNameAllowed()
    {
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
        Assert.Equal(2, ErrorCount("""
            print(a);
            print(b);
        """));
    }

    [Fact]
    public void UndeclaredReference_InIfCondition()
    {
        // if (x) の x は未宣言 → 1エラー。型チェックは (型がないので) スキップ。
        Assert.Equal(1, ErrorCount("if (x) print(1);"));
        Assert.Equal(1, ErrorCount("while (x) print(1);"));
    }

    [Fact]
    public void Initializer_UndeclaredReference()
    {
        Assert.Equal(1, ErrorCount("int x = y;"));
    }

    [Fact]
    public void Initializer_SelfReference_Allowed()
    {
        Assert.Equal(0, ErrorCount("int x = x;"));
    }

    // --- 型チェック ---

    [Fact]
    public void TypeCheck_IfConditionMustBeBool()
    {
        // if (1) の条件は int → 型エラー
        Assert.Equal(1, ErrorCount("if (1) print(1);"));
        Assert.Equal(1, ErrorCount("while (1) print(1);"));
    }

    [Fact]
    public void TypeCheck_IfConditionBool_OK()
    {
        Assert.Equal(0, ErrorCount("if (true) print(1);"));
    }

    [Fact]
    public void TypeCheck_AssignBoolToInt_Error()
    {
        // int 変数に bool を代入 → 型エラー
        Assert.Equal(1, ErrorCount("""
            int x;
            x = true;
        """));
    }

    [Fact]
    public void TypeCheck_DeclInitBoolToInt_Error()
    {
        // int 変数の初期化式に bool → 型エラー
        Assert.Equal(1, ErrorCount("int x = true;"));
    }

    [Fact]
    public void TypeCheck_ArithmeticIsInt_OK()
    {
        // 算術結果は int。int 変数への代入は OK。
        Assert.Equal(0, ErrorCount("int x = 1 + 2 * 3;"));
    }

    // --- 位置情報 (行・列) ---

    [Fact]
    public void Diagnostic_HasCorrectLineColumn()
    {
        // 2 行目の x が未宣言 → 診断の位置は行 2 (これまでは常に 0 だった)
        var diags = Analyze("int a;\nprint(x);");
        Assert.Single(diags);
        Assert.Equal(2, diags[0].Span.Start.Line);
        Assert.True(diags[0].Span.Start.Column > 0);
    }
}
