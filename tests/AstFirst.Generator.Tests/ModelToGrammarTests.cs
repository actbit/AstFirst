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
    public void TableResolvesShiftReduceFromLeftRecursionToShift()
    {
        // BuildCalcGrammar は Expr -> [0-9]+ | Expr + Expr (左再帰・優先度なし)。
        // shift-reduce は bison 互換に shift 優先で解決される (報告なし)。
        var g = BuildCalcGrammar();
        var first = new FirstSet(g);
        var auto = Lr0AutomatonBuilder.Build(g);
        var la = new LalrLookahead(g, auto, first);
        var table = LalrTableBuilder.Build(g, auto, la);
        Assert.DoesNotContain(table.Conflicts, c => c.Description.Contains("shift-reduce"));
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

    [Fact]
    public void EmptyConstructorBecomesEpsilonProduction()
    {
        // 引数なし ctor (NoElements) は右辺長 0 の ε 規則になる。
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
            new NodeModel("SampleJson.JsonElements", "AstFirst.AstNode", true, new List<CtorModel>()),
            new NodeModel("SampleJson.NoElements", "SampleJson.JsonElements", false, new List<CtorModel>
            {
                new CtorModel(new List<ParamModel>())   // 引数なし = ε
            }),
        };
        var tokenDefs = new List<TokenDefModel> { new TokenDefModel("AstFirst.Token", "[0-9]+", 0, false) };
        var model = new GrammarModel("SampleJson.Json", nodes, tokenDefs);
        var g = ModelToGrammar.Build(model);
        var eps = g.Productions.FirstOrDefault(p => p.Lhs.Name == "SampleJson.JsonElements" && p.Length == 0);
        Assert.NotNull(eps);   // ★ ε 規則が生成される (引数なし ctor)
    }

    [Fact]
    public void MultipleConstructorsProduceMultipleProductions()
    {
        // JsonObject に 2 ctor ({} と { Members }) → 同一 LHS に 2 規則。
        var nodes = new List<NodeModel>
        {
            new NodeModel("SampleJson.Json", "AstFirst.AstNode", true, new List<CtorModel>()),
            new NodeModel("SampleJson.JsonObject", "SampleJson.Json", false, new List<CtorModel>
            {
                new CtorModel(new List<ParamModel>
                {
                    new ParamModel("AstFirst.Token", "lb", "\\{", false, 0),
                    new ParamModel("AstFirst.Token", "rb", "\\}", false, 0),
                }),
                new CtorModel(new List<ParamModel>
                {
                    new ParamModel("AstFirst.Token", "lb", "\\{", false, 0),
                    new ParamModel("SampleJson.JsonMembers", "members", null, false, 0),
                    new ParamModel("AstFirst.Token", "rb", "\\}", false, 0),
                }),
            }),
            new NodeModel("SampleJson.JsonMembers", "AstFirst.AstNode", true, new List<CtorModel>()),
        };
        var tokenDefs = new List<TokenDefModel>
        {
            new TokenDefModel("AstFirst.Token", "\\{", 0, false),
            new TokenDefModel("AstFirst.Token", "\\}", 0, false),
        };
        var model = new GrammarModel("SampleJson.Json", nodes, tokenDefs);
        var g = ModelToGrammar.Build(model);
        var objProds = g.Productions.Where(p => p.Tag is ReduceActionModel ra && ra.AstTypeName == "SampleJson.JsonObject").ToList();
        Assert.Equal(2, objProds.Count);   // ★ 2規則生成
    }

    [Fact]
    public void UnresolvableParameterThrowsInvalidOperationException()
    {
        // [Pattern] も派生もない解決不能な引数型。
        var nodes = new List<NodeModel>
        {
            new NodeModel("Expr", "AstFirst.AstNode", true, new List<CtorModel>()),
            new NodeModel("BadExpr", "Expr", false, new List<CtorModel>
            {
                new CtorModel(new List<ParamModel>
                {
                    new ParamModel("UnknownType", "x", null, false, 0)
                })
            }),
        };
        var model = new GrammarModel("Expr", nodes, new List<TokenDefModel>());
        Assert.Throws<InvalidOperationException>(() => ModelToGrammar.Build(model));
    }

    [Fact]
    public void MissingStartSymbolThrowsInvalidOperationException()
    {
        var nodes = new List<NodeModel>
        {
            new NodeModel("Expr", "AstFirst.AstNode", true, new List<CtorModel>()),
        };
        var model = new GrammarModel("Nonexistent", nodes, new List<TokenDefModel>());
        Assert.Throws<InvalidOperationException>(() => ModelToGrammar.Build(model));
    }
}
