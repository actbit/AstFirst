using MiniC;

// 軽量C言語サンプル: Parse + 意味解析 (スコープ管理 + シンボル解決 + 型チェック)
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

Run("型エラー: if の条件が int", "if (1) print(1);");

Run("型エラー: int 変数に bool を代入", "int x; x = true;");

Run("型OK: if の条件が bool", "if (true) print(1);");

Console.WriteLine("=== Done ===");

void Run(string title, string code)
{
    Console.WriteLine($"--- {title} ---");
    var result = ProgramParser.Parse(code, new MiniCContext());
    if (result.Errors.Count > 0)
    {
        Console.WriteLine("  構文エラー:");
        foreach (var err in result.Errors) Console.WriteLine($"    {err}");
    }
    if (result.Diagnostics.Count == 0)
        Console.WriteLine("  意味解析: 診断なし (OK)");
    else
    {
        Console.WriteLine("  意味解析の診断:");
        foreach (var d in result.Diagnostics) Console.WriteLine($"    {d.Severity}: {d.Message} @ {d.Span}");
    }
    Console.WriteLine();
}
