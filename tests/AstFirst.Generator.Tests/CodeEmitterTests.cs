using System.Collections.Generic;
using System.IO;
using System.Linq;
using AstFirst.Core.Lexing;
using AstFirst.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AstFirst.Tests.Generator;

public class CodeEmitterTests
{
    private static GrammarModel CalcModel()
    {
        var nodes = new List<NodeModel>
        {
            new NodeModel("Expr", "AstFirst.AstNode", true, new List<RuleModel>()),
            new NodeModel("NumExpr", "Expr", false, new List<RuleModel>
            {
                new RuleModel("Reduce", new List<ParamModel>
                {
                    new ParamModel("AstFirst.Token", "num", "[0-9]+", false, false, 0)
                })
            }),
        };
        var tokenDefs = new List<TokenDefModel>
        {
            new TokenDefModel("AstFirst.Token", "[0-9]+", 0, false),
        };
        return new GrammarModel("Expr", nodes, tokenDefs);
    }

    private static Compilation Compile(string source)
    {
        var trusted = (string)System.AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        var refs = trusted.Split(Path.PathSeparator)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
        refs.Add(MetadataReference.CreateFromFile(typeof(Dfa).Assembly.Location)); // AstFirst.Core

        return CSharpCompilation.Create("Generated",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    [Fact]
    public void EmitLexerProducesCompilableCode()
    {
        var model = CalcModel();
        var dfa = ModelToDfa.Build(model, out var rules);
        var source = CodeEmitter.EmitLexer(model, dfa, rules, "CalcLexer", "TestNs");
        var comp = Compile(source);
        var errors = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.False(errors.Count > 0, "生成コードのコンパイルエラー:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    }

    [Fact]
    public void EmitLexerContainsTokenizeAndDfa()
    {
        var model = CalcModel();
        var dfa = ModelToDfa.Build(model, out var rules);
        var source = CodeEmitter.EmitLexer(model, dfa, rules, "CalcLexer", "TestNs");
        Assert.Contains("public static class CalcLexer", source);
        Assert.Contains("public static List<LexToken> Tokenize(string input)", source);
        Assert.Contains("Boundaries", source);
        Assert.Contains("Transitions", source);
        Assert.Contains("AcceptTokenIds", source);
        Assert.Contains("namespace TestNs", source);
    }

    [Fact]
    public void SplitFullNameHandlesNoNamespace()
    {
        var (ns, type) = CodeEmitter.SplitFullName("Expr");
        Assert.Equal("", ns);
        Assert.Equal("Expr", type);
    }

    [Fact]
    public void SplitFullNameHandlesNamespace()
    {
        var (ns, type) = CodeEmitter.SplitFullName("Foo.Bar.Expr");
        Assert.Equal("Foo.Bar", ns);
        Assert.Equal("Expr", type);
    }
}
