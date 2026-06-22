using AstFirst;

namespace AstFirst.Tests.Runtime;

public class AstNodeTests
{
    private sealed class TestNode : AstNode { }
    private sealed class Marker { public int V { get; set; } }

    [Fact]
    public void SetGetAnnotation_RoundTrip()
    {
        var n = new TestNode();
        var m = new Marker { V = 42 };
        n.SetAnnotation("symbol", m);
        var got = n.GetAnnotation<Marker>("symbol");
        Assert.NotNull(got);
        Assert.Equal(42, got!.V);
    }

    [Fact]
    public void GetAnnotation_NotSet_ReturnsNull()
    {
        var n = new TestNode();
        Assert.Null(n.GetAnnotation<Marker>("anything"));
    }

    [Fact]
    public void GetAnnotation_TypeMismatch_ReturnsNull()
    {
        var n = new TestNode();
        n.SetAnnotation("k", "string value");
        Assert.Null(n.GetAnnotation<Marker>("k"));
    }

    [Fact]
    public void SetAnnotation_Overwrite()
    {
        var n = new TestNode();
        n.SetAnnotation("k", new Marker { V = 1 });
        n.SetAnnotation("k", new Marker { V = 2 });
        Assert.Equal(2, n.GetAnnotation<Marker>("k")!.V);
    }
}
