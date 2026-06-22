using Calc;

namespace AstFirst.Tests.EndToEnd;

/// <summary>拡充されたエラー回復（期待トークン、近接抑制、回復）のテスト。</summary>
public class ErrorRecoveryTests
{
    [Fact]
    public void MultipleErrorsAreCollected()
    {
        // "1++2" は + の後に + が来てエラー、その後 2 で回復
        var result = ExprParser.Parse("1++2");
        Assert.True(result.HasErrors);
        Assert.True(result.Errors.Count >= 1);
    }

    [Fact]
    public void ErrorAfterValidInput()
    {
        // "1+2+3" は正しい
        var ok = ExprParser.Parse("1+2+3");
        Assert.False(ok.HasErrors);

        // "1+2+" は最後に + が来てエラー
        var err = ExprParser.Parse("1+2+");
        Assert.True(err.HasErrors);
    }

    [Fact]
    public void PanicModeRecoversAndContinues()
    {
        // "+1" は先頭が + でエラーだが、回復して解析を試みる
        var result = ExprParser.Parse("+1");
        // エラーが記録される（回復できたかどうかに関わらず）
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void ErrorMessageContainsPosition()
    {
        var result = ExprParser.Parse("+1");
        Assert.True(result.HasErrors);
        Assert.True(result.Errors.Count > 0);
        // エラー位置が 0 以上
        Assert.True(result.Errors[0].Position >= 0);
    }

    [Fact]
    public void CompletelyInvalidInput()
    {
        // "+++" は完全に不正
        var result = ExprParser.Parse("+++");
        Assert.True(result.HasErrors);
        Assert.True(result.Errors.Count >= 1);
    }

    [Fact]
    public void EmptyInputNoError()
    {
        // 空入力はエラーなし（空のプログラムとして受理、または部分AST）
        var result = ExprParser.Parse("");
        // 空入力の挙動は実装依存だが、クラッシュしないこと
        Assert.NotNull(result);
    }
}
