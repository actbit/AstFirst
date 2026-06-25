using AstFirst;
using AstFirst.Core.Parsing;
using AstFirst.Generator;
using BenchmarkDotNet.Attributes;
using Perf.Grammars;

namespace Perf.Bench;

/// <summary>
/// 代表ベンチ: 全文法パターンの Parse (実行) と Build (生成: ModelToTable) を Medium サイズ固定で計測。
/// 引数なし実行のデフォルト。Parse 6 + Build 6 = 12 ケース で SimpleJob 短縮により ~1-2分。
/// C# 文法の「パーサ生成時間 (Build_CSharp)」と「実行時間 (Parse_CSharp)」をここで分離計測 (意味解析なし)。
/// 全サイズ・Tokenize 含む全量は --filter '*' で BenchmarkSwitcher へ。
/// </summary>
[MemoryDiagnoser]
[SimpleJob(launchCount: 1, warmupCount: 2, iterationCount: 3)]
public class RepresentativeBenchmarks
{
    private string? _deepPrec, _wideRules, _manyTokens, _deepNest, _megaLang, _csharp;
    private GrammarModel? _deepPrecModel, _wideRulesModel, _manyTokensModel, _deepNestModel, _megaLangModel, _csharpModel;

    [GlobalSetup]
    public void Setup()
    {
        // Medium サイズ固定
        _deepPrec = InputGen.DeepPrec(10_000);
        _wideRules = InputGen.WideRules(100);
        _manyTokens = InputGen.ManyTokens(10_000);
        _deepNest = InputGen.DeepNest(1_000);
        _megaLang = InputGen.MegaLang(200);
        _csharp = InputGen.CSharp(50);

        Console.WriteLine($"# Representative (Medium): " +
            $"DeepPrec={_deepPrec!.Length:N0}, WideRules={_wideRules!.Length:N0}, " +
            $"ManyTokens={_manyTokens!.Length:N0}, DeepNest={_deepNest!.Length:N0}, " +
            $"MegaLang={_megaLang!.Length:N0}, CSharp={_csharp!.Length:N0}");

        Verify(PerfDeepPrec.PrecExprParser.Parse(_deepPrec), "DeepPrec");
        Verify(PerfWideRules.WideProgramParser.Parse(_wideRules), "WideRules");
        Verify(PerfManyTokens.TokenProgramParser.Parse(_manyTokens), "ManyTokens");
        Verify(PerfDeepNest.NestExprParser.Parse(_deepNest), "DeepNest");
        Verify(PerfMegaLang.MegaProgramParser.Parse(_megaLang), "MegaLang");
        Verify(CSharpParser.CSharpCompilationUnitParser.Parse(_csharp), "CSharp");

        _deepPrecModel = DeepPrecFactory.Create().ToModel();
        _wideRulesModel = WideRulesFactory.Create().ToModel();
        _manyTokensModel = ManyTokensFactory.Create().ToModel();
        _deepNestModel = DeepNestFactory.Create().ToModel();
        _megaLangModel = MegaLangFactory.Create().ToModel();
        _csharpModel = CSharpFactory.Create().ToModel();
    }

    // --- 実行: Parse (字句解析+構文解析+AST 構築) ---
    [Benchmark] public void Parse_DeepPrec() => PerfDeepPrec.PrecExprParser.Parse(_deepPrec!);
    [Benchmark] public void Parse_WideRules() => PerfWideRules.WideProgramParser.Parse(_wideRules!);
    [Benchmark] public void Parse_ManyTokens() => PerfManyTokens.TokenProgramParser.Parse(_manyTokens!);
    [Benchmark] public void Parse_DeepNest() => PerfDeepNest.NestExprParser.Parse(_deepNest!);
    [Benchmark] public void Parse_MegaLang() => PerfMegaLang.MegaProgramParser.Parse(_megaLang!);
    [Benchmark] public void Parse_CSharp() => CSharpParser.CSharpCompilationUnitParser.Parse(_csharp!);

    // --- 生成: ModelToTable.Build (GrammarModel → LALR テーブル構築の純粋な時間) ---
    [Benchmark] public LalrTable Build_DeepPrec() => ModelToTable.Build(_deepPrecModel!);
    [Benchmark] public LalrTable Build_WideRules() => ModelToTable.Build(_wideRulesModel!);
    [Benchmark] public LalrTable Build_ManyTokens() => ModelToTable.Build(_manyTokensModel!);
    [Benchmark] public LalrTable Build_DeepNest() => ModelToTable.Build(_deepNestModel!);
    [Benchmark] public LalrTable Build_MegaLang() => ModelToTable.Build(_megaLangModel!);
    [Benchmark] public LalrTable Build_CSharp() => ModelToTable.Build(_csharpModel!);

    private static void Verify(ParseResult result, string name)
    {
        if (result.HasErrors || result.Ast is null)
            throw new InvalidOperationException(
                $"正確性検証失敗: {name} (HasErrors={result.HasErrors}, errors={result.Errors.Count}: {string.Join("; ", result.Errors.Take(3))})");
    }
}
