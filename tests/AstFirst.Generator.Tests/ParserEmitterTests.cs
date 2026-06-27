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
        var dfa = ModelToDfa.Build(model, out var rules);
        var lexerSource = CodeEmitter.EmitLexer(model, dfa, rules, "ExprLexer", "TestNs");
        var (grammar, table) = ModelToTable.BuildWithGrammar(model);
        var parserSource = ParserEmitter.EmitParser(model, grammar, table, rules, "TestNs");
        var stubs = @"
namespace AstFirst {
    public abstract class AstNode { public bool IsAccepted => true; public virtual void OnSecondPassEnter(SemanticContext ctx) { } public virtual void OnSecondPassExit(SemanticContext ctx) { } }
    public abstract class Token { public Token(string t, SourceSpan s) { } public Token(System.ReadOnlyMemory<char> t, SourceSpan s) { } public virtual string Text => string.Empty; }
    public sealed class BasicToken : Token { public BasicToken(string t, SourceSpan s) : base(t, s) { } public BasicToken(System.ReadOnlyMemory<char> t, SourceSpan s) : base(t, s) { } }
    public readonly struct Position { public Position(int o, int l, int c) { } }
    public readonly struct SourceSpan { public SourceSpan(Position s, Position e) { } }
    public enum Severity { Error, Warning }
    public sealed class Diagnostic { public Severity Severity { get; } public Diagnostic(string m, SourceSpan s, Severity v) { Severity = v; } }
    public sealed class DiagnosticBag { public System.Collections.Generic.IReadOnlyList<Diagnostic> Items { get; } = new System.Collections.Generic.List<Diagnostic>(); }
    public sealed class ParseError { public ParseError(string m, int p) { } }
    public sealed class ParseResult { public ParseResult(object? a, System.Collections.Generic.IReadOnlyList<ParseError> e, System.Collections.Generic.IReadOnlyList<Diagnostic>? d) { } }
    public abstract class SemanticContext { public abstract DiagnosticBag Diagnostics { get; } }
    public sealed class BasicSemanticContext : SemanticContext { public override DiagnosticBag Diagnostics { get; } = new DiagnosticBag(); }
}
public class Expr : AstFirst.AstNode { }
public class NumExpr : Expr { public NumExpr(string ruleName, AstFirst.Token t) { } }
public class AddExpr : Expr { public AddExpr(string ruleName, Expr a, AstFirst.Token b, Expr c) { } }
";
        var comp = Compile(stubs, lexerSource, parserSource);
        var errors = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.False(errors.Count > 0, "生成コードのコンパイルエラー:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    }

    [Fact]
    public void EmitParserContainsParseAndTables()
    {
        var model = CalcModel();
        var (grammar, table) = ModelToTable.BuildWithGrammar(model);
        ModelToDfa.Build(model, out var rules);
        var source = ParserEmitter.EmitParser(model, grammar, table, rules, "TestNs");
        Assert.Contains("public static class ExprParser", source);
        Assert.Contains("public static AstFirst.ParseResult Parse(string input)", source);
        Assert.Contains("public static AstFirst.ParseResult Parse(string input, AstFirst.SemanticContext? context)", source);
        Assert.Contains("ActionKind", source);
        Assert.Contains("Goto", source);
        Assert.Contains("ProdLhs", source);
        Assert.Contains("ProdLen", source);
        Assert.Contains("TokenIdToSym", source);
    }

    private static GrammarModel TokenDerivedModel()
    {
        // NumExpr(NumToken) — Token派生型を ctor 引数に取る (G7)。
        var nodes = new List<NodeModel>
        {
            new NodeModel("Expr", "AstFirst.AstNode", true, new List<RuleModel>()),
            new NodeModel("NumExpr", "Expr", false, new List<RuleModel>
            {
                new RuleModel("Reduce", new List<ParamModel>
                {
                    new ParamModel("NumToken", "num", null, false, false, 0)   // Token派生型、Pattern なし
                })
            }),
        };
        var tokenDefs = new List<TokenDefModel>
        {
            new TokenDefModel("NumToken", "[0-9]+", 0, false),   // Key=NumToken (Token派生)
        };
        return new GrammarModel("Expr", nodes, tokenDefs);
    }

    [Fact]
    public void EmitParserRegeneratesTokenDerivedType()
    {
        // G7: Token派生型 (NumToken) 引数は new NumToken(token.Text) で再生成 (キャストでない)。
        var model = TokenDerivedModel();
        ModelToDfa.Build(model, out var rules);
        var (grammar, table) = ModelToTable.BuildWithGrammar(model);
        var source = ParserEmitter.EmitParser(model, grammar, table, rules, "TestNs");
        Assert.Contains("new NumToken(", source);
        Assert.DoesNotContain("(NumToken)c[", source);
    }

    [Fact]
    public void EmitParserWithTokenDerivedParameterCompiles()
    {
        // 生成コード (new NumExpr(new NumToken(token.Text))) がコンパイル通り。
        var model = TokenDerivedModel();
        var dfa = ModelToDfa.Build(model, out var rules);
        var lexerSource = CodeEmitter.EmitLexer(model, dfa, rules, "ExprLexer", "TestNs");
        var (grammar, table) = ModelToTable.BuildWithGrammar(model);
        var parserSource = ParserEmitter.EmitParser(model, grammar, table, rules, "TestNs");
        var stubs = @"
namespace AstFirst {
    public abstract class AstNode { public bool IsAccepted => true; public virtual void OnSecondPassEnter(SemanticContext ctx) { } public virtual void OnSecondPassExit(SemanticContext ctx) { } }
    public abstract class Token { public Token(string t, SourceSpan s) { } public Token(System.ReadOnlyMemory<char> t, SourceSpan s) { } public virtual string Text => string.Empty; }
    public sealed class BasicToken : Token { public BasicToken(string t, SourceSpan s) : base(t, s) { } public BasicToken(System.ReadOnlyMemory<char> t, SourceSpan s) : base(t, s) { } }
    public readonly struct Position { public Position(int o, int l, int c) { } }
    public readonly struct SourceSpan { public SourceSpan(Position s, Position e) { } }
    public enum Severity { Error, Warning }
    public sealed class Diagnostic { public Severity Severity { get; } public Diagnostic(string m, SourceSpan s, Severity v) { Severity = v; } }
    public sealed class DiagnosticBag { public System.Collections.Generic.IReadOnlyList<Diagnostic> Items { get; } = new System.Collections.Generic.List<Diagnostic>(); }
    public sealed class ParseError { public ParseError(string m, int p) { } }
    public sealed class ParseResult { public ParseResult(object? a, System.Collections.Generic.IReadOnlyList<ParseError> e, System.Collections.Generic.IReadOnlyList<Diagnostic>? d) { } }
    public abstract class SemanticContext { public abstract DiagnosticBag Diagnostics { get; } }
    public sealed class BasicSemanticContext : SemanticContext { public override DiagnosticBag Diagnostics { get; } = new DiagnosticBag(); }
}
public class NumToken : AstFirst.Token { public NumToken(string t) : base(t, default) { } }
public class Expr : AstFirst.AstNode { }
public class NumExpr : Expr { public NumExpr(string ruleName, NumToken n) { } }
";
        var comp = Compile(stubs, lexerSource, parserSource);
        var errors = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.False(errors.Count > 0, "Token派生型の生成コードのコンパイルエラー:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    }

    private static GrammarModel SemanticContextModel()
    {
        // SemanticContext 派生引数 (IsContext) を持つ ctor。
        var nodes = new List<NodeModel>
        {
            new NodeModel("Expr", "AstFirst.AstNode", true, new List<RuleModel>()),
            new NodeModel("DeclExpr", "Expr", false, new List<RuleModel>
            {
                new RuleModel("Reduce", new List<ParamModel>
                {
                    new ParamModel("AstFirst.Token", "name", "[a-z]+", false, false, 0),
                    new ParamModel("AstFirst.SemanticContext", "ctx", null, true, false, 0),
                })
            }),
        };
        var tokenDefs = new List<TokenDefModel> { new TokenDefModel("AstFirst.Token", "[a-z]+", 0, false) };
        return new GrammarModel("Expr", nodes, tokenDefs);
    }

    [Fact]
    public void EmitParserInjectsSemanticContext()
    {
        // SemanticContext 派生引数は reduce ケースで ctx を注入する。
        var model = SemanticContextModel();
        ModelToDfa.Build(model, out var rules);
        var (grammar, table) = ModelToTable.BuildWithGrammar(model);
        var source = ParserEmitter.EmitParser(model, grammar, table, rules, "TestNs");
        // new DeclExpr(..., ctx) のように ctx が渡る。
        Assert.Contains("new DeclExpr(", source);
        Assert.Contains("ctx)", source);
    }

    private static GrammarModel RepeatModel()
    {
        // Program → [Repeat] Stmt を List_Stmt → Stmt | List_Stmt Stmt に展開。
        // ProgramBody.Statements は IReadOnlyList<StmtItem>。
        var nodes = new List<NodeModel>
        {
            new NodeModel("Program", "AstFirst.AstNode", true, new List<RuleModel>()),
            new NodeModel("ProgramBody", "Program", false, new List<RuleModel>
            {
                new RuleModel("Body", new List<ParamModel>
                {
                    new ParamModel("StmtItem", "statements", null, false, true, 0, false, 1)  // RepeatMin=1 (Plus)
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
        return new GrammarModel("Program", nodes, tokenDefs);
    }

    [Fact]
    public void EmitParserExpandsRepeatIntoListConstruction()
    {
        // [Repeat] は List_T → item | List_T item に展開され、reduce で List<T> を構築する。
        var model = RepeatModel();
        ModelToDfa.Build(model, out var rules);
        var (grammar, table) = ModelToTable.BuildWithGrammar(model);
        var source = ParserEmitter.EmitParser(model, grammar, table, rules, "TestNs");
        // リスト構築: new List<StmtItem>(...) と __list.Add(...)。
        Assert.Contains("new System.Collections.Generic.List<StmtItem>(4)", source);
        Assert.Contains("__list.Add((StmtItem)", source);
        // ProgramBody の reduce で IReadOnlyList<StmtItem> を渡す。
        Assert.Contains("new ProgramBody(\"Body\"", source);
        Assert.Contains("IReadOnlyList<StmtItem>", source);
    }

    [Fact]
    public void EmitParserWithRepeatCompiles()
    {
        // [Repeat] 展開後の生成コード (List 構築 + IReadOnlyList 渡し) がコンパイル通り。
        var model = RepeatModel();
        var dfa = ModelToDfa.Build(model, out var rules);
        var lexerSource = CodeEmitter.EmitLexer(model, dfa, rules, "ProgramLexer", "TestNs");
        var (grammar, table) = ModelToTable.BuildWithGrammar(model);
        var parserSource = ParserEmitter.EmitParser(model, grammar, table, rules, "TestNs");
        var stubs = @"
namespace AstFirst {
    public abstract class AstNode { public bool IsAccepted => true; public virtual void OnSecondPassEnter(SemanticContext ctx) { } public virtual void OnSecondPassExit(SemanticContext ctx) { } }
    public abstract class Token { public Token(string t, SourceSpan s) { } public Token(System.ReadOnlyMemory<char> t, SourceSpan s) { } public virtual string Text => string.Empty; }
    public sealed class BasicToken : Token { public BasicToken(string t, SourceSpan s) : base(t, s) { } public BasicToken(System.ReadOnlyMemory<char> t, SourceSpan s) : base(t, s) { } }
    public readonly struct Position { public Position(int o, int l, int c) { } }
    public readonly struct SourceSpan { public SourceSpan(Position s, Position e) { } }
    public enum Severity { Error, Warning }
    public sealed class Diagnostic { public Severity Severity { get; } public Diagnostic(string m, SourceSpan s, Severity v) { Severity = v; } }
    public sealed class DiagnosticBag { public System.Collections.Generic.IReadOnlyList<Diagnostic> Items { get; } = new System.Collections.Generic.List<Diagnostic>(); }
    public sealed class ParseError { public ParseError(string m, int p) { } }
    public sealed class ParseResult { public ParseResult(object? a, System.Collections.Generic.IReadOnlyList<ParseError> e, System.Collections.Generic.IReadOnlyList<Diagnostic>? d) { } }
    public abstract class SemanticContext { public abstract DiagnosticBag Diagnostics { get; } }
    public sealed class BasicSemanticContext : SemanticContext { public override DiagnosticBag Diagnostics { get; } = new DiagnosticBag(); }
}
public class Program : AstFirst.AstNode { }
public class ProgramBody : Program { public ProgramBody(string ruleName, System.Collections.Generic.IReadOnlyList<StmtItem> statements) { } }
public class StmtItem : AstFirst.AstNode { public StmtItem(string ruleName, AstFirst.Token text) { } }
";
        var comp = Compile(stubs, lexerSource, parserSource);
        var errors = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.False(errors.Count > 0, "[Repeat] 展開の生成コードのコンパイルエラー:\n" + string.Join("\n", errors.Select(e => e.ToString())));
    }
}
