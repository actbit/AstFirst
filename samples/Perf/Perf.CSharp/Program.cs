using CSharpParser;
// C# 完全文法 (365 規則) の構文解析デモ。意味解析なし (AST 構築まで)。
// 実行: dotnet run --project samples/Perf/Perf.CSharp
var input = "class C { void M() { var x = 1; if (x > 0) { x = x - 1; } return x; } }";
var result = CSharpCompilationUnitParser.Parse(input);
Console.WriteLine($"Input:     {input}");
Console.WriteLine($"HasErrors={result.HasErrors}, AstNull={result.Ast is null}");
foreach (var e in result.Errors)
    Console.WriteLine("  " + e);
if (!result.HasErrors && result.Ast is not null)
    Console.WriteLine($"OK: AST 構築成功 ({input.Length} chars)");
