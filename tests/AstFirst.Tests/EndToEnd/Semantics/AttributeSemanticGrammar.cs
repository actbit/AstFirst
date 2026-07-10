using AstFirst;

namespace AstFirst.Tests.EndToEnd.Semantics;

// このファイルの文法は AttributeSemanticTests 用。Generator が AttrStmtParser / AttrStmtLexer / AttrStmtWalker を生成。
// [OnReduce] 属性で宣言 (1パス・reduce 時)、[Enter] 属性で参照解決 (2パス・Walker)。partial OnReduce は Name 設定のみ。

/// <summary>属性ベース意味解析 ([OnReduce]/[Enter]/[Exit]) の E2E テスト用文法ルート。</summary>
[Grammar]
[Skip(@"\s+")]
public abstract partial class AttrStmt : AstNode
{
    /// <summary>[OnReduce]: AttrDecl の reduce 時に宣言を登録 (partial OnReduce の直後・共存)。</summary>
    [OnReduce]
    public static void Declare(AttrDecl d, SemanticContext ctx)
    {
        if (!ctx.Symbols.TryDeclare(d.Name, d.Span, null, out _))
            ctx.Diagnostics.Error("'" + d.Name + "' は既に宣言されています", d.Span);
    }

    /// <summary>[Enter]: 2パス目で AttrUse に入る時に参照解決。ctx のキャストは Generator が自動挿入。</summary>
    [Enter]
    public static void ResolveUse(AttrUse u, SemanticContext ctx)
    {
        if (ctx.Symbols.Lookup(u.Name) is null)
            ctx.Diagnostics.Error("'" + u.Name + "' は宣言されていません", u.Span);
    }
}

/// <summary>let name; — 宣言。Name は partial OnReduce で設定、意味解析は [OnReduce] 属性。</summary>
public sealed partial class AttrDecl : AttrStmt
{
    public string Name { get; private set; } = "";
    [Rule]
    public static void Decl([Token(@"let", Priority = 1)] Token kw, [Token(@"[A-Za-z_]\w*")] Token nameTok, SemanticContext ctx, [Token(@";")] Token semi) { }
    partial void OnReduce(SemanticContext ctx) { Name = NameTok.Text; Span = NameTok.Span; }
}

/// <summary>use name; — 参照。Name は partial OnReduce、参照解決は [Enter] 属性。</summary>
public sealed partial class AttrUse : AttrStmt
{
    public string Name { get; private set; } = "";
    [Rule]
    public static void Use([Token(@"use", Priority = 1)] Token kw, [Token(@"[A-Za-z_]\w*")] Token nameTok, SemanticContext ctx, [Token(@";")] Token semi) { }
    partial void OnReduce(SemanticContext ctx) { Name = NameTok.Text; Span = NameTok.Span; }
}
