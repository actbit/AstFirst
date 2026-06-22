using System.Reflection;
using BenchmarkDotNet.Running;
using Perf.Bench;

// Release ビルドで実行: dotnet run -c Release --project samples/Perf/Perf.Bench
//   引数なし        : 全ベンチ実行 (Parse/Tokenize × Small/Medium/Large + TableBuild)
//   --filter '*X*'  : 特定ベンチのみ (例: --filter '*TableBuild*' は生成のみ、速い)
//   --filter '*Small*' は効かない (Size は Params)。メソッド名で絞る。
// 結果は BenchmarkDotNet が BenchmarkDotNet.Artifacts/ に Markdown/CSV で出力。
Console.WriteLine("=== AstFirst 大規模文法ベンチマーク ===");
Console.WriteLine();

if (args.Length == 0)
{
    BenchmarkRunner.Run<ParseTokenizeBenchmarks>();
    BenchmarkRunner.Run<TableBuildBenchmarks>();
}
else
{
    BenchmarkSwitcher.FromAssembly(Assembly.GetEntryAssembly()!).Run(args);
}
