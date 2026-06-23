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

    /// <summary>到達不能非終端 (規則はあるが開始記号から到達不能)。不要な規則の可能性。</summary>
    public static readonly DiagnosticDescriptor UnreachableNonTerminal = new(
        id: "ASTF002",
        title: "Unreachable nonterminal / 到達不能非終端",
        messageFormat: "{0}",
        category: "AstFirst",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A nonterminal has productions but is unreachable from the start symbol. Likely an unused rule.");

    /// <summary>未定義非終端 (右辺で参照されるが規則がない)。パーサ生成バグや文法 typo の兆候。</summary>
    public static readonly DiagnosticDescriptor UndefinedNonTerminal = new(
        id: "ASTF003",
        title: "Undefined nonterminal / 未定義非終端",
        messageFormat: "{0}",
        category: "AstFirst",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A nonterminal is referenced in a RHS but has no productions. Often a sign of a generator rule-generation bug or a typo.");
}
