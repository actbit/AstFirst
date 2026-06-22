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
                    spc.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.GrammarConflict, model.RootLocation, conflict.Description));

                spc.AddSource(typeName + suffix + "Lexer.g.cs", CodeEmitter.EmitLexer(model, dfa, rules, typeName + suffix + "Lexer", ns));
                spc.AddSource(typeName + suffix + "Parser.g.cs", ParserEmitter.EmitParser(model, grammar, table, rules, ns));
                spc.AddSource(typeName + suffix + "Listener.g.cs", ListenerEmitter.EmitListener(model, ns));
            }
        });
    }
}
