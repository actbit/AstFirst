using AstFirst;

namespace AstFirst.Tests.EndToEnd;

// このファイルの文法は SemanticContextIntegrationTests 用。Generator が
// テストプロジェクト内で SymStmtParser / SymStmtLexer を生成する。
// SemanticContext 引数は右辺から除外され、パーサから ctx が注入される ([Rule] static モデル)。

/// <summary>ctx 注入の E2E テスト用の文法。意味解析は [Enter] (2パス目 Walker) で行う。</summary>
[Grammar]
[Skip(@"\s+")]
public abstract partial class SymStmt : AstNode
{
    // --- 意味解析ルール ([Enter] で宣言チェック。ctx は BasicSemanticContext で書き込み可) ---
    [Enter] public static void EnterDecl(SymDecl n, SemanticContext ctx)
    {
        // OnReduce では読み取り専用 ctx しか渡されないため、宣言は Walker で行う。
        // ただし SemanticContext には WritableSymbols がないので、直接は宣言できない。
        // → このテストは OnReduce では ctx 書き換え不可であることを検証する用途に変更。
    }
}

/// <summary>let name; — 宣言。</summary>
public sealed partial class SymDecl : SymStmt
{
    public string Name { get; private set; } = "";
    [Rule]
    public static void Decl([Token(@"let", Priority = 1)] Token kw, [Token(@"[A-Za-z_]\w*")] Token nameTok, SemanticContext ctx, [Token(@";")] Token semi) { }
    partial void OnReduce(SemanticContext ctx)
    {
        Name = NameTok.Text;
        Span = NameTok.Span;
    }
}

/// <summary>use name; — 参照。</summary>
public sealed partial class SymUse : SymStmt
{
    public string Name { get; private set; } = "";
    [Rule]
    public static void Use([Token(@"use", Priority = 1)] Token kw, [Token(@"[A-Za-z_]\w*")] Token nameTok, SemanticContext ctx, [Token(@";")] Token semi) { }
    partial void OnReduce(SemanticContext ctx)
    {
        Name = NameTok.Text;
        Span = NameTok.Span;
        // Lookup は読み取り専用 ctx でも可能
        if (ctx.Symbols.Lookup(NameTok.Text) is null)
        {
            // 診断の追加は OnReduce では不可。Walker で行う必要がある。
            // ここでは Reject でパーサにフィードバックする (軽量 GLR の場合)。
        }
    }
}
