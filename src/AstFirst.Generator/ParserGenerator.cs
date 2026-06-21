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
                spc.AddSource(typeName + "Lexer.g.cs", CodeEmitter.EmitLexer(model, typeName + "Lexer", ns));
                spc.AddSource(typeName + "Parser.g.cs", ParserEmitter.EmitParser(model, ns));
            }
        });
    }
}
