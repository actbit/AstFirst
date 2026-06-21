using Microsoft.CodeAnalysis;

namespace AstFirst.Generator;

/// <summary>
/// AstFirst のレクサ/パーサをコンパイル時に生成する Source Generator のエントリポイント。
/// フェーズ0では空実装（何も生成しない）。フェーズ3以降で
/// 「[Grammar] ルート抽出 → Core で Grammar+Dfa 構築 → レクサ/パーサ C# 生成」を実装する。
/// </summary>
[Generator]
public sealed class ParserGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // フェーズ3で実装する。
    }
}
