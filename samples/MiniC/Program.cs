using MiniC;

// 軽量C言語サンプル: Parse + 意味解析 (スコープ付きシンボル表で未宣言/二重宣言/スコープ外を検出)
Console.WriteLine("=== AstFirst MiniC Sample (Parse + Meaning Analysis) ===\n");

Run("正常系", """
    int x = 10 + 20 * 3;
    int y = x - 5;
    print(y);
""");

Run("未宣言参照", "print(x);");

Run("二重宣言", """
    int x;
    int x;
""");

Run("スコープ外参照", """
    {
        int inner;
    }
    print(inner);
""");

Run("シャドウイング (許容)", """
    int x;
    {
        int x;
        print(x);
    }
""");

Console.WriteLine("=== Done ===");

void Run(string title, string code)
{
    Console.WriteLine($"--- {title} ---");
    var result = ProgramParser.Parse(code);
    if (result.HasErrors)
    {
        Console.WriteLine("  構文エラー:");
        foreach (var err in result.Errors) Console.WriteLine($"    {err}");
    }
    var diagnostics = new SemanticAnalyzer().Analyze(result.Ast as MiniC.Program);
    if (diagnostics.Count == 0)
        Console.WriteLine("  意味解析: 診断なし (OK)");
    else
    {
        Console.WriteLine("  意味解析の診断:");
        foreach (var d in diagnostics) Console.WriteLine($"    {d.Severity}: {d.Message} @ {d.Span}");
    }
    Console.WriteLine();
}
