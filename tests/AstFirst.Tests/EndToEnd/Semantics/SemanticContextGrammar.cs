using AstFirst;

namespace AstFirst.Tests.EndToEnd;

// このファイルの文法は SemanticContextIntegrationTests 用。Generator が
// テストプロジェクト内で SymStmtParser / SymStmtLexer を生成する。
// SemanticContext 引数は右辺から除外され、パーサから ctx が注入される。

/// <summary>ctx 注入の E2E テスト用の文法 (1パス意味解析)。</summary>
[Grammar]
[Skip(@"\s+")]
public abstract class SymStmt : AstNode { }

/// <summary>let name; — 宣言。同一スコープの重複で診断。</summary>
public sealed class SymDecl : SymStmt
{
    public string Name { get; }
    public SymDecl([Pattern(@"let", Priority = 1)] Token kw, [Pattern(@"[A-Za-z_]\w*")] Token name, SemanticContext ctx, [Pattern(@";")] Token semi)
    {
        Name = name.Text;
        Span = name.Span;
        if (!ctx.Symbols.TryDeclare(name.Text, name.Span, null, out _))
            ctx.Diagnostics.Error($"'{name.Text}' は既に宣言されています", name.Span);
    }
}

/// <summary>use name; — 参照。未宣言で診断。</summary>
public sealed class SymUse : SymStmt
{
    public string Name { get; }
    public SymUse([Pattern(@"use", Priority = 1)] Token kw, [Pattern(@"[A-Za-z_]\w*")] Token name, SemanticContext ctx, [Pattern(@";")] Token semi)
    {
        Name = name.Text;
        Span = name.Span;
        if (ctx.Symbols.Lookup(name.Text) is null)
            ctx.Diagnostics.Error($"'{name.Text}' は宣言されていません", name.Span);
    }
}
