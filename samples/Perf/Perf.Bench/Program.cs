using System.Reflection;
using BenchmarkDotNet.Running;
using Perf.Bench;

// Release ビルドで実行: dotnet run -c Release --project samples/Perf/Perf.Bench
//   引数なし (既定): 代表ベンチ (Parse + Build × 6文法、Medium固定)。~2分。
//                    C# の生成時間 (Build_CSharp) と実行時間 (Parse_CSharp) を確認。
//   -- --filter '*'              全量 (Parse/Tokenize × 3サイズ + TableBuild)。時間かかる。
//   -- --filter '*TableBuild*'   生成（テーブル構築）のみ。
Console.WriteLine("=== AstFirst 大規模文法ベンチマーク ===");
Console.WriteLine();

if (args.Length == 0)
{
    BenchmarkRunner.Run<RepresentativeBenchmarks>();
}
else
{
    BenchmarkSwitcher.FromAssembly(Assembly.GetEntryAssembly()!).Run(args);
}
