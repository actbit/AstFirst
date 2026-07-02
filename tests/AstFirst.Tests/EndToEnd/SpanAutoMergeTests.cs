using AstFirst.Tests.EndToEnd.SpanAutoTest;

namespace AstFirst.Tests.EndToEnd;

/// <summary>AstNode.Span の自動マージ (子の Span 統合) と OnReduce での手動上書きの E2E 検証。</summary>
public class SpanAutoMergeTests
{
    [Fact]
    public void OnReduceAbsent_AutoMergesAllChildren()
    {
        // OnReduce なしノード: 子 (# と name) の Span を両方マージして "#abc" 全体を覆う。
        var result = SAMRootParser.Parse("#abc");
        Assert.False(result.HasErrors);
        var auto = Assert.IsType<SAMAuto>(result.Ast);
        Assert.Equal(0, auto.Span.Start.Offset);
        Assert.Equal(4, auto.Span.End.Offset);   // "#abc" = [0,4)
    }

    [Fact]
    public void OnReduceManual_OverridesAutoMerge()
    {
        // OnReduce で Name.Span のみ設定: 自動マージ ($ と name 全体 [0,4)) を上書きして [1,4) に。
        var result = SAMRootParser.Parse("$abc");
        Assert.False(result.HasErrors);
        var manual = Assert.IsType<SAMManual>(result.Ast);
        Assert.Equal(1, manual.Span.Start.Offset);
        Assert.Equal(4, manual.Span.End.Offset); // name のみ = [1,4)
    }
}
