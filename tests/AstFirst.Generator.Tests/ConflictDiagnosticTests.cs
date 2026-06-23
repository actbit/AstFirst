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
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class GrammarAttribute : System.Attribute { }
    [System.AttributeUsage(System.AttributeTargets.Parameter)]
    public class PatternAttribute : System.Attribute { public PatternAttribute(string regex) {} }
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class PrecedenceAttribute : System.Attribute { public PrecedenceAttribute(int priority) {} }
}
";

    private static IReadOnlyList<Microsoft.CodeAnalysis.Diagnostic> RunGenerator(string grammar)
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
        return driver.GetRunResult().Diagnostics;
    }

    [Fact]
    public void AmbiguousGrammarEmitsAstf001()
    {
        // Add の左再帰・優先度なし → shift-reduce コンフリクト → ASTF001 警告。
        var grammar = @"
[AstFirst.Grammar]
public class RootExpr : AstFirst.AstNode { }
public class Num : RootExpr {
    public Num([AstFirst.Pattern(""[0-9]+"")] AstFirst.Token n) { }
}
public class Add : RootExpr {
    public Add(RootExpr l, [AstFirst.Pattern(""\\+"")] AstFirst.Token op, RootExpr r) { }
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
    public Num([AstFirst.Pattern(""[0-9]+"")] AstFirst.Token n) { }
}
public class Unused : AstFirst.AstNode {
    public Unused([AstFirst.Pattern(""x"")] AstFirst.Token n) { }
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
    public A(B b) { }
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
    public Num([AstFirst.Pattern(""[0-9]+"")] AstFirst.Token n) { }
}
public class IntToken : AstFirst.Token {
    public IntToken([AstFirst.Pattern(""[0-9]+"")] int n) { }
}
";
        var diagnostics = RunGenerator(grammar);
        Assert.Contains(diagnostics, d => d.Id == "ASTF004");
    }
}
