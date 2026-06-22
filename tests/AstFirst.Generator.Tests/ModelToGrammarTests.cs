using System.Collections.Generic;
using System.Linq;
using AstFirst.Core.Parsing;
using AstFirst.Generator;

namespace AstFirst.Tests.Generator;

public class ModelToGrammarTests
{
    private static Grammar BuildCalcGrammar()
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
        var model = new GrammarModel("Expr", nodes, tokenDefs);
        return ModelToGrammar.Build(model);
    }

    [Fact]
    public void RootIsStartSymbol()
    {
        var g = BuildCalcGrammar();
        Assert.Equal("Expr", g.StartSymbol.Name);
        Assert.Equal("Expr'", g.AugmentedStart.Name);
    }

    [Fact]
    public void ConcreteNodesBecomeProductions()
    {
        var g = BuildCalcGrammar();
        // NumExpr -> [0-9]+, AddExpr -> Expr + Expr, 拡張 S' -> Expr $
        Assert.Equal(3, g.Productions.Count);
    }

    [Fact]
    public void TokenPatternsBecomeTerminals()
    {
        var g = BuildCalcGrammar();
        Assert.Contains(g.Symbols, s => s.IsTerminal && s.Name == "token:[0-9]+");
        Assert.Contains(g.Symbols, s => s.IsTerminal && s.Name == "token:\\+");
    }

    [Fact]
    public void TableHasShiftReduceConflictFromLeftRecursion()
    {
        // BuildCalcGrammar は Expr -> [0-9]+ | Expr + Expr (左再帰・優先度なし)。
        // shift-reduce 衝突が発生しデフォルト shift で解決される。
        var g = BuildCalcGrammar();
        var first = new FirstSet(g);
        var auto = Lr0AutomatonBuilder.Build(g);
        var la = new LalrLookahead(g, auto, first);
        var table = LalrTableBuilder.Build(g, auto, la);
        Assert.Contains(table.Conflicts, c => c.Description.Contains("shift-reduce"));
    }

    [Fact]
    public void TokenDerivedTypeResolvesToTerminal()
    {
        // Token 派生クラスの引数 ([Pattern] なし) は型名から解決。
        var nodes = new List<NodeModel>
        {
            new NodeModel("Expr", "AstFirst.AstNode", true, new List<CtorModel>()),
            new NodeModel("NumExpr", "Expr", false, new List<CtorModel>
            {
                new CtorModel(new List<ParamModel>
                {
                    new ParamModel("NumToken", "num", null, false, 0)
                })
            }),
        };
        var tokenDefs = new List<TokenDefModel>
        {
            new TokenDefModel("NumToken", "[0-9]+", 0, false),
        };
        var model = new GrammarModel("Expr", nodes, tokenDefs);
        var g = ModelToGrammar.Build(model);
        Assert.Contains(g.Symbols, s => s.IsTerminal && s.Name == "token:[0-9]+");
    }

    [Fact]
    public void AstNodeDirectConcreteClassBecomesNonterminal()
    {
        // AstNode 直系の具象クラス (親が非終端でない) を cons 引数で参照したとき、
        // そのクラス自身が非終端の左辺として規則を生成する。
        // 過去のバグ回帰: 以前は BaseFullName が非終端でないと continue され、
        // JsonMember のような葉クラスの規則が欠落して到達不能非終端になっていた。
        var nodes = new List<NodeModel>
        {
            new NodeModel("SampleJson.Json", "AstFirst.AstNode", true, new List<CtorModel>()),
            new NodeModel("SampleJson.JsonNumber", "SampleJson.Json", false, new List<CtorModel>
            {
                new CtorModel(new List<ParamModel>
                {
                    new ParamModel("AstFirst.Token", "num", "[0-9]+", false, 0)
                })
            }),
            // ★ AstNode 直系の具象 (葉) クラス。cons 引数から参照される。
            new NodeModel("SampleJson.JsonMember", "AstFirst.AstNode", false, new List<CtorModel>
            {
                new CtorModel(new List<ParamModel>
                {
                    new ParamModel("AstFirst.Token", "key", "\"[^\"]*\"", false, 0),
                    new ParamModel("AstFirst.Token", "colon", ":", false, 0),
                    new ParamModel("SampleJson.Json", "value", null, false, 0),
                })
            }),
            new NodeModel("SampleJson.JsonMembers", "AstFirst.AstNode", true, new List<CtorModel>()),
            new NodeModel("SampleJson.ConsMembers", "SampleJson.JsonMembers", false, new List<CtorModel>
            {
                new CtorModel(new List<ParamModel>
                {
                    new ParamModel("SampleJson.JsonMember", "head", null, false, 0),
                })
            }),
        };
        var tokenDefs = new List<TokenDefModel>
        {
            new TokenDefModel("AstFirst.Token", "[0-9]+", 0, false),
            new TokenDefModel("AstFirst.Token", "\"[^\"]*\"", 0, false),
            new TokenDefModel("AstFirst.Token", ":", 0, false),
        };
        var model = new GrammarModel("SampleJson.Json", nodes, tokenDefs);
        var grammar = ModelToGrammar.Build(model);

        // JsonMember が非終端の左辺として規則を持つ (バグ前は欠落)。
        Assert.Contains(grammar.Productions, p => p.Lhs.Name == "SampleJson.JsonMember");
        var memberProd = grammar.Productions.First(p => p.Lhs.Name == "SampleJson.JsonMember");
        // 右辺は key (STRING) : Json の 3 記号。
        Assert.Equal(3, memberProd.Rhs.Length);
    }
}
