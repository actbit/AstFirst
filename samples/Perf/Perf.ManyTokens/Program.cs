using PerfManyTokens;

// smoke test: 大規模文法 (多数のトークン) がパース可能か。キーワードと識別子の優先度解決を含む。
var input = "kw0 kw1 kw2 ident1 kw99 myvar";
var result = TokenProgramParser.Parse(input);
Console.WriteLine($"Perf.ManyTokens smoke: HasErrors={result.HasErrors}, AstNull={result.Ast is null}, input='{input}'");
foreach (var e in result.Errors) Console.WriteLine("  " + e);
