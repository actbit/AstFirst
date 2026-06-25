using CSharpParser;

// C# 完全文法パーサのサンプル。実コード断片をパースして AST を構築する (意味解析はしない)。
// AstFirst の LALR(1) で C# の構文 (式/文/型/宣言/メンバ、generic は Member の型のみ) を再現。
// 文法本体は GeneratedGrammar.cs (Perf.Grammars/CSharpFactory から生成、benchmark と同一)。
var input = """
using System;
using System.Collections.Generic;

namespace Sample {
    class Program {
        int count;
        List<int> items;
        Dictionary<string, int> map;
        static int Run(string[] args) {
            var x = 1 + 2 * 3;
            var y = x > 5 ? x : 0;
            if (x > 5) {
                x = x - 1;
            }
            while (x > 0) {
                x = x - 1;
            }
            return x;
        }
    }
}
""";
var result = CSharpCompilationUnitParser.Parse(input);
Console.WriteLine($"CSharpParser sample: HasErrors={result.HasErrors}, AstNull={result.Ast is null}");
foreach (var e in result.Errors) Console.WriteLine("  " + e);
