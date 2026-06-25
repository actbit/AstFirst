using AstFirst;
using BenchmarkDotNet.Attributes;
using Perf.Grammars;

namespace Perf.Bench;

/// <summary>
/// 実行パフォーマンス計測: 字句解析のみ (Parser が内部で呼ぶ Lexer.Tokenize を独立計測)。
/// Parse に含まれるためデフォルト実行からは外し、--filter '*Tokenize*' で明示的に計測。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 2, iterationCount: 3)]
public class TokenizeBenchmarks
{
    [Params(BenchSize.Small, BenchSize.Medium, BenchSize.Large)]
    public BenchSize Size { get; set; }

    private string? _deepPrec, _wideRules, _manyTokens, _deepNest, _megaLang, _csharp;

    [GlobalSetup]
    public void Setup()
    {
        _deepPrec = InputGen.DeepPrec(Size == BenchSize.Small ? 100 : Size == BenchSize.Medium ? 10_000 : 100_000);
        _wideRules = InputGen.WideRules(Size == BenchSize.Small ? 1 : Size == BenchSize.Medium ? 100 : 1_000);
        _manyTokens = InputGen.ManyTokens(Size == BenchSize.Small ? 100 : Size == BenchSize.Medium ? 10_000 : 100_000);
        _deepNest = InputGen.DeepNest(Size == BenchSize.Small ? 100 : Size == BenchSize.Medium ? 1_000 : 5_000);
        _megaLang = InputGen.MegaLang(Size == BenchSize.Small ? 10 : Size == BenchSize.Medium ? 200 : 2_000);
        _csharp = InputGen.CSharp(Size == BenchSize.Small ? 2 : Size == BenchSize.Medium ? 50 : 500);
    }

    [Benchmark] public void Tokenize_DeepPrec() => PerfDeepPrec.PrecExprLexer.Tokenize(_deepPrec!);
    [Benchmark] public void Tokenize_WideRules() => PerfWideRules.WideProgramLexer.Tokenize(_wideRules!);
    [Benchmark] public void Tokenize_ManyTokens() => PerfManyTokens.TokenProgramLexer.Tokenize(_manyTokens!);
    [Benchmark] public void Tokenize_DeepNest() => PerfDeepNest.NestExprLexer.Tokenize(_deepNest!);
    [Benchmark] public void Tokenize_MegaLang() => PerfMegaLang.MegaProgramLexer.Tokenize(_megaLang!);
    [Benchmark] public void Tokenize_CSharp() => CSharpParser.CSharpCompilationUnitLexer.Tokenize(_csharp!);
}
