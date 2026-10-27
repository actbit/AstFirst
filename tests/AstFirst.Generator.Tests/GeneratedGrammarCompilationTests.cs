using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AstFirst;
using AstFirst.Core.Parsing;
using AstFirst.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AstFirst.Tests.Generator;

public sealed class GeneratedGrammarCompilationTests
{
    private static IReadOnlyList<Microsoft.CodeAnalysis.Diagnostic> Generate(string source, out Compilation output)
    {
        var trusted = (string)System.AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        var refs = trusted.Split(Path.PathSeparator)
            .Where(p => !p.EndsWith("AstFirst.Generator.dll", StringComparison.OrdinalIgnoreCase)
                     && !p.EndsWith("AstFirst.Runtime.dll", StringComparison.OrdinalIgnoreCase)
                     && !p.EndsWith("AstFirst.Core.dll", StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList();
        refs.Add(MetadataReference.CreateFromFile(typeof(AstNode).Assembly.Location));
        refs.Add(MetadataReference.CreateFromFile(
            Path.Combine(AppContext.BaseDirectory, "AstFirst.Core.dll")));

        var input = CSharpCompilation.Create(
            "GeneratedGrammarCompilation",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ParserGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(input, out output, out var generatorDiagnostics);
        return output.GetDiagnostics()
            .Concat(generatorDiagnostics)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
    }

    [Fact]
    public void TypeHierarchyGrammarWithCrossNamespaceNodeCompilesGeneratedCode()
    {
        var source = @"
using AstFirst;
namespace GrammarNs
{
    [Grammar(Discovery = GrammarDiscovery.TypeHierarchy)]
    public abstract partial class Expr : AstNode { }
}
namespace Nodes
{
    public sealed partial class Number : GrammarNs.Expr
    {
        [Rule] public static void Reduce([Token(@""[0-9]+"")] Token value) { }
    }
}
";

        var errors = Generate(source, out _);

        Assert.Empty(errors);
    }

    [Fact]
    public void MultipleGrammarModesCompileWithOneSharedNodePartial()
    {
        var source = @"
using AstFirst;
[Grammar(Mode = ""A"")]
[Grammar(Mode = ""B"")]
public abstract partial class Expr : AstNode { }
public sealed partial class Number : Expr
{
    [Rule] public static void Reduce([Token(@""[0-9]+"")] Token value) { }
}
";

        var errors = Generate(source, out _);

        Assert.Empty(errors);
    }

    [Fact]
    public void SameSimpleNodeNamesInDifferentNamespacesCompileGeneratedCode()
    {
        var source = @"
using AstFirst;
namespace First
{
    [Grammar]
    public abstract partial class Expr : AstNode { }
    public sealed partial class Number : Expr
    {
        [Rule] public static void Reduce([Token(""1"")] Token value) { }
    }
}
namespace Second
{
    [Grammar]
    public abstract partial class Expr : AstNode { }
    public sealed partial class Number : Expr
    {
        [Rule] public static void Reduce([Token(""2"")] Token value) { }
    }
}
";

        var errors = Generate(source, out _);

        Assert.Empty(errors);
    }

    [Fact]
    public void SharedGrammarPartUsedByTwoRootsCompilesGeneratedCode()
    {
        var source = @"
using AstFirst;
namespace First
{
    [Grammar]
    public abstract partial class Root : AstNode
    {
        [OnReduce] public static void Analyze(Shared.Value node, SemanticContext ctx) { }
    }
}
namespace Second
{
    [Grammar]
    public abstract partial class Root : AstNode
    {
        [OnReduce] public static void Analyze(Shared.Value node, SemanticContext ctx) { }
    }
}
namespace Shared
{
    [GrammarPart(typeof(First.Root))]
    [GrammarPart(typeof(Second.Root))]
    public sealed partial class Value : AstNode
    {
        [Rule] public static void Reduce([Token(""value"")] Token value, SemanticContext ctx) { }
    }
}
";

        var errors = Generate(source, out _);

        Assert.Empty(errors);
    }

    [Fact]
    public void GeneratedPropertiesAvoidReservedAndPascalCaseCollisions()
    {
        var source = @"
using AstFirst;
[Grammar]
public abstract partial class Root : AstNode { }
public sealed partial class Pair : Root
{
    public string A { get; } = ""existing"";
    [Rule] public static void Reduce(
        [Token(""a"")] Token a,
        [Token(""b"")] Token A,
        [Token(""c"")] Token span,
        [Token(""d"")] Token ruleName) { }
}
";

        var errors = Generate(source, out _);

        Assert.Empty(errors);
    }

    [Fact]
    public void MultipleRulesWithTheSameConstructorSignaturePreserveRuleSpecificProperties()
    {
        var source = @"
using AstFirst;
[Grammar]
public abstract partial class Root : AstNode { }
public sealed partial class Choice : Root
{
    [Rule] public static void First([Token(""a"")] Token first) { }
    [Rule] public static void Second([Token(""b"")] Token second) { }
}
";

        var errors = Generate(source, out var output);

        Assert.Empty(errors);
        var generated = output.SyntaxTrees.Select(tree => tree.ToString()).Single(text => text.Contains("this.First_ = __astFirstArg0;"));
        Assert.Contains("if (__astFirstRuleName == \"First\")", generated);
        Assert.Contains("this.First_ = __astFirstArg0;", generated);
        Assert.Contains("this.Second_ = __astFirstArg0;", generated);
    }

    [Fact]
    public void SameParameterNameWithDifferentTypesGetsDistinctProperties()
    {
        var source = @"
using AstFirst;
[Grammar]
public abstract partial class Root : AstNode { }
public sealed partial class Child : Root
{
    [Rule] public static void Reduce([Token(""c"")] Token token) { }
}
public sealed partial class Choice : Root
{
    [Rule] public static void Node(Child value) { }
    [Rule] public static void Token([Token(""t"")] Token value) { }
}
";

        var errors = Generate(source, out _);

        Assert.Empty(errors);
    }

    [Fact]
    public void InheritedConstructorArgumentsKeepBaseRuleOrder()
    {
        var source = @"
using AstFirst;
[Grammar(Discovery = GrammarDiscovery.TypeHierarchy)]
public abstract partial class Root : AstNode { }
public abstract partial class Base : Root
{
    [Rule] public static void BaseRule(BNode b, ANode a) { }
}
public sealed partial class Derived : Base
{
    [Rule] public static void DerivedRule(BNode b, ANode a) { }
}
public sealed partial class ANode : Root
{
    [Rule] public static void A([Token(""a"")] Token token) { }
}
public sealed partial class BNode : Root
{
    [Rule] public static void B([Token(""b"")] Token token) { }
}
";

        var errors = Generate(source, out _);

        Assert.Empty(errors);
    }

    [Fact]
    public void ModeNamesWithPunctuationStillProduceValidIdentifiers()
    {
        var source = @"
using AstFirst;
[Grammar(Mode = ""v1-beta"")]
public abstract partial class Root : AstNode { }
public sealed partial class Number : Root
{
    [Rule] public static void Reduce([Token(""[0-9]+"")] Token value) { }
}
";

        var errors = Generate(source, out _);

        Assert.Empty(errors);
    }
}
