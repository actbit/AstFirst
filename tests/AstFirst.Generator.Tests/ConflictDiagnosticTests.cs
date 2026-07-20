using System.Collections.Generic;
using System.IO;
using System.Linq;
using AstFirst.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AstFirst.Tests.Generator;

/// <summary>
/// Generator を CSharpGeneratorDriver で実際に駆動し、コンフリクト警告 (ASTF001) の
/// 報告を検証する。曖昧な文法で警告が出ること、優先度で解決した文法で出ないこと。
/// </summary>
public class ConflictDiagnosticTests
{
    // Generator が [Grammar]/AstNode/Token/SemanticContext を解決するための最小スタブ。
    private const string Stubs = @"
namespace AstFirst {
    public abstract class AstNode { }
    public abstract class Token { }
    public abstract class SemanticContext { }
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
    public class GrammarAttribute : System.Attribute { public string Mode { get; set; } }
    [System.AttributeUsage(System.AttributeTargets.Parameter)]
    public class PatternAttribute : System.Attribute { public PatternAttribute(string regex) {} }
    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
    public class GrammarPartAttribute : System.Attribute { public GrammarPartAttribute(System.Type root) {} }
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class PrecedenceAttribute : System.Attribute { public PrecedenceAttribute(int priority) {} }
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class RuleAttribute : System.Attribute { }
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class OnReduceAttribute : System.Attribute { }
}
";

    private static GeneratorDriverRunResult RunGeneratorResult(string grammar)
    {
        var trusted = (string)System.AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        var refs = trusted.Split(Path.PathSeparator)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
        var compilation = CSharpCompilation.Create("ConflictTest",
            new[] { CSharpSyntaxTree.ParseText(Stubs + grammar) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        CSharpGeneratorDriver driver = CSharpGeneratorDriver.Create(new ParserGenerator());
        driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);
        return driver.GetRunResult();
    }

    private static IReadOnlyList<Microsoft.CodeAnalysis.Diagnostic> RunGenerator(string grammar)
        => RunGeneratorResult(grammar).Diagnostics;

    [Fact]
    public void AmbiguousGrammarEmitsAstf001()
    {
        // A と B が同じ右辺 (num 1つ) → reduce-reduce コンフリクト → ASTF001 警告。
        // (shift-reduce は bison 互換に shift 優先で解決されるため、reduce-reduce で検証する。)
        var grammar = @"
[AstFirst.Grammar]
public class RootExpr : AstFirst.AstNode { }
public class A : RootExpr {
    [AstFirst.Rule] public static void Reduce([AstFirst.Pattern(""[0-9]+"")] AstFirst.Token n) { }
}
public class B : RootExpr {
    [AstFirst.Rule] public static void Reduce([AstFirst.Pattern(""[0-9]+"")] AstFirst.Token n) { }
}
";
        var diagnostics = RunGenerator(grammar);
        Assert.Contains(diagnostics, d => d.Id == "ASTF001");
    }

    [Fact]
    public void SoundGrammarEmitsNoAstf001()
    {
        // Add に [Precedence(1)] → 優先度/結合性でコンフリクト解決 → ASTF001 なし。
        var grammar = @"
[AstFirst.Grammar]
public class RootExpr : AstFirst.AstNode { }
public class Num : RootExpr {
    public Num([AstFirst.Pattern(""[0-9]+"")] AstFirst.Token n) { }
}
[AstFirst.Precedence(1)]
public class Add : RootExpr {
    public Add(RootExpr l, [AstFirst.Pattern(""\\+"")] AstFirst.Token op, RootExpr r) { }
}
";
        var diagnostics = RunGenerator(grammar);
        Assert.DoesNotContain(diagnostics, d => d.Id == "ASTF001");
    }

    [Fact]
    public void UnreachableNonTerminalEmitsAstf002()
    {
        // Unused は [Pattern] 付きで規則を持つが RootExpr から到達不能 → ASTF002。
        var grammar = @"
[AstFirst.Grammar]
public class RootExpr : AstFirst.AstNode { }
public class Num : RootExpr {
    [AstFirst.Rule] public static void Reduce([AstFirst.Pattern(""[0-9]+"")] AstFirst.Token n) { }
}
public class Unused : AstFirst.AstNode {
    [AstFirst.Rule] public static void Reduce([AstFirst.Pattern(""x"")] AstFirst.Token n) { }
}
";
        var diagnostics = RunGenerator(grammar);
        Assert.Contains(diagnostics, d => d.Id == "ASTF002");
    }

    [Fact]
    public void UndefinedNonTerminalEmitsAstf003()
    {
        // A(B) で B は abstract・具象サブクラスなし → B は参照されるが規則がない → ASTF003。
        // (直前の ModelToGrammar 規則欠落バグと同種を Core 段階で検出する仕組み)
        var grammar = @"
[AstFirst.Grammar]
public abstract class Root : AstFirst.AstNode { }
public class A : Root {
    [AstFirst.Rule] public static void Reduce(B b) { }
}
public abstract class B : AstFirst.AstNode { }
";
        var diagnostics = RunGenerator(grammar);
        Assert.Contains(diagnostics, d => d.Id == "ASTF003");
    }

    [Fact]
    public void TokenDerivedWithoutStringCtorEmitsAstf004()
    {
        // IntToken は [Pattern] int を取り (string) コンストラクタがない →
        // G7 (new IntToken(token.Text)) の生成でコンパイルエラーになる → ASTF004。
        var grammar = @"
[AstFirst.Grammar]
public class RootExpr : AstFirst.AstNode { }
public class Num : RootExpr {
    [AstFirst.Rule] public static void Reduce([AstFirst.Pattern(""[0-9]+"")] IntToken n) { }
}
public class IntToken : AstFirst.Token {
    public IntToken([AstFirst.Pattern(""[0-9]+"")] int n) { }
}
";
        var diagnostics = RunGenerator(grammar);
        Assert.Contains(diagnostics, d => d.Id == "ASTF004");
    }

    [Fact]
    public void TokenDerivedWithProtectedStringCtorEmitsAstf004()
    {
        var grammar = @"
[AstFirst.Grammar]
public class RootExpr : AstFirst.AstNode { }
public class Num : RootExpr {
    [AstFirst.Rule] public static void Reduce([AstFirst.Pattern(""[0-9]+"")] ProtectedToken n) { }
}
public class ProtectedToken : AstFirst.Token {
    protected ProtectedToken(string text) { }
}
";

        var diagnostics = RunGenerator(grammar);

        Assert.Contains(diagnostics, d => d.Id == "ASTF004");
    }

    [Fact]
    public void SameSimpleNodeNameInDifferentNamespacesUsesUniqueHintNames()
    {
        var grammar = @"
namespace RootNs {
    [AstFirst.Grammar]
    public abstract partial class Expr : AstFirst.AstNode { }
}
namespace FirstNs {
    public sealed partial class Number : RootNs.Expr {
        [AstFirst.Rule] public static void Reduce([AstFirst.Pattern(""1"")] AstFirst.Token n) { }
    }
}
namespace SecondNs {
    public sealed partial class Number : RootNs.Expr {
        [AstFirst.Rule] public static void Reduce([AstFirst.Pattern(""2"")] AstFirst.Token n) { }
    }
}
";

        var result = RunGeneratorResult(grammar);

        Assert.DoesNotContain(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        var hintNames = result.Results.Single().GeneratedSources.Select(s => s.HintName).ToList();
        Assert.Equal(hintNames.Count, hintNames.Distinct().Count());
        Assert.Contains(hintNames, name => name.Contains("FirstNs_Number"));
        Assert.Contains(hintNames, name => name.Contains("SecondNs_Number"));
    }

    [Fact]
    public void SameSimpleRootNameInDifferentNamespacesUsesUniqueHintNames()
    {
        var grammar = @"
namespace FirstNs {
    [AstFirst.Grammar]
    public abstract partial class Expr : AstFirst.AstNode { }
}
namespace SecondNs {
    [AstFirst.Grammar]
    public abstract partial class Expr : AstFirst.AstNode { }
}
";

        var result = RunGeneratorResult(grammar);
        var generated = result.Results.Single().GeneratedSources;

        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "CS8785");
        Assert.Equal(4, generated.Length);
        Assert.Equal(generated.Length, generated.Select(source => source.HintName).Distinct().Count());
    }

    [Fact]
    public void MultipleGrammarModesGenerateEachParserWithoutDuplicatePartials()
    {
        var grammar = @"
[AstFirst.Grammar(Mode = ""A"")]
[AstFirst.Grammar(Mode = ""B"")]
public abstract partial class Expr : AstFirst.AstNode { }
public sealed partial class Number : Expr {
    [AstFirst.Rule] public static void Reduce([AstFirst.Pattern(""[0-9]+"")] AstFirst.Token n) { }
}
";

        var result = RunGeneratorResult(grammar);
        var generated = result.Results.Single().GeneratedSources;
        var sourceText = generated.Select(source => source.SourceText.ToString()).ToList();

        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "CS8785");
        Assert.Contains(sourceText, source => source.Contains("class Expr_ALexer"));
        Assert.Contains(sourceText, source => source.Contains("class Expr_AParser"));
        Assert.Contains(sourceText, source => source.Contains("class Expr_BLexer"));
        Assert.Contains(sourceText, source => source.Contains("class Expr_BParser"));
        Assert.Single(generated.Where(source => source.SourceText.ToString().Contains("partial class Number")));
    }

