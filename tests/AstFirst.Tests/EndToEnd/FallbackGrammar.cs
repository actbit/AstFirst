using AstFirst;

namespace AstFirst.Tests.EndToEnd.FallbackTest;

// Accept/Reject + フォールバックの E2E 検証用文法。Generator が FNodeParser を生成。
// 同一右辺 "a b" の 2 規則 (Foo/Bar) は reduce-reduce コンフリクトになり、
// 片方がデフォルト・他方がフォールバック候補になる。
// OnReduce の Reject() で、デフォルトが拒否されたらもう一方へ切り替わることを検証する。

/// <summary>フォールバック検証用のルート。</summary>
[Grammar]
[Skip(@"\s+")]
public abstract partial class FNode : AstNode { }

/// <summary>規則 Foo : a b。PreferBar のとき Reject して Bar に道を譲る。</summary>
public sealed partial class Foo : FNode
{
    [Rule]
    public static void Make([Token("a")] Token a, [Token("b")] Token b, SemanticContext ctx) { }
    partial void OnReduce(SemanticContext ctx)
    {
        if (FallbackFlags.PreferBar) Reject();
    }
}

/// <summary>規則 Bar : a b。PreferBar でないとき Reject して Foo に道を譲る。</summary>
public sealed partial class Bar : FNode
{
    [Rule]
    public static void Make([Token("a")] Token a, [Token("b")] Token b, SemanticContext ctx) { }
    partial void OnReduce(SemanticContext ctx)
    {
        if (!FallbackFlags.PreferBar) Reject();
    }
}

/// <summary>どちらの規則を受領するかの切り替えフラグ (テスト間で設定)。</summary>
public static class FallbackFlags
{
    public static bool PreferBar;
}
