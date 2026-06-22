using System.IO;
using AstFirst.Generator;
using Perf.Grammars;

// 各ファクトリから C# 文法ソース (GeneratedGrammar.cs) を文法プロジェクトに書き出す。
// 同時に GrammarModel で LALR テーブルを構築し、状態数・衝突を検証する。
// 実行: dotnet run --project samples/Perf/Perf.Gen
var root = FindCsprojDir(); // Perf.Gen.csproj があるディレクトリ (cwd に依存しない)
Console.WriteLine($"Perf.Gen: 基準ディレクトリ = {root}");
Console.WriteLine();

Emit(DeepPrecFactory.Create(), "Perf.DeepPrec");
Emit(WideRulesFactory.Create(), "Perf.WideRules");
Emit(ManyTokensFactory.Create(), "Perf.ManyTokens");
Emit(DeepNestFactory.Create(), "Perf.DeepNest");
Emit(MegaLangFactory.Create(), "Perf.MegaLang");

void Emit(GrammarSpec spec, string projectDirName)
{
    var model = spec.ToModel();
    var table = ModelToTable.Build(model);
    var path = Path.Combine(root, "..", projectDirName, "GeneratedGrammar.cs");
    var full = Path.GetFullPath(path);
    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
    var cs = spec.ToCsSource();
    File.WriteAllText(full, cs);
    var lines = cs.Count(c => c == '\n') + 1;
    Console.WriteLine($"=== {projectDirName} ({spec.Namespace}) ===");
    Console.WriteLine($"  Productions={table.Grammar.Productions.Count}, Symbols={table.SymbolCount}, States={table.StateCount}, Conflicts={table.Conflicts.Count}");
    Console.WriteLine($"  -> {full} ({new FileInfo(full).Length:N0} bytes, {lines} lines)");
}

static string FindCsprojDir()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Perf.Gen.csproj")))
        dir = dir.Parent;
    return dir?.FullName ?? AppContext.BaseDirectory;
}
