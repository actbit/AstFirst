using System.Collections.Generic;
using AstFirst;

namespace AstFirst.Tests.Runtime;

public class OverloadResolverTests
{
    private static readonly TypeSymbol Int = new("int");
    private static readonly TypeSymbol Bool = new("bool");
    private static readonly TypeSymbol Animal = new("Animal");
    private static readonly TypeSymbol Dog = new("Dog", Animal);
    private static readonly SourceSpan Span = new(new Position(0, 1, 1), new Position(0, 1, 1));

    private static FunctionSymbol Func(string name, TypeSymbol ret, params TypeSymbol[] paramTypes)
    {
        var paramList = new List<FunctionParam>();
        foreach (var t in paramTypes)
            paramList.Add(new FunctionParam("p" + paramList.Count, t));
        var fnType = new FunctionTypeSymbol(ret, paramTypes);
        return new FunctionSymbol(name, Span, 0, fnType, paramList);
    }

    [Fact]
    public void Resolve_ExactMatch_Selected()
    {
        var candidates = new List<FunctionSymbol> { Func("f", Int, Int), Func("f", Int, Bool) };
        var result = OverloadResolver.Resolve(candidates, new[] { Int }, new DiagnosticBag(), Span, "f");
        Assert.NotNull(result);
        Assert.Same(candidates[0], result);
    }

    [Fact]
    public void Resolve_ImplicitConversion_Selected()
    {
        // Dog 実引数 → Animal 仮引数 (派生→基底 = Implicit)
        var candidates = new List<FunctionSymbol> { Func("f", Int, Animal) };
        var result = OverloadResolver.Resolve(candidates, new[] { Dog }, new DiagnosticBag(), Span, "f");
        Assert.NotNull(result);
        Assert.Same(candidates[0], result);
    }

    [Fact]
    public void Resolve_NoArityMatch_Diagnostic()
    {
        var bag = new DiagnosticBag();
        var candidates = new List<FunctionSymbol> { Func("f", Int, Int) };
        var result = OverloadResolver.Resolve(candidates, new[] { Int, Int }, bag, Span, "f");
        Assert.Null(result);
        Assert.True(bag.HasErrors);
    }

    [Fact]
    public void Resolve_NoTypeMatch_Diagnostic()
    {
        var bag = new DiagnosticBag();
        var candidates = new List<FunctionSymbol> { Func("f", Int, Int) };
        var result = OverloadResolver.Resolve(candidates, new[] { Bool }, bag, Span, "f");
        Assert.Null(result);
        Assert.True(bag.HasErrors);
    }

    [Fact]
    public void Resolve_Ambiguous_Diagnostic()
    {
        var bag = new DiagnosticBag();
        // Dog 実引数に対し戻り値型の違う (Animal) 候補が2つ → 両方適合で曖昧
        var candidates = new List<FunctionSymbol>
        {
            Func("f", Int, Animal),
            Func("f", Bool, Animal),
        };
        var result = OverloadResolver.Resolve(candidates, new[] { Dog }, bag, Span, "f");
        Assert.Null(result);
        Assert.True(bag.HasErrors);
    }

    [Fact]
    public void Resolve_EmptyCandidates_Diagnostic()
    {
        var bag = new DiagnosticBag();
        var result = OverloadResolver.Resolve(new List<FunctionSymbol>(), new[] { Int }, bag, Span, "f");
        Assert.Null(result);
        Assert.True(bag.HasErrors);
    }
}