    [Fact]
    public void GrammarPartSharedByTwoRootsEmitsOnePartialAndGrammarSpecificHooks()
    {
        var grammar = @"
namespace FirstNs {
    [AstFirst.Grammar]
    public abstract partial class Root : AstFirst.AstNode {
        [AstFirst.OnReduce] public static void Analyze(Shared.Value node, AstFirst.SemanticContext ctx) { }
    }
}
namespace SecondNs {
    [AstFirst.Grammar]
    public abstract partial class Root : AstFirst.AstNode {
        [AstFirst.OnReduce] public static void Analyze(Shared.Value node, AstFirst.SemanticContext ctx) { }
    }
}
namespace Shared {
    [AstFirst.GrammarPart(typeof(FirstNs.Root))]
    [AstFirst.GrammarPart(typeof(SecondNs.Root))]
    public sealed partial class Value : AstFirst.AstNode {
        [AstFirst.Rule] public static void Reduce([AstFirst.Pattern(""value"")] AstFirst.Token token, AstFirst.SemanticContext ctx) { }
    }
}
";

        var result = RunGeneratorResult(grammar);
        var generated = result.Results.Single().GeneratedSources;
        var sourceText = generated.Select(source => source.SourceText.ToString()).ToList();

        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "CS8785");
        Assert.Single(sourceText, source => source.Contains("partial class Value"));
        Assert.Contains(sourceText, source => source.Contains("FirstNs.Root.Analyze(__node"));
        Assert.Contains(sourceText, source => source.Contains("SecondNs.Root.Analyze(__node"));
    }
}
