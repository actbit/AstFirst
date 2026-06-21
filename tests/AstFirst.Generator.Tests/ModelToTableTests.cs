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
        // デフォルト shift (左結合) で解決される。
        var model = CalcModel();
        var (_, table) = ModelToTable.BuildWithGrammar(model);
        Assert.Contains(table.Conflicts, c => c.Description.Contains("shift-reduce"));
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
}
