using System.Collections.Generic;
using System.IO;
using System.Linq;
using AstFirst.Core.Lexing;
using AstFirst.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AstFirst.Tests.Generator;

public class WalkerEmitterTests
{
    private static GrammarModel ModelWithChildren()
    {
        var nodes = new List<NodeModel>
        {
            new NodeModel("TestNs.Expr", "AstFirst.AstNode", true, new List<RuleModel>()),
            new NodeModel("TestNs.NumExpr", "TestNs.Expr", false, new List<RuleModel>(),
                new List<ChildModel>()),
            new NodeModel("TestNs.AddExpr", "TestNs.Expr", false, new List<RuleModel>(),
                new List<ChildModel>
                {
                    new ChildModel("Left", "TestNs.Expr", false),
                    new ChildModel("Right", "TestNs.Expr", false),
                }),
            new NodeModel("TestNs.OptExpr", "TestNs.Expr", false, new List<RuleModel>(),
                new List<ChildModel> { new ChildModel("Inner", "TestNs.Expr", true) }),
        };
        return new GrammarModel("TestNs.Expr", nodes, new List<TokenDefModel>());
    }

    private static Compilation Compile(params string[] sources)
    {
        var trusted = (string)System.AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        var refs = trusted.Split(Path.PathSeparator)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
        refs.Add(MetadataReference.CreateFromFile(typeof(Dfa).Assembly.Location)); // AstFirst.Core
        return CSharpCompilation.Create("Generated",
            sources.Select(s => CSharpSyntaxTree.ParseText(s)),
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    [Fact]
    public void EmitWalker_GeneratesWalkerClass()
    {
        var source = WalkerEmitter.EmitWalker(ModelWithChildren(), "TestNs");
        Assert.Contains("public abstract class ExprWalker", source);
        Assert.Contains("public void Walk(AstFirst.AstNode root, AstFirst.SemanticContext ctx)", source);
        Assert.Contains("protected virtual void EnterEach", source);
        Assert.Contains("protected virtual void ExitEach", source);
        Assert.Contains("EnterAddExpr", source);
        Assert.Contains("ExitAddExpr", source);
        Assert.Contains("internal sealed class _Default : ExprWalker", source);
        // abstract ノード (Expr) には Enter/Exit メソッドを生成しない
        Assert.DoesNotContain("EnterExpr(", source);
    }

    [Fact]
    public void EmitWalker_ChildrenCollected()
    {
        var source = WalkerEmitter.EmitWalker(ModelWithChildren(), "TestNs");
        // 通常子
        Assert.Contains("children.Add(__n.Left)", source);
        Assert.Contains("children.Add(__n.Right)", source);
        // nullable 子は null チェック
        Assert.Contains("if (__n.Inner is not null) children.Add(__n.Inner)", source);
    }

    [Fact]
    public void EmitWalker_ProducesCompilableCode()
    {
        var model = ModelWithChildren();
        var walkerSource = WalkerEmitter.EmitWalker(model, "TestNs");
        var stubs = @"
namespace AstFirst {
    public abstract class AstNode { }
    public abstract class SemanticContext { }
    public interface IOnSecondPassEnter { void OnSecondPassEnter(SemanticContext ctx); }
    public interface IOnSecondPassExit { void OnSecondPassExit(SemanticContext ctx); }
}
namespace TestNs {
    public abstract class Expr : AstFirst.AstNode { }
    public class NumExpr : Expr { }
    public class AddExpr : Expr { public Expr Left { get; } public Expr Right { get; } public AddExpr(Expr l, Expr r) { Left = l; Right = r; } }
    public class OptExpr : Expr { public Expr? Inner { get; } public OptExpr(Expr? i) { Inner = i; } }
}";
        var comp = Compile(stubs, walkerSource);
        var errors = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.False(errors.Count > 0, "Walker 生成コードのコンパイルエラー:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    }
}
