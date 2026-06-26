using AstFirst.Tests.EndToEnd.FallbackTest;

namespace AstFirst.Tests.EndToEnd;

/// <summary>
/// 新モデルの核心機能: OnReduce で Reject() すると優先度順の別候補 (別規則/shift) へフォールバックする。
/// 同一右辺 "a b" の reduce-reduce コンフリクトで、Reject によって結果の AST 型が切り替わることを検証。
/// 順序に依存しないよう、両規則がフラグに応じて Reject し、必ず一方だけが受領されるようにしている。
/// </summary>
public class FallbackTests
{
    [Fact]
    public void PreferBarFalse_ResolvesToFoo()
    {
        FallbackFlags.PreferBar = false;
        var r = FNodeParser.Parse("a b");
        Assert.False(r.HasErrors, string.Join("; ", r.Errors));
        Assert.IsType<Foo>(r.Ast);
    }

    [Fact]
    public void PreferBarTrue_ResolvesToBar()
    {
        FallbackFlags.PreferBar = true;
        var r = FNodeParser.Parse("a b");
        Assert.False(r.HasErrors, string.Join("; ", r.Errors));
        Assert.IsType<Bar>(r.Ast);
    }
}
