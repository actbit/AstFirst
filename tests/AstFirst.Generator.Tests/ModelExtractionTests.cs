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

[Grammar] public abstract partial class Expr : AstNode { }

public sealed partial class NumExpr : Expr
{
    [Rule] public static void Num([Pattern(""[0-9]+"")] Token num) { }
}

public sealed partial class AddExpr : Expr
{
    [Rule] public static void Add(Expr left, [Pattern(""\\+"")] Token op, Expr right) { }
}

// Token 派生型を [Rule] 引数で使うと Key=型名 で TokenDef 抽出 (TokenDerivedPatternCollected 用)。
public sealed class NumToken : Token
{
    public NumToken(string text) : base(text, default) { }
}
public sealed partial class NumTokenExpr : Expr
{
    [Rule] public static void NumTokenRule([Pattern(""[0-9]+"")] NumToken num) { }
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
        var ctor = Assert.Single(add.Rules);
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

    [Fact]
    public void ModelEqualityIncludesIncrementalGeneratorInputs()
    {
        var nodes = new List<NodeModel>();
        var tokens = new List<TokenDefModel>();
        var baseline = new GrammarModel("Expr", nodes, tokens);

        Assert.NotEqual(baseline, new GrammarModel("Expr", nodes, tokens, skipPatterns: new[] { "\\s+" }));
        Assert.NotEqual(baseline, new GrammarModel("Expr", nodes, tokens, mode: "V2"));
        Assert.NotEqual(baseline, new GrammarModel("Expr", nodes, tokens,
            tokenDerivedWarnings: new[] { "MissingStringCtorToken" }));
        Assert.NotEqual(baseline, new GrammarModel("Expr", nodes, tokens,
            discovery: AstFirst.Generator.GrammarDiscovery.TypeHierarchy));
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
    public void AssemblySkipPatternCollected()
    {
        var source = @"
using AstFirst;
[assembly: Skip(""//[^\\n]*"")]
[Grammar]
public abstract class S : AstNode { }
";
        var model = ExtractSource(source, "S");
        Assert.Contains("//[^\\n]*", model.SkipPatterns);
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

    [Fact]
    public void DefaultDiscoveryIncludesRootDescendantsFromOtherNamespaces()
    {
        var source = @"
using AstFirst;
namespace RootNs { [Grammar] public abstract partial class Expr : AstNode { } }
namespace NodeNs {
    public sealed partial class Num : RootNs.Expr {
        [Rule] public static void Reduce([Token(""[0-9]+"")] Token value) { }
    }
}
";
        var model = ExtractSource(source, "RootNs.Expr");
        Assert.Contains(model.Nodes, n => n.FullName == "NodeNs.Num");
        Assert.Equal(AstFirst.Generator.GrammarDiscovery.NamespaceAndTypeHierarchy, model.Discovery);
    }

    [Fact]
    public void TypeHierarchyDiscoveryDoesNotScanTheNamespace()
    {
        var source = @"
using AstFirst;
namespace Shared {
    [Grammar(Discovery = AstFirst.GrammarDiscovery.TypeHierarchy)]
    public abstract partial class Expr : AstNode { }
    public sealed partial class Unrelated : AstNode {
        [Rule] public static void Reduce([Token(""x"")] Token value) { }
    }
}
namespace Nodes {
    public sealed partial class Num : Shared.Expr {
        [Rule] public static void Reduce([Token(""[0-9]+"")] Token value) { }
    }
}
";
        var model = ExtractSource(source, "Shared.Expr");
        Assert.Contains(model.Nodes, n => n.FullName == "Nodes.Num");
        Assert.DoesNotContain(model.Nodes, n => n.FullName == "Shared.Unrelated");
        Assert.Equal(AstFirst.Generator.GrammarDiscovery.TypeHierarchy, model.Discovery);
    }

    [Fact]
    public void GrammarPartCanIncludeANodeOutsideNamespaceAndHierarchy()
    {
        var source = @"
using AstFirst;
namespace RootNs {
    [Grammar(Discovery = AstFirst.GrammarDiscovery.TypeHierarchy)]
    public abstract partial class Expr : AstNode { }
}
namespace SharedNodes {
    [GrammarPart(typeof(RootNs.Expr))]
    public sealed partial class SharedValue : AstNode {
        [Rule] public static void Reduce([Token(""value"")] Token value) { }
    }
}
";
        var model = ExtractSource(source, "RootNs.Expr");
        Assert.Contains(model.Nodes, n => n.FullName == "SharedNodes.SharedValue");
    }

    [Fact]
    public void UnrelatedGrammarPartAttributeIsIgnored()
    {
        var source = @"
using AstFirst;
namespace Other {
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class GrammarPartAttribute : System.Attribute {
        public GrammarPartAttribute(System.Type grammarRoot) { }
    }
}
namespace RootNs {
    [Grammar(Discovery = AstFirst.GrammarDiscovery.TypeHierarchy)]
    public abstract partial class Expr : AstNode { }
}
namespace SharedNodes {
    [Other.GrammarPart(typeof(RootNs.Expr))]
    public sealed partial class SharedValue : AstNode {
        [Rule] public static void Reduce([Token(""value"")] Token value) { }
    }
}
";

        var model = ExtractSource(source, "RootNs.Expr");

        Assert.DoesNotContain(model.Nodes, n => n.FullName == "SharedNodes.SharedValue");
    }

    [Fact]
    public void UnrelatedGrammarAndAssemblySkipAttributesAreIgnored()
    {
        var source = @"
using AstFirst;
[assembly: Other.Skip(""not-a-lexer-pattern"")]
namespace Other {
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class GrammarAttribute : System.Attribute {
        public string Mode { get; set; } = """";
    }
    [System.AttributeUsage(System.AttributeTargets.Assembly)]
    public sealed class SkipAttribute : System.Attribute {
        public SkipAttribute(string value) { }
    }
}
[AstFirst.Grammar(Mode = ""Real"")]
[Other.Grammar(Mode = ""Fake"")]
public abstract partial class Expr : AstFirst.AstNode { }
";

        var model = ExtractSource(source, "Expr");

        Assert.Equal("Real", model.Mode);
        Assert.Empty(model.SkipPatterns);
    }

    [Fact]
    public void NamespaceDiscoveryRetainsTheLegacyBoundary()
    {
        var source = @"
using AstFirst;
namespace RootNs {
    [Grammar(Discovery = AstFirst.GrammarDiscovery.Namespace)]
    public abstract partial class Expr : AstNode { }
}
namespace NodeNs {
    public sealed partial class Num : RootNs.Expr {
        [Rule] public static void Reduce([Token(""[0-9]+"")] Token value) { }
    }
}
";
        var model = ExtractSource(source, "RootNs.Expr");
        Assert.DoesNotContain(model.Nodes, n => n.FullName == "NodeNs.Num");
    }
}
