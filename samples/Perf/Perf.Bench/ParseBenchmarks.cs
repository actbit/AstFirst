using AstFirst;
using BenchmarkDotNet.Attributes;
using Perf.Grammars;

namespace Perf.Bench;

/// <summary>
/// 実行パフォーマンス計測: 各文法パターンの Parse (字句解析+構文解析+AST 構築)。
/// [Params] で入力サイズを Small/Medium/Large に切り替え、規模に対するスケールを見る。
/// Tokenize (字句解析のみ) は TokenizeBenchmarks に分離 (Parse に含まれるため)。
/// [SimpleJob(launchCount:1, warmupCount:2, iterationCount:3)] で DefaultJob より計測時間を短縮。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 2, iterationCount: 3)]
public class ParseBenchmarks
{
    [Params(BenchSize.Small, BenchSize.Medium, BenchSize.Large)]
    public BenchSize Size { get; set; }

    private string? _deepPrec, _wideRules, _manyTokens, _deepNest, _megaLang, _csharp;

    [GlobalSetup]
    public void Setup()
    {
        _deepPrec = InputGen.DeepPrec(Pick(100, 10_000, 100_000));
        _wideRules = InputGen.WideRules(Pick(1, 100, 1_000));
        _manyTokens = InputGen.ManyTokens(Pick(100, 10_000, 100_000));
        _deepNest = InputGen.DeepNest(Pick(100, 1_000, 5_000));
        _megaLang = InputGen.MegaLang(Pick(10, 200, 2_000));
        _csharp = InputGen.CSharp(Pick(2, 50, 500));

        Console.WriteLine($"# Input size ({Size}): " +
            $"DeepPrec={_deepPrec!.Length:N0}, WideRules={_wideRules!.Length:N0}, " +
            $"ManyTokens={_manyTokens!.Length:N0}, DeepNest={_deepNest!.Length:N0}, " +
            $"MegaLang={_megaLang!.Length:N0}, CSharp={_csharp!.Length:N0}");

        // 正確性検証: 各入力がエラーなくパースされること (壊れた入力で計測しないよう担保)。
        Verify(PerfDeepPrec.PrecExprParser.Parse(_deepPrec), "DeepPrec");
        Verify(PerfWideRules.WideProgramParser.Parse(_wideRules), "WideRules");
        Verify(PerfManyTokens.TokenProgramParser.Parse(_manyTokens), "ManyTokens");
        Verify(PerfDeepNest.NestExprParser.Parse(_deepNest), "DeepNest");
        Verify(PerfMegaLang.MegaProgramParser.Parse(_megaLang), "MegaLang");
        Verify(CSharpParser.CSharpCompilationUnitParser.Parse(_csharp), "CSharp");
    }

    // --- Parse: 字句解析 + 構文解析 + AST 構築 ---
    [Benchmark] public void Parse_DeepPrec() => PerfDeepPrec.PrecExprParser.Parse(_deepPrec!);
    [Benchmark] public void Parse_WideRules() => PerfWideRules.WideProgramParser.Parse(_wideRules!);
    [Benchmark] public void Parse_ManyTokens() => PerfManyTokens.TokenProgramParser.Parse(_manyTokens!);
    [Benchmark] public void Parse_DeepNest() => PerfDeepNest.NestExprParser.Parse(_deepNest!);
    [Benchmark] public void Parse_MegaLang() => PerfMegaLang.MegaProgramParser.Parse(_megaLang!);
    [Benchmark] public void Parse_CSharp() => CSharpParser.CSharpCompilationUnitParser.Parse(_csharp!);

    private int Pick(int small, int medium, int large)
        => Size switch { BenchSize.Small => small, BenchSize.Medium => medium, _ => large };

    private static void Verify(ParseResult result, string name)
    {
        if (result.HasErrors || result.Ast is null)
            throw new InvalidOperationException(
                $"正確性検証失敗: {name} (HasErrors={result.HasErrors}, errors={result.Errors.Count}: {string.Join("; ", result.Errors.Take(3))})");
    }
}
