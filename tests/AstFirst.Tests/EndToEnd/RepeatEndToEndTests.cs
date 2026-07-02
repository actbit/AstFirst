using AstFirst.Tests.EndToEnd.RepeatTest;

namespace AstFirst.Tests.EndToEnd;

/// <summary>[Repeat] 属性によるリスト表現 (List_T → item | List_T item) の E2E 検証。</summary>
public class RepeatEndToEndTests
{
    [Fact]
    public void ParsesRepeatedItemsAsList()
    {
        // "a b c" → RProgramBody { Items = [RItem, RItem, RItem] }
        var result = RProgramParser.Parse("a b c");
        Assert.False(result.HasErrors);
        var body = Assert.IsType<RProgramBody>(result.Ast);
        Assert.Equal(3, body.Items.Count);
        // [Repeat] 子の各要素の Span をマージして "a b c" 全体を覆う。
        Assert.Equal(0, body.Span.Start.Offset);
        Assert.Equal(5, body.Span.End.Offset);
    }

    [Fact]
    public void SingleItemIsListOfOne()
    {
        // [Repeat] は1回以上 (Plus) なので、1要素でもリストになる。
        var result = RProgramParser.Parse("a");
        Assert.False(result.HasErrors);
        var body = Assert.IsType<RProgramBody>(result.Ast);
        Assert.Single(body.Items);
    }

    [Fact]
    public void EmptyInputIsRejected()
    {
        // 1回以上なので空入力は受理されない。
        var result = RProgramParser.Parse("");
        Assert.True(result.HasErrors);
    }
}
