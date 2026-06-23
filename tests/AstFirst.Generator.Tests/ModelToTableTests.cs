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
    public void TableHasShiftReduceConflictFromLeftRecursion()
    {
        // Expr -> Expr + Expr (左再帰・優先度なし) は shift-reduce 衝突。
        // デフォルト reduce (左結合) で解決される (優先度/結合性未設定時)。
        var model = CalcModel();
        var (_, table) = ModelToTable.BuildWithGrammar(model);
        Assert.Contains(table.Conflicts, c => c.Description.Contains("shift-reduce"));
    }

    [Fact]
    public void UnresolvedShiftReduceDefaultsToLeftAssociative()
    {
        // 優先度/結合性が未設定の shift-reduce は reduce (左結合) を選ぶ。
        // (従来は shift 優先 = 右結合的だったが、左結合が直感的なので変更)。
        var model = CalcModel();
        var (_, table) = ModelToTable.BuildWithGrammar(model);
        var shiftReduce = table.Conflicts.Where(c => c.Description.Contains("shift-reduce")).ToList();
        Assert.NotEmpty(shiftReduce);
        foreach (var c in shiftReduce)
            Assert.Equal(LrActionKind.Reduce, table.Action(c.State, c.SymbolId).Kind);
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
                    new ParamModel("Expr", "right", null, false, 0),
                })
            }, precedencePriority: 1),
            new NodeModel("MulExpr", "Expr", false, new List<CtorModel>
            {
                new CtorModel(new List<ParamModel>
                {
                    new ParamModel("Expr", "left", null, false, 0),
                    new ParamModel("AstFirst.Token", "op", "\\*", false, 0),
                    new ParamModel("Expr", "right", null, false, 0),
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
