using System.Collections.Generic;
using AstFirst.Core.Lexing;
using AstFirst.Core.Parsing;

namespace AstFirst.Generator;

/// <summary>
/// LightGlr モード専用のパーサコード生成エミッタ。
/// フェーズ1では配線のみ: 既存の <see cref="ParserEmitter"/> に委譲し、ParseMode の分岐と後方互換性を検証する。
/// フェーズ3でテーブル直列化 + <c>LightGlrDriver</c> 呼出 + COW リスト reduce を本実装する。
/// </summary>
internal static class GlrParserEmitter
{
    public static string EmitParser(GrammarModel model, Grammar grammar, LalrTable table, IReadOnlyList<LexerRule> rules, string ns)
        => ParserEmitter.EmitParser(model, grammar, table, rules, ns);

    public static string EmitPartial(GrammarModel model, NodeModel node, string ns)
        => ParserEmitter.EmitPartial(model, node, ns);
}
