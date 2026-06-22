using AstFirst;

namespace AstFirst.Tests.Runtime;

public class ScopedSymbolTableTests
{
    private static SourceSpan Span(int offset) => new(new Position(offset, 0, 0), new Position(offset, 0, 0));

    [Fact]
    public void DeclareAndLookup_RootScope()
    {
        var t = new ScopedSymbolTable();
        Assert.True(t.TryDeclare("x", Span(0), null, out var existing));
        Assert.Null(existing);
        Assert.NotNull(t.Lookup("x"));
        Assert.Equal("x", t.Lookup("x")!.Name);
        Assert.Equal(0, t.Lookup("x")!.Depth);
    }

    [Fact]
    public void Lookup_Undeclared_ReturnsNull()
    {
        var t = new ScopedSymbolTable();
        Assert.Null(t.Lookup("missing"));
    }

    [Fact]
    public void DuplicateDeclare_SameScope_Fails()
    {
        var t = new ScopedSymbolTable();
        t.TryDeclare("x", Span(0), null, out _);
        Assert.False(t.TryDeclare("x", Span(1), null, out var existing));
        Assert.NotNull(existing);
        Assert.Equal(0, existing!.Span.Start.Offset); // 最初の宣言位置を保持
    }

    [Fact]
    public void PushPop_InnerScopeShadowsOuter()
    {
        var t = new ScopedSymbolTable();
        t.TryDeclare("x", Span(0), 1, out _);
        t.PushScope();
        Assert.True(t.TryDeclare("x", Span(1), 2, out _)); // シャドウイング許可
        Assert.Equal(2, t.Lookup("x")!.Value); // 内側が勝つ
        Assert.Equal(1, t.Lookup("x")!.Depth);
        t.PopScope();
        Assert.Equal(1, t.Lookup("x")!.Value); // 外側に戻る
    }

    [Fact]
    public void Lookup_FindsOuterFromInner()
    {
        var t = new ScopedSymbolTable();
        t.TryDeclare("outer", Span(0), null, out _);
        t.PushScope();
        Assert.NotNull(t.Lookup("outer")); // 内側から外側が見える
    }

    [Fact]
    public void PopScope_Root_NoOp()
    {
        var t = new ScopedSymbolTable();
        t.PopScope(); // ルートでは何もしない
        Assert.Equal(0, t.Current.Depth);
        Assert.Null(t.Current.Parent);
    }

    [Fact]
    public void InnerDeclareNotVisibleInOuter()
    {
        var t = new ScopedSymbolTable();
        t.PushScope();
        t.TryDeclare("inner", Span(0), null, out _);
        t.PopScope();
        Assert.Null(t.Lookup("inner")); // 外側からは見えない
    }

    [Fact]
    public void Depth_TracksNesting()
    {
        var t = new ScopedSymbolTable();
        Assert.Equal(0, t.Current.Depth);
        t.PushScope();
        Assert.Equal(1, t.Current.Depth);
        t.PushScope();
        Assert.Equal(2, t.Current.Depth);
    }
}
