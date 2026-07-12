using System.Collections.Generic;
using System.IO;
using System.Linq;
using AstFirst;
using AstFirst.Core.Lexing;
using AstFirst.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AstFirst.Tests.Generator;

/// <summary>GlrParserEmitter の生成コード検証。Core+Runtime の実アセンブリを参照してコンパイル。</summary>
public class GlrParserEmitterTests
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
            new NodeModel("AddExpr", "Expr", false, new List<RuleModel>
            {
                new RuleModel("Reduce", new List<ParamModel>
                {
                    new ParamModel("Expr", "left", null, false, false, 0),
                    new ParamModel("AstFirst.Token", "op", "\\+", false, false, 0),
                    new ParamModel("Expr", "right", null, false, false, 0)
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

    private static Compilation CompileWithRuntime(params string[] sources)
    {
        var trusted = (string)System.AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        // Generator に Core が直コンパイルされており AstFirst.Generator.dll と AstFirst.Core.dll で同型が重複 (CS0433)。
        // テストホストの TPA に Generator.dll が含まれるため除外する (生成コードは Generator 型を使わない)。
        var refs = trusted.Split(Path.PathSeparator)
            .Where(p => !Path.GetFileName(p).Equals("AstFirst.Generator.dll", System.StringComparison.OrdinalIgnoreCase))
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
        // Generator に Core が直コンパイルされておりアセンブリ名が 'AstFirst.Generator' になるため、
        // typeof(Dfa).Assembly では Core 型の参照解決 (CS0012) に失敗する。Runtime.dll と同ディレクトリの
        // AstFirst.Core.dll を直接参照する。
        var runtimeDir = Path.GetDirectoryName(typeof(AstNode).Assembly.Location)!;
        refs.Add(MetadataReference.CreateFromFile(Path.Combine(runtimeDir, "AstFirst.Core.dll")));
        refs.Add(MetadataReference.CreateFromFile(typeof(AstNode).Assembly.Location));   // AstFirst.Runtime
        return CSharpCompilation.Create("Generated",
            sources.Select(s => CSharpSyntaxTree.ParseText(s)),
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    [Fact]
    public void EmitParserProducesCompilableCode()
    {
        var model = CalcModel();
        var dfa = ModelToDfa.Build(model, out var rules);
        var lexerSource = CodeEmitter.EmitLexer(model, dfa, rules, "ExprLexer", "TestNs");
        var (grammar, table) = ModelToTable.BuildWithGrammar(model);
        var parserSource = GlrParserEmitter.EmitParser(model, grammar, table, rules, "TestNs");
        var userNodes = @"
public class Expr : AstFirst.AstNode { }
public class NumExpr : Expr { public NumExpr(string ruleName, AstFirst.Token t) { } }
public class AddExpr : Expr { public AddExpr(string ruleName, Expr a, AstFirst.Token b, Expr c) { } }
";
        var comp = CompileWithRuntime(userNodes, lexerSource, parserSource);
        var errors = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.False(errors.Count > 0, "GLR 生成コードのコンパイルエラー:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    }

    [Fact]
    public void EmitParserContainsDriverCallAndTables()
    {
        var model = CalcModel();
        var (grammar, table) = ModelToTable.BuildWithGrammar(model);
        ModelToDfa.Build(model, out var rules);
        var source = GlrParserEmitter.EmitParser(model, grammar, table, rules, "TestNs");
        Assert.Contains("public static class ExprParser", source);
        Assert.Contains("AstFirst.Glr.GlrTables(", source);
        Assert.Contains("AstFirst.Glr.LightGlrDriver.Run(", source);
        Assert.Contains("ActionKind", source);
        Assert.Contains("Goto", source);
        Assert.Contains("ProdLhs", source);
    }

    [Fact]
    public void EmitParserUsesChildrenIndexNotStackOffset()
    {
        // GLR の reduce は children[i] (右辺 i 番目) で参照。values[top - len + i] (LALR の仮想 reduce) でない。
        var model = CalcModel();
        var (grammar, table) = ModelToTable.BuildWithGrammar(model);
        ModelToDfa.Build(model, out var rules);
        var source = GlrParserEmitter.EmitParser(model, grammar, table, rules, "TestNs");
        Assert.Contains("children[", source);
        Assert.DoesNotContain("values[top -", source);
    }

    [Fact]
    public void EmitParserUsesCopyOnWriteListForRecursive()
    {
        // [Repeat] の再帰リスト (List_T → List_T item) は COW: foreach でコピーしてから Add。
        var nodes = new List<NodeModel>
        {
            new NodeModel("Program", "AstFirst.AstNode", true, new List<RuleModel>()),
            new NodeModel("ProgramBody", "Program", false, new List<RuleModel>
            {
                new RuleModel("Body", new List<ParamModel>
                {
                    new ParamModel("StmtItem", "statements", null, false, true, 0, false, 1) // RepeatMin=1 (Plus)
                })
            }),
            new NodeModel("StmtItem", "AstFirst.AstNode", false, new List<RuleModel>
            {
                new RuleModel("Reduce", new List<ParamModel>
                {
                    new ParamModel("AstFirst.Token", "text", "[a-z]+", false, false, 0)
                })
            }),
        };
        var tokenDefs = new List<TokenDefModel> { new TokenDefModel("AstFirst.Token", "[a-z]+", 0, false) };
        var model = new GrammarModel("Program", nodes, tokenDefs);

        ModelToDfa.Build(model, out var rules);
        var (grammar, table) = ModelToTable.BuildWithGrammar(model);
        var source = GlrParserEmitter.EmitParser(model, grammar, table, rules, "TestNs");
        // COW: __src を foreach でコピー。破壊的 Add (ParserEmitter の __list.Add(...); 単独) でない。
        Assert.Contains("foreach (var __x in __src)", source);
        Assert.Contains("__list.Add(__x)", source);
    }
}
