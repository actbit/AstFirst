using AstFirst;

namespace AstFirst.Tests.Runtime;

public class DiagnosticBagTests
{
    private static readonly SourceSpan Span = new(new Position(0, 0, 0), new Position(0, 0, 0));

    [Fact]
    public void HasErrors_Empty_False()
    {
        var bag = new DiagnosticBag();
        Assert.False(bag.HasErrors);
    }

    [Fact]
    public void HasErrors_WarningOnly_False()
    {
        var bag = new DiagnosticBag();
        bag.Warning("注意", Span);
        Assert.False(bag.HasErrors);
    }

    [Fact]
    public void HasErrors_WithError_True()
    {
        var bag = new DiagnosticBag();
        bag.Warning("注意", Span);
        bag.Error("エラー", Span);
        Assert.True(bag.HasErrors);
    }

    [Fact]
    public void Items_Accumulated()
    {
        var bag = new DiagnosticBag();
        bag.Error("e1", Span);
        bag.Warning("w1", Span);
        Assert.Equal(2, bag.Items.Count);
    }

    [Fact]
    public void Items_PreserveInsertionOrder()
    {
        var bag = new DiagnosticBag();
        bag.Error("first", Span);
        bag.Error("second", Span);
        bag.Warning("third", Span);
        Assert.Equal(3, bag.Items.Count);
        Assert.Equal("first", bag.Items[0].Message);
        Assert.Equal("second", bag.Items[1].Message);
        Assert.Equal(Severity.Warning, bag.Items[2].Severity);
    }
}
