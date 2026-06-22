using PerfMegaLang;

// smoke test: 総合大規模文法がパース可能か。宣言・式・代入・ネストを含む。
var input = """
decl0 ;
x = 1 + 2 * 3 ;
y = (x - 4) ;
""";
var result = MegaProgramParser.Parse(input);
Console.WriteLine($"Perf.MegaLang smoke: HasErrors={result.HasErrors}, AstNull={result.Ast is null}");
foreach (var e in result.Errors) Console.WriteLine("  " + e);
