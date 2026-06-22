using PerfDeepPrec;

// smoke test: 大規模文法 (深い優先度階層) がパース可能か。Generator が正常に Parser を生成したかの確認。
var input = "1 + 2 * 3 - 4 / 5 % 6";
var result = PrecExprParser.Parse(input);
Console.WriteLine($"Perf.DeepPrec smoke: HasErrors={result.HasErrors}, AstNull={result.Ast is null}, input='{input}'");
foreach (var e in result.Errors) Console.WriteLine("  " + e);
