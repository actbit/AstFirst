using Microsoft.CodeAnalysis;

namespace AstFirst.Generator;

/// <summary>AstFirst ジェネレータの Diagnostic 定義。</summary>
internal static class DiagnosticDescriptors
{
    /// <summary>
    /// LALR(1) テーブル構築で優先度/結合性では解決できなかったコンフリクト
    /// (shift-reduce / reduce-reduce)。文法が構文的に曖昧であることを示す。
    /// </summary>
    public static readonly DiagnosticDescriptor GrammarConflict = new(
        id: "ASTF001",
        title: "Grammar conflict / 文法コンフリクト",
        messageFormat: "{0}",
        category: "AstFirst",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Unresolved shift-reduce or reduce-reduce conflict that precedence/associativity could not resolve. The grammar is syntactically ambiguous; specify precedence/associativity via [Precedence] or revise the grammar.");
}
