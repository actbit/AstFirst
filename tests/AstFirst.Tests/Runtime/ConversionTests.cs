using AstFirst;

namespace AstFirst.Tests.Runtime;

public class ConversionTests
{
    private static readonly TypeSymbol Int = new("int");
    private static readonly TypeSymbol Bool = new("bool");
    private static readonly TypeSymbol Animal = new("Animal");
    private static readonly TypeSymbol Dog = new("Dog", Animal);

    [Fact]
    public void ClassifyConversion_Identity()
    {
        Assert.Equal(ConversionKind.Identity, Int.ClassifyConversion(Int));
    }

    [Fact]
    public void ClassifyConversion_DerivedToBase_Implicit()
    {
        Assert.Equal(ConversionKind.Implicit, Dog.ClassifyConversion(Animal));
    }

    [Fact]
    public void ClassifyConversion_BaseToDerived_None()
    {
        Assert.Equal(ConversionKind.None, Animal.ClassifyConversion(Dog));
    }

    [Fact]
    public void ClassifyConversion_Unrelated_None()
    {
        Assert.Equal(ConversionKind.None, Int.ClassifyConversion(Bool));
    }

    [Fact]
    public void IsImplicitlyConvertible_Identity_True()
    {
        Assert.True(Int.IsImplicitlyConvertible(Int));
    }

    [Fact]
    public void IsImplicitlyConvertible_DerivedToBase_True()
    {
        Assert.True(Dog.IsImplicitlyConvertible(Animal));
    }

    [Fact]
    public void IsImplicitlyConvertible_Unrelated_False()
    {
        Assert.False(Int.IsImplicitlyConvertible(Bool));
    }
}
