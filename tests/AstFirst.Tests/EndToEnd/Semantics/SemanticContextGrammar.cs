using AstFirst;

namespace AstFirst.Tests.EndToEnd;

// このファイルの文法は SemanticContextIntegrationTests 用。Generator が
// テストプロジェクト内で SymStmtParser / SymStmtLexer を生成する。
// SemanticContext 引数は右辺から除外され、パーサから ctx が注入される ([Rule] static モデル)。

/// <summary>ctx 注入の E2E テスト用の文法 (1パス意味解析・OnReduce で診断)。</summary>
[Grammar]
[Skip(@"\s+")]
public abstract partial class SymStmt : AstNode { }

/// <summary>let name; — 宣言。同一スコープの重複で診断。</summary>
public sealed partial class SymDecl : SymStmt
{
    public string Name { get; private set; } = "";
    [Rule]
    public static void Decl([Token(@"let", Priority = 1)] Token kw, [Token(@"[A-Za-z_]\w*")] Token nameTok, SemanticContext ctx, [Token(@";")] Token semi) { }
    partial void OnReduce(SemanticContext ctx)
    {
        Name = NameTok.Text;
        Span = NameTok.Span;
        if (!ctx.Symbols.TryDeclare(NameTok.Text, NameTok.Span, null, out _))
            ctx.Diagnostics.Error($"'{NameTok.Text}' は既に宣言されています", NameTok.Span);
    }
}

/// <summary>use name; — 参照。未宣言で診断。</summary>
public sealed partial class SymUse : SymStmt
{
    public string Name { get; private set; } = "";
    [Rule]
    public static void Use([Token(@"use", Priority = 1)] Token kw, [Token(@"[A-Za-z_]\w*")] Token nameTok, SemanticContext ctx, [Token(@";")] Token semi) { }
    partial void OnReduce(SemanticContext ctx)
    {
        Name = NameTok.Text;
        Span = NameTok.Span;
        if (ctx.Symbols.Lookup(NameTok.Text) is null)
            ctx.Diagnostics.Error($"'{NameTok.Text}' は宣言されていません", NameTok.Span);
    }
}
