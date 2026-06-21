using System.Collections.Generic;
using System.IO;
using System.Linq;
using AstFirst.Core.Lexing;
using AstFirst.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AstFirst.Tests.Generator;

public class ParserEmitterTests
{
    private static GrammarModel CalcModel()
    {
        var nodes = new List<NodeModel>
        {
            new NodeModel("Expr", "AstFirst.AstNode", true, new List<CtorModel>()),
            new NodeModel("NumExpr", "Expr", false, new List<CtorModel>
            {
                new CtorModel(new List<ParamModel>
                {
                    new ParamModel("AstFirst.Token", "num", "[0-9]+", false, 0)
                })
            }),
            new NodeModel("AddExpr", "Expr", false, new List<CtorModel>
            {
                new CtorModel(new List<ParamModel>
                {
                    new ParamModel("Expr", "left", null, false, 0),
                    new ParamModel("AstFirst.Token", "op", "\\+", false, 0),
                    new ParamModel("Expr", "right", null, false, 0)
                })
            }),
        };
        var tokenDefs = new List<TokenDefModel>
        {
            new TokenDefModel("AstFirst.Token", "[0-9]+", 0, false),
            new TokenDefModel("AstFirst.Token", "\\+", 0, false),
        };
        return new GrammarModel("Expr", nodes, tokenDefs);
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
    public void EmitParserProducesCompilableCode()
    {
        var model = CalcModel();
        var lexerSource = CodeEmitter.EmitLexer(model, "ExprLexer", "TestNs");
        var parserSource = ParserEmitter.EmitParser(model, "TestNs");
        var comp = Compile(lexerSource, parserSource);
        var errors = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.False(errors.Count > 0, "生成コードのコンパイルエラー:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    }

    [Fact]
    public void EmitParserContainsParseAndTables()
    {
        var source = ParserEmitter.EmitParser(CalcModel(), "TestNs");
        Assert.Contains("public static class ExprParser", source);
        Assert.Contains("public static object? Parse(string input)", source);
        Assert.Contains("ActionKind", source);
        Assert.Contains("Goto", source);
        Assert.Contains("ProdLhs", source);
        Assert.Contains("ProdLen", source);
        Assert.Contains("TokenIdToSym", source);
    }
}
