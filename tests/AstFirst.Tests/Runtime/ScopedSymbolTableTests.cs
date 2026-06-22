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

    // --- 境界・エッジケース ---

    [Fact]
    public void SiblingScopes_Independent()
    {
        // 兄弟スコープは互いに独立: A で宣言 → Pop → B で同名宣言が可能
        var t = new ScopedSymbolTable();
        t.PushScope();
        t.TryDeclare("x", Span(0), "A", out _);
        t.PopScope();
        t.PushScope(); // 兄弟スコープ
        Assert.True(t.TryDeclare("x", Span(1), "B", out _)); // 同名 OK
        Assert.Equal("B", t.Lookup("x")!.Value);
    }

    [Fact]
    public void DeepNesting_LookupReturnsInnermost()
    {
        // 3層ネスト: 同名が複数スコープにあるとき最内を返す
        var t = new ScopedSymbolTable();
        t.TryDeclare("x", Span(0), "root", out _);
        t.PushScope();
        t.TryDeclare("x", Span(1), "mid", out _);
        t.PushScope();
        t.TryDeclare("x", Span(2), "inner", out _);
        Assert.Equal("inner", t.Lookup("x")!.Value);
        Assert.Equal(2, t.Lookup("x")!.Depth);
    }

    [Fact]
    public void TryDeclare_ExistingHasOriginalSpan()
    {
        // 二重宣言の existing は「先に」宣言された方の位置を返す
        var t = new ScopedSymbolTable();
        t.TryDeclare("x", Span(5), null, out _);
        Assert.False(t.TryDeclare("x", Span(9), null, out var existing));
        Assert.NotNull(existing);
        Assert.Equal(5, existing!.Span.Start.Offset);
    }

    [Fact]
    public void SymbolValue_StoredAndUpdatable()
    {
        // SymbolEntry.Value は格納され、Lookup 経由で更新できる
        var t = new ScopedSymbolTable();
        t.TryDeclare("x", Span(0), 10, out _);
        var sym = t.Lookup("x");
        Assert.Equal(10, sym!.Value);
        sym.Value = 20;
        Assert.Equal(20, t.Lookup("x")!.Value); // 同じ SymbolEntry インスタンス
    }

    [Fact]
    public void Lookup_ReturnsIntermediateScope()
    {
        // 内側に無ければ中間スコープを返す (外側まで一気に飛ばない)
        var t = new ScopedSymbolTable();
        t.TryDeclare("x", Span(0), "root", out _);
        t.PushScope();
        t.PushScope();
        Assert.Equal("root", t.Lookup("x")!.Value); // 内側2層どちらにも無い → root

        t.PopScope();
        t.TryDeclare("x", Span(1), "mid", out _); // 中間スコープに追加
        t.PushScope();
        Assert.Equal("mid", t.Lookup("x")!.Value); // 中間が勝つ (root を飛ばす)
    }

    [Fact]
    public void ScopeSymbols_ContainsOnlyLocal()
    {
        // Scope.Symbols は当該スコープの宣言のみ (外側を含まない)
        var t = new ScopedSymbolTable();
        t.TryDeclare("a", Span(0), null, out _);
        t.PushScope();
        t.TryDeclare("b", Span(1), null, out _);
        var localNames = t.Current.Symbols.Select(s => s.Name).ToArray();
        Assert.Equal(new[] { "b" }, localNames);
    }

    [Fact]
    public void PushPop_CycleReusable()
    {
        // Pop 後に新しく Push したスコープは、前のサイクルの宣言を引き継がない
        var t = new ScopedSymbolTable();
        t.PushScope();
        t.TryDeclare("tmp", Span(0), null, out _);
        t.PopScope();
        Assert.Null(t.Lookup("tmp"));

        t.PushScope();
        Assert.Null(t.Lookup("tmp")); // 前サイクルの残滓なし
        Assert.True(t.TryDeclare("tmp", Span(1), null, out _)); // 同名 OK
    }
}
