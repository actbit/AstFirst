using AstFirst.Core.Parsing;
using AstFirst.Generator;
using BenchmarkDotNet.Attributes;
using Perf.Grammars;

namespace Perf.Bench;

/// <summary>
/// 生成パフォーマンス計測 (精密): GrammarModel → LALR テーブル構築の純粋な時間。
/// Generator プロセス外で Compile Include した ModelToTable.Build を直接呼ぶ
/// (ビルド全体のノイズを除いた、テーブル構築アルゴリズム自体の速度)。
/// </summary>
[MemoryDiagnoser]
public class TableBuildBenchmarks
{
    private GrammarModel? _deepPrec, _wideRules, _manyTokens, _deepNest, _megaLang;

    [GlobalSetup]
    public void Setup()
    {
        _deepPrec = DeepPrecFactory.Create().ToModel();
        _wideRules = WideRulesFactory.Create().ToModel();
        _manyTokens = ManyTokensFactory.Create().ToModel();
        _deepNest = DeepNestFactory.Create().ToModel();
        _megaLang = MegaLangFactory.Create().ToModel();
    }

    [Benchmark] public LalrTable Build_DeepPrec() => ModelToTable.Build(_deepPrec!);
    [Benchmark] public LalrTable Build_WideRules() => ModelToTable.Build(_wideRules!);
    [Benchmark] public LalrTable Build_ManyTokens() => ModelToTable.Build(_manyTokens!);
    [Benchmark] public LalrTable Build_DeepNest() => ModelToTable.Build(_deepNest!);
    [Benchmark] public LalrTable Build_MegaLang() => ModelToTable.Build(_megaLang!);
}
