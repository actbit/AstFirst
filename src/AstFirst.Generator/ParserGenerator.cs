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

        // フェーズ (3c) でテーブル構築 + レクサ/パーサ C# コード生成を行う。
        context.RegisterSourceOutput(models.Collect(), (spc, _) => { });
    }
}
