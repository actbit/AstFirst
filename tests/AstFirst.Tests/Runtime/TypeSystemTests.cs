using AstFirst;

namespace AstFirst.Tests.Runtime;

public class TypeSystemTests
{
    private static readonly TypeSymbol Int = new("int");
    private static readonly TypeSymbol Bool = new("bool");
    private static readonly TypeSymbol Animal = new("Animal");
    private static readonly TypeSymbol Dog = new("Dog", Animal); // Dog : Animal

    [Fact]
    public void IsAssignableFrom_SameType_True()
    {
        Assert.True(Int.IsAssignableFrom(Int));
    }

    [Fact]
    public void IsAssignableFrom_Unrelated_False()
    {
        Assert.False(Int.IsAssignableFrom(Bool));
        Assert.False(Bool.IsAssignableFrom(Int));
    }

    [Fact]
    public void IsAssignableFrom_DerivedToBase_True()
    {
        // Animal a = new Dog(); は OK
        Assert.True(Animal.IsAssignableFrom(Dog));
    }

    [Fact]
    public void IsAssignableFrom_BaseToDerived_False()
    {
        // Dog d = new Animal(); は NG
        Assert.False(Dog.IsAssignableFrom(Animal));
    }

    [Fact]
    public void AreCompatible_OneDirection_True()
    {
        Assert.True(TypeSymbol.AreCompatible(Animal, Dog));
        Assert.False(TypeSymbol.AreCompatible(Int, Bool));
    }

    [Fact]
    public void TypeContext_StoreAndRetrieve()
    {
        var node = new TestNode();
        var ctx = new TypeContext();
        Assert.Null(ctx.TypeOf(node));
        Assert.False(ctx.HasType(node));
        ctx.SetType(node, Int);
        Assert.True(ctx.HasType(node));
        Assert.Same(Int, ctx.TypeOf(node));
    }

    [Fact]
    public void TypeContext_Overwrite()
    {
        var node = new TestNode();
        var ctx = new TypeContext();
        ctx.SetType(node, Int);
        ctx.SetType(node, Bool);
        Assert.Same(Bool, ctx.TypeOf(node));
    }

    private sealed class TestNode : AstNode { }
}
