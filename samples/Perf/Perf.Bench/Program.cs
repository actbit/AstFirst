using System.Diagnostics;
using System.Reflection;
using BenchmarkDotNet.Running;
using Perf.Bench;
using Perf.Grammars;

// Release ビルドで実行: dotnet run -c Release --project samples/Perf/Perf.Bench
//   引数なし (既定): 代表ベンチ (Parse + Build × 6文法、Medium固定)。~2分。
//                    C# の生成時間 (Build_CSharp) と実行時間 (Parse_CSharp) を確認。
//   -- direct    : BenchmarkDotNet を経由しない直接計測 (Stopwatch + GC)。
//                  Windows Defender 等がベンチ子プロセスを遮断する環境向け。
//   -- --filter '*'              全量 (Parse/Tokenize × 3サイズ + TableBuild)。時間かかる。
//   -- --filter '*TableBuild*'   生成（テーブル構築）のみ。
Console.WriteLine("=== AstFirst 大規模文法ベンチマーク ===");
Console.WriteLine();

if (args.Length > 0 && args[0] == "direct")
{
    DirectBench.Run();
}
else if (args.Length == 0)
{
    BenchmarkRunner.Run<RepresentativeBenchmarks>();
}
else
{
    BenchmarkSwitcher.FromAssembly(Assembly.GetEntryAssembly()!).Run(args);
}

/// <summary>直接計測 (Stopwatch + GC.GetAllocatedBytesForCurrentThread)。ベンチ子プロセス遮断回避用。</summary>
public static class DirectBench
{
    public static void Run()
    {
        var csharp = InputGen.CSharp(50);
        var megaLang = InputGen.MegaLang(200);
        var deepNest = InputGen.DeepNest(1000);
        var deepPrec = InputGen.DeepPrec(10_000);

        // warmup (JIT 最適化を安定させる)
        for (int w = 0; w < 5; w++)
        {
            CSharpParser.CSharpCompilationUnitParser.Parse(csharp);
            PerfMegaLang.MegaProgramParser.Parse(megaLang);
            PerfDeepNest.NestExprParser.Parse(deepNest);
            PerfDeepPrec.PrecExprParser.Parse(deepPrec);
        }

        Console.WriteLine("文法,       時間(ms),  アロケ(KB),  入力(bytes)");
        Measure("CSharp", csharp, CSharpParser.CSharpCompilationUnitParser.Parse);
        Measure("MegaLang", megaLang, PerfMegaLang.MegaProgramParser.Parse);
        Measure("DeepNest", deepNest, PerfDeepNest.NestExprParser.Parse);
        Measure("DeepPrec", deepPrec, PerfDeepPrec.PrecExprParser.Parse);
    }

    private static void Measure(string name, string input, System.Func<string, AstFirst.ParseResult> parse)
    {
        var sw = Stopwatch.StartNew();
        long before = System.GC.GetAllocatedBytesForCurrentThread();
        parse(input);
        long after = System.GC.GetAllocatedBytesForCurrentThread();
        sw.Stop();
        Console.WriteLine($"{name,-10},  {sw.Elapsed.TotalMilliseconds,7:F3},  {(after - before) / 1024.0,9:F0},  {input.Length,10}");
    }
}
