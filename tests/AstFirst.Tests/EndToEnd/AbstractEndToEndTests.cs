using AstFirst.Tests.EndToEnd.AbstractTest;

namespace AstFirst.Tests.EndToEnd;

/// <summary>中間抽象クラス (プロパティ継承 + 単位規則) の E2E 検証。</summary>
public class AbstractEndToEndTests
{
    [Fact]
    public void ParsesThroughIntermediateAbstract()
    {
        // "1+2" → AAdd(ANum(1), ANum(2))。AAdd.Left/Right は基底 ABinary のプロパティ (継承)。
        // ANode → ABinary (単位規則・値をそのまま渡す) → AAdd と到達する。
        var result = ANodeParser.Parse("1+2");
        Assert.False(result.HasErrors);
        var add = Assert.IsType<AAdd>(result.Ast);
        Assert.IsType<ANum>(add.Left);
        Assert.IsType<ANum>(add.Right);
        // OnReduce を持たないノードでも、子の Span から自動計算されて "1+2" 全体を覆う。
        Assert.Equal(0, add.Span.Start.Offset);
        Assert.Equal(3, add.Span.End.Offset);
    }

    [Fact]
    public void ParsesLeafWithoutBinary()
    {
        // "1" → ANum(Num)。単位規則を経由しない直接の規則。
        var result = ANodeParser.Parse("1");
        Assert.False(result.HasErrors);
        Assert.IsType<ANum>(result.Ast);
    }

    [Fact]
    public void InheritedPropertyIsAccessible()
    {
        // AAdd.Left/Right は基底 ABinary で定義 (継承)。AAdd 自体にはフィールドがない。
        var result = ANodeParser.Parse("3+4");
        Assert.False(result.HasErrors);
        var add = Assert.IsType<AAdd>(result.Ast);
        // Op は AAdd 自身のプロパティ。Left/Right は基底 ABinary のプロパティ。
        Assert.NotNull(add.Op);
        Assert.NotNull(add.Left);
        Assert.NotNull(add.Right);
    }
}
