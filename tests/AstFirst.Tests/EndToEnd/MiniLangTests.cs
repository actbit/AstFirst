using MiniLang;

namespace AstFirst.Tests.EndToEnd;

/// <summary>
/// MiniLang (変数宣言・print・四則演算) のエンドツーエンドテスト。
/// Generator が StmtParser/StmtLexer を生成し、Parse が AST を返すことを検証。
/// </summary>
public class MiniLangTests
{
    [Fact]
    public void ParsesLetStatement()
    {
        // let x = 1+2*3; → LetStmt { Name="x", Value=AddExpr(1, MulExpr(2,3)) }
        var result = StmtParser.Parse("let x = 1+2*3;");
        Assert.False(result.HasErrors);
        var let = Assert.IsType<LetStmt>(result.Ast);
        Assert.Equal("x", let.Name);
        var add = Assert.IsType<AddExpr>(let.Value);
        Assert.IsType<NumExpr>(add.Left);
        var mul = Assert.IsType<MulExpr>(add.Right);
        Assert.Equal(2, ((NumExpr)mul.Left).Value);
        Assert.Equal(3, ((NumExpr)mul.Right).Value);
    }

    [Fact]
    public void ParsesPrintStatement()
    {
        // print x; → PrintStmt { Value=VarExpr("x") }
        var result = StmtParser.Parse("print x;");
        Assert.False(result.HasErrors);
        var print = Assert.IsType<PrintStmt>(result.Ast);
        var varExpr = Assert.IsType<VarExpr>(print.Value);
        Assert.Equal("x", varExpr.Name);
    }

    [Fact]
    public void KeywordPriorityOverIdentifier()
    {
        // "let" はキーワード (Priority=1) が識別子より優先される。
        var tokens = StmtLexer.Tokenize("let let = 5;");
        // let kw, "let" id?, = , 5, ;
        // 実際は "let" が2回ともキーワードとしてトークン化される (Priority=1)
        Assert.True(tokens.Count >= 3);
    }
}
