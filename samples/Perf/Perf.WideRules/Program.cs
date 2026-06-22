using PerfWideRules;

// smoke test: 大規模文法 (幅広の規則数) がパース可能か。
var input = "s0; s1; s2; s3; s4; s10; s99;";
var result = WideProgramParser.Parse(input);
Console.WriteLine($"Perf.WideRules smoke: HasErrors={result.HasErrors}, AstNull={result.Ast is null}, input='{input}'");
foreach (var e in result.Errors) Console.WriteLine("  " + e);
