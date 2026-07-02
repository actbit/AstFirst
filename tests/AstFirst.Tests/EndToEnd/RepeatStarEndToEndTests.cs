using AstFirst.Tests.EndToEnd.RepeatStarTest;

namespace AstFirst.Tests.EndToEnd;

/// <summary>[Repeat(Min=0)] (0回以上=Star) の E2E 検証。空リストの受理と要素ありリストの両方。</summary>
public class RepeatStarEndToEndTests
{
    [Fact]
    public void EmptyInputProducesEmptyList()
    {
        // 空入力 → RSProgramBody { Items = [] } (ε 規則で空リスト)。
        var result = RSProgramParser.Parse("");
        Assert.False(result.HasErrors);
        var body = Assert.IsType<RSProgramBody>(result.Ast);
        Assert.Empty(body.Items);
        // 空リストは子を持たないため Span は自動計算されず、default (IsEmpty) のまま。
        Assert.True(body.Span.IsEmpty);
    }

    [Fact]
    public void MultipleItemsProduceList()
    {
        var result = RSProgramParser.Parse("a b c");
        Assert.False(result.HasErrors);
        var body = Assert.IsType<RSProgramBody>(result.Ast);
        Assert.Equal(3, body.Items.Count);
    }

    [Fact]
    public void SingleItemProducesListOfOne()
    {
        var result = RSProgramParser.Parse("a");
        Assert.False(result.HasErrors);
        var body = Assert.IsType<RSProgramBody>(result.Ast);
        Assert.Single(body.Items);
    }
}
