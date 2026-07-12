using AstFirst;

namespace AstFirst.Tests.EndToEnd.Semantics;

/// <summary>属性ベース意味解析 ([Enter]/[Exit]) の E2E テスト用文法ルート。
/// OnReduce は読み取り専用 ctx (ノードローカル初期化のみ)。
/// 宣言・参照解決は [Enter] (2パス目 Walker、BasicSemanticContext で書き込み可)。</summary>
[Grammar]
[Skip(@"\s+")]
public abstract partial class AttrStmt : AstNode
{
    /// <summary>[Enter]: AttrDecl の宣言登録 (2パス目・Walker)。</summary>
    [Enter]
    public static void Declare(AttrDecl d, BasicSemanticContext ctx)
    {
        if (!ctx.WritableSymbols.TryDeclare(d.Name, d.Span, null, out _))
            ctx.Diagnostics.Error("'" + d.Name + "' は既に宣言されています", d.Span);
    }

    /// <summary>[Enter]: AttrUse の参照解決 (2パス目・Walker)。</summary>
    [Enter]
    public static void ResolveUse(AttrUse u, BasicSemanticContext ctx)
    {
        if (ctx.WritableSymbols.Lookup(u.Name) is null)
            ctx.Diagnostics.Error("'" + u.Name + "' は宣言されていません", u.Span);
    }
}

/// <summary>let name; — 宣言。Name は OnReduce (読み取り専用 ctx)。</summary>
public sealed partial class AttrDecl : AttrStmt
{
    public string Name { get; private set; } = "";
    [Rule]
    public static void Decl([Token(@"let", Priority = 1)] Token kw, [Token(@"[A-Za-z_]\w*")] Token nameTok, SemanticContext ctx, [Token(@";")] Token semi) { }
    partial void OnReduce(SemanticContext ctx) { Name = NameTok.Text; Span = NameTok.Span; }
}

/// <summary>use name; — 参照。Name は OnReduce、参照解決は [Enter]。</summary>
public sealed partial class AttrUse : AttrStmt
{
    public string Name { get; private set; } = "";
    [Rule]
    public static void Use([Token(@"use", Priority = 1)] Token kw, [Token(@"[A-Za-z_]\w*")] Token nameTok, SemanticContext ctx, [Token(@";")] Token semi) { }
    partial void OnReduce(SemanticContext ctx) { Name = NameTok.Text; Span = NameTok.Span; }
}
