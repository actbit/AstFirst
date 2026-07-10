using AstFirst;

namespace AstFirst.Tests.Runtime;

public class SymbolTests
{
    private static readonly TypeSymbol Int = new("int");
    private static readonly SourceSpan Span = new(new Position(0, 1, 1), new Position(0, 1, 1));

    [Fact]
    public void VariableSymbol_HasNameSpanDepthType()
    {
        var v = new VariableSymbol("x", Span, 1, Int);
        Assert.Equal("x", v.Name);
        Assert.Equal(Span, v.Span);
        Assert.Equal(1, v.Depth);
        Assert.Same(Int, v.Type);
    }

    [Fact]
    public void FunctionSymbol_HasNameAndParams()
    {
        var retType = new FunctionTypeSymbol(Int, new[] { Int });
        var f = new FunctionSymbol("f", Span, 0, retType, new[] { new FunctionParam("a", Int) });
        Assert.Equal("f", f.Name);
        Assert.Same(retType, f.Type);
        Assert.Single(f.Parameters);
        Assert.Equal("a", f.Parameters[0].Name);
        Assert.Same(Int, f.Parameters[0].Type);
    }

    [Fact]
    public void SymbolEntry_ImplementsISymbol()
    {
        ISymbol s = new SymbolEntry("x", Span, 1, null);
        Assert.Equal("x", s.Name);
        Assert.Equal(1, s.Depth);
        Assert.Equal(Span, s.Span);
    }

    [Fact]
    public void SymbolEntry_AsVariable_ReturnsStoredSymbol()
    {
        var varSym = new VariableSymbol("x", Span, 1, Int);
        var entry = new SymbolEntry("x", Span, 1, varSym);
        Assert.Same(varSym, entry.AsVariable());
    }

    [Fact]
    public void SymbolEntry_AsFunction_ReturnsStoredSymbol()
    {
        var fnType = new FunctionTypeSymbol(Int, System.Array.Empty<TypeSymbol>());
        var fnSym = new FunctionSymbol("f", Span, 0, fnType, System.Array.Empty<FunctionParam>());
        var entry = new SymbolEntry("f", Span, 0, fnSym);
        Assert.Same(fnSym, entry.AsFunction());
    }

    [Fact]
    public void SymbolEntry_AsVariable_NullWhenNotSet()
    {
        var entry = new SymbolEntry("x", Span, 1, null);
        Assert.Null(entry.AsVariable());
        Assert.Null(entry.AsFunction());
    }

    [Fact]
    public void ScopedSymbolTable_DeclareAndLookup_TypedSymbol()
    {
        var table = new ScopedSymbolTable();
        var varSym = new VariableSymbol("x", Span, 0, Int);
        Assert.True(table.TryDeclare("x", Span, varSym, out _));
        var entry = table.Lookup("x");
        Assert.NotNull(entry);
        Assert.Same(varSym, entry!.AsVariable());
    }

    [Fact]
    public void VariableSymbol_ImplementsISymbol()
    {
        ISymbol s = new VariableSymbol("x", Span, 2, Int);
        Assert.Equal("x", s.Name);
        Assert.Equal(2, s.Depth);
    }
}
