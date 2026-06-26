using System.Collections.Generic;
using System.Linq;
using AstFirst.Core.Lexing;
using AstFirst.Core.Parsing;
using AstFirst.Generator;

namespace AstFirst.Tests.Generator;

public class ModelToTableTests
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

    [Fact]
    public void DfaAcceptsNumberAndPlus()
    {
        var model = CalcModel();
        var dfa = ModelToDfa.Build(model, out var rules);
        // DFA を駆動して "123" が1トークン、"+" が1トークンになること。
        var lex = new Lexer(dfa, rules, "123+");
        var toks = lex.Tokenize();
        Assert.Equal(2, toks.Count);
        Assert.Equal("123", toks[0].Text);
        Assert.Equal("+", toks[1].Text);
    }

    [Fact]
    public void TableResolvesShiftReduceFromLeftRecursionToShift()
    {
        // Expr -> Expr + Expr (左再帰・優先度なし) の shift-reduce は
        // bison 互換に shift 優先で解決される (報告なし)。
        var model = CalcModel();
        var (_, table) = ModelToTable.BuildWithGrammar(model);
        Assert.DoesNotContain(table.Conflicts, c => c.Description.Contains("shift-reduce"));
    }

    [Fact]
    public void UnresolvedShiftReduceDefaultsToShift()
    {
        // 優先度/結合性が未設定の shift-reduce は shift (bison 互換の既定) を選ぶ。
        // (左結合にするには [Precedence] で明示的に設定する。case 2 で解決)。
        var model = CalcModel();
        var (_, table) = ModelToTable.BuildWithGrammar(model);
        // shift 優先で解決されるため shift-reduce コンフリクトは残らない。
        Assert.DoesNotContain(table.Conflicts, c => c.Description.Contains("shift-reduce"));
    }

    [Fact]
    public void PatternToTokenIdMapsPatterns()
    {
        var model = CalcModel();
        ModelToDfa.Build(model, out var rules);
        var map = ModelToDfa.PatternToTokenId(rules);
        Assert.Equal(2, map.Count);
        Assert.Contains("[0-9]+", map.Keys);
        Assert.Contains("\\+", map.Keys);
    }

    private static GrammarModel CalcModelWithPrecedence()
    {
        // Expr -> Expr + Expr (AddExpr, prec 1) | Expr * Expr (MulExpr, prec 2) | num
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
                    new ParamModel("Expr", "right", null, false, false, 0),
                })
            }, precedencePriority: 1),
            new NodeModel("MulExpr", "Expr", false, new List<RuleModel>
            {
                new RuleModel("Reduce", new List<ParamModel>
                {
                    new ParamModel("Expr", "left", null, false, false, 0),
                    new ParamModel("AstFirst.Token", "op", "\\*", false, false, 0),
                    new ParamModel("Expr", "right", null, false, false, 0),
                })
            }, precedencePriority: 2),
        };
        var tokenDefs = new List<TokenDefModel>
        {
            new TokenDefModel("AstFirst.Token", "[0-9]+", 0, false),
            new TokenDefModel("AstFirst.Token", "\\+", 0, false),
            new TokenDefModel("AstFirst.Token", "\\*", 0, false),
        };
        return new GrammarModel("Expr", nodes, tokenDefs);
    }

    [Fact]
    public void PrecedenceResolvesShiftReduceWithoutConflict()
    {
        // AddExpr(+, prec1) と MulExpr(*, prec2) で優先度を設定 → shift-reduce が解決され衝突なし。
        var model = CalcModelWithPrecedence();
        var (_, table) = ModelToTable.BuildWithGrammar(model);
        Assert.False(table.HasConflicts, string.Join("\n", table.Conflicts.Select(c => c.Description)));
    }

    [Fact]
    public void PrecedenceAssignsHigherToStarThanPlus()
    {
        // * の TerminalPrecedence が + より高いことを確認。
        var model = CalcModelWithPrecedence();
        var (grammar, _) = ModelToTable.BuildWithGrammar(model);
        var starSym = grammar.Symbols.First(s => s.IsTerminal && s.Name == "token:\\*");
        var plusSym = grammar.Symbols.First(s => s.IsTerminal && s.Name == "token:\\+");
        Assert.True(grammar.TerminalPrecedence[starSym.Id].Priority > grammar.TerminalPrecedence[plusSym.Id].Priority);
    }
}
