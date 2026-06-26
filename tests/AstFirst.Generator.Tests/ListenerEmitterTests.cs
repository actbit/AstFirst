using System.Collections.Generic;
using System.IO;
using System.Linq;
using AstFirst.Core.Lexing;
using AstFirst.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AstFirst.Tests.Generator;

public class ListenerEmitterTests
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
    public void EmitListener_GeneratesListenerClass()
    {
        var source = ListenerEmitter.EmitListener(ModelWithChildren(), "TestNs");
        Assert.Contains("public abstract class ExprListener", source);
        Assert.Contains("public virtual void Walk(AstFirst.AstNode node)", source);
        Assert.Contains("EnterAddExpr", source);
        Assert.Contains("ExitAddExpr", source);
        // abstract ノード (Expr) には Enter/Exit メソッドを生成しない
        Assert.DoesNotContain("EnterExpr(", source);
    }

    [Fact]
    public void EmitListener_WalksChildren()
    {
        var source = ListenerEmitter.EmitListener(ModelWithChildren(), "TestNs");
        Assert.Contains("Walk(n.Left)", source);
        Assert.Contains("Walk(n.Right)", source);
        // nullable の子は null チェック付き
        Assert.Contains("if (n.Inner is not null) Walk(n.Inner)", source);
    }

    [Fact]
    public void EmitListener_ProducesCompilableCode()
    {
        var model = ModelWithChildren();
        var listenerSource = ListenerEmitter.EmitListener(model, "TestNs");
        var stubs = @"
namespace AstFirst { public abstract class AstNode { } }
namespace TestNs {
    public abstract class Expr : AstFirst.AstNode { }
    public class NumExpr : Expr { }
    public class AddExpr : Expr { public Expr Left { get; } public Expr Right { get; } public AddExpr(Expr l, Expr r) { Left = l; Right = r; } }
    public class OptExpr : Expr { public Expr? Inner { get; } public OptExpr(Expr? i) { Inner = i; } }
}";
        var comp = Compile(stubs, listenerSource);
        var errors = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.False(errors.Count > 0, "Listener 生成コードのコンパイルエラー:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    }
}
