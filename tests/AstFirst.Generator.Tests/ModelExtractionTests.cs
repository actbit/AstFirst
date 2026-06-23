using System.Linq;
using AstFirst;
using AstFirst.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AstFirst.Tests.Generator;

public class ModelExtractionTests
{
    private static Compilation CreateCompilation(string source)
    {
        // フレームワーク既定アセンブリを全て参照に (System.Runtime 等の CS0012 を回避)。
        var trusted = (string)System.AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        var refs = trusted.Split(System.IO.Path.PathSeparator)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
        refs.Add(MetadataReference.CreateFromFile(typeof(AstNode).Assembly.Location)); // AstFirst.Runtime

        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private const string CalcSource = @"
using AstFirst;

[Grammar] public abstract class Expr : AstNode { }

public sealed class NumExpr : Expr
{
    public NumExpr([Pattern(""[0-9]+"")] Token num) { Span = num.Span; }
}

public sealed class AddExpr : Expr
{
    public AddExpr(Expr left, [Pattern(""\\+"")] Token op, Expr right) { Span = left.Span; }
}

public sealed class NumToken : Token
{
    public NumToken([Pattern(""[0-9]+"")] string text) : base(text, default) { }
}
";

    private static GrammarModel Extract()
    {
        var comp = CreateCompilation(CalcSource);
        var errors = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.False(errors.Count > 0, "コンパイルエラー:\n" + string.Join("\n", errors.Select(e => e.ToString())));
        var root = comp.GetTypeByMetadataName("Expr");
        return ModelExtraction.Extract(comp, root!);
    }

    [Fact]
    public void RootTypeFullNameExtracted()
    {
        var model = Extract();
        Assert.Equal("Expr", model.RootTypeFullName);
    }

    [Fact]
    public void AstNodeDerivedClassesCollected()
    {
        var model = Extract();
        Assert.Contains(model.Nodes, n => n.FullName == "NumExpr");
        Assert.Contains(model.Nodes, n => n.FullName == "AddExpr");
        Assert.Contains(model.Nodes, n => n.FullName == "Expr");
    }

    [Fact]
    public void NodeBaseTypeRecorded()
    {
        var model = Extract();
        var num = model.Nodes.First(n => n.FullName == "NumExpr");
        Assert.Equal("Expr", num.BaseFullName);
        Assert.False(num.IsAbstract);
        Assert.True(model.Nodes.First(n => n.FullName == "Expr").IsAbstract);
    }

    [Fact]
    public void ConstructorParametersRecorded()
    {
        var model = Extract();
        var add = model.Nodes.First(n => n.FullName == "AddExpr");
        var ctor = Assert.Single(add.Constructors);
        Assert.Equal(3, ctor.Parameters.Count);
        Assert.Equal("Expr", ctor.Parameters[0].TypeFullName);
        Assert.Null(ctor.Parameters[0].Pattern);
        Assert.Equal("\\+", ctor.Parameters[1].Pattern);
    }

    [Fact]
    public void TokenDerivedPatternCollected()
    {
        var model = Extract();
        // NumToken 派生クラスの [Pattern]
        Assert.Contains(model.TokenDefs, t => t.Key == "NumToken" && t.Pattern == "[0-9]+");
    }

    [Fact]
    public void InlineTokenPatternCollected()
    {
        var model = Extract();
        // AST クラスの [Pattern] (共通 Token 型) は Key=AstFirst.Token
        Assert.Contains(model.TokenDefs, t => t.Pattern == "[0-9]+" && t.Key == "AstFirst.Token");
    }

    [Fact]
    public void ModelIsEquatable()
    {
        var a = Extract();
        var b = Extract();
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    private static GrammarModel ExtractSource(string source, string rootName)
    {
        var comp = CreateCompilation(source);
        var errors = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.False(errors.Count > 0, "コンパイルエラー:\n" + string.Join("\n", errors.Select(e => e.ToString())));
        var root = comp.GetTypeByMetadataName(rootName);
        return ModelExtraction.Extract(comp, root!);
    }

    [Fact]
    public void SkipPatternCollected()
    {
        // [Skip(@"\s+")] は GrammarModel.SkipPatterns に収集される。
        var source = @"
using AstFirst;
[Grammar]
[Skip(""\\s+"")]
public abstract class S : AstNode { }
public sealed class A : S { public A([Pattern(""a"")] Token t) { } }
";
        var model = ExtractSource(source, "S");
        Assert.Contains(model.SkipPatterns, p => p == "\\s+");
    }

    [Fact]
    public void ModeExtracted()
    {
        // [Grammar(Mode = "V2")] は GrammarModel.Mode に抽出される。
        var source = @"
using AstFirst;
[Grammar(Mode = ""V2"")]
public abstract class M : AstNode { }
public sealed class A : M { public A([Pattern(""a"")] Token t) { } }
";
        var model = ExtractSource(source, "M");
        Assert.Equal("V2", model.Mode);
    }

    [Fact]
    public void ModeDefaultsToNull()
    {
        // Mode 未指定時は null。
        var model = Extract();
        Assert.Null(model.Mode);
    }
}
