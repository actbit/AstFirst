using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AstFirst.Generator;

/// <summary>
/// AstFirst のレクサ/パーサをコンパイル時に生成する Source Generator のエントリポイント。
/// [Grammar] ルートを抽出し、ModelExtraction でモデル化する (コード生成は 3c 以降)。
/// </summary>
[Generator]
public sealed class ParserGenerator : IIncrementalGenerator
{
    private const string GrammarAttributeFullName = "AstFirst.GrammarAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GrammarAttributeFullName,
                predicate: (node, _) => node is ClassDeclarationSyntax,
                transform: (ctx, _) => ModelExtraction.Extract(ctx))
            .Where(m => m is not null)
            .Select((m, _) => m!);

        // Lexer と Parser を生成。
        context.RegisterSourceOutput(models.Collect(), (spc, modelArray) =>
        {
            foreach (var model in modelArray)
            {
                var (ns, typeName) = CodeEmitter.SplitFullName(model.RootTypeFullName);
                var suffix = string.IsNullOrEmpty(model.Mode) ? "" : "_" + model.Mode;

                // テーブルと DFA を1回だけ構築し、Lexer/Parser の生成で共有 (重複ビルドを避ける)。
                var (grammar, table) = ModelToTable.BuildWithGrammar(model);
                var dfa = ModelToDfa.Build(model, out var rules);

                // 優先度/結合性で解決できなかったコンフリクトを警告で報告 (構文的曖昧さの可視化)。
                foreach (var conflict in table.Conflicts)
                    spc.ReportDiagnostic(Microsoft.CodeAnalysis.Diagnostic.Create(DiagnosticDescriptors.GrammarConflict, model.RootLocation, conflict.Description));

                // 到達不能/未定義非終端を警告で報告 (規則の過不足の可視化)。
                foreach (var nt in grammar.UnreachableNonTerminals)
                    spc.ReportDiagnostic(Microsoft.CodeAnalysis.Diagnostic.Create(DiagnosticDescriptors.UnreachableNonTerminal, model.RootLocation, $"到達不能な非終端 '{nt.Name}' があります (開始記号から到達できません) / Unreachable nonterminal '{nt.Name}' (no path from the start symbol)"));
                foreach (var nt in grammar.UndefinedNonTerminals)
                    spc.ReportDiagnostic(Microsoft.CodeAnalysis.Diagnostic.Create(DiagnosticDescriptors.UndefinedNonTerminal, model.RootLocation, $"未定義の非終端 '{nt.Name}' があります (右辺で参照されていますが規則がありません) / Undefined nonterminal '{nt.Name}' (referenced in a RHS but has no productions)"));
                foreach (var tdw in model.TokenDerivedWarnings)
                    spc.ReportDiagnostic(Microsoft.CodeAnalysis.Diagnostic.Create(DiagnosticDescriptors.TokenDerivedNoStringCtor, model.RootLocation, $"Token 派生型 '{tdw}' に (string) コンストラクタがありません (new DerivedType(token.Text) の生成に必要) / Token-derived type '{tdw}' has no (string) constructor"));

                spc.AddSource(typeName + suffix + "Lexer.g.cs", CodeEmitter.EmitLexer(model, dfa, rules, typeName + suffix + "Lexer", ns));
                spc.AddSource(typeName + suffix + "Parser.g.cs", ParserEmitter.EmitParser(model, grammar, table, rules, ns));
                // 各ノードの partial (子プロパティ + OnReduce/OnSecondPass 宣言 + partial コンストラクタ)。
                // [Rule] を持つ抽象基底 (中間抽象のプロパティ宣言) は protected コンストラクタ生成のため partial 必要。
                // [Rule] のない抽象クラスはフィールド/コンストラクタがないので partial 不要。
                foreach (var node in model.Nodes)
                {
                    if (node.IsAbstract && node.Rules.Count == 0) continue;
                    var simple = CodeEmitter.SplitFullName(node.FullName).type;
                    spc.AddSource(typeName + suffix + "_" + simple + ".partial.g.cs", ParserEmitter.EmitPartial(model, node, ns));
                }
            }
        });
    }
}
