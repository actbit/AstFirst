using PerfDeepNest;

// smoke test: 深いネスト文法がパース可能か。
var input = "(((((1)))))";
var result = NestExprParser.Parse(input);
Console.WriteLine($"Perf.DeepNest smoke: HasErrors={result.HasErrors}, AstNull={result.Ast is null}, input='{input}'");
foreach (var e in result.Errors) Console.WriteLine("  " + e);
