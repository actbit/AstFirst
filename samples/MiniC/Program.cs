using MiniC;

// 軽量C言語パーサのサンプル
Console.WriteLine("=== AstFirst MiniC Parser Sample ===\n");

string code = """
    int x = 10 + 20 * 3;
    int y = x - 5;
    print(y);
    if (x)
        print(x);
    while (x)
        x = x - 1;
    print(x);
""";

var result = ProgramParser.Parse(code);

if (result.HasErrors)
{
    Console.WriteLine($"Parse errors: {result.Errors.Count}");
    foreach (var err in result.Errors)
        Console.WriteLine($"  {err}");
}
else
{
    Console.WriteLine("Parse succeeded!\n");

    // AST を表示
    void PrintAst(MiniC.Program? prog, int indent = 0)
    {
        while (prog is ConsStmt cons)
        {
            var stmt = cons.First;
            var pad = new string(' ', indent * 2);
            switch (stmt)
            {
                case DeclStmt d:
                    Console.WriteLine($"{pad}Decl {d.Name}" + (d.Init != null ? " = ..." : ""));
                    break;
                case AssignStmt a:
                    Console.WriteLine($"{pad}Assign {a.Name}");
                    break;
                case PrintStmt p:
                    Console.WriteLine($"{pad}Print");
                    break;
                case IfStmt f:
                    Console.WriteLine($"{pad}If");
                    PrintAst(new MiniC.ConsStmt(f.Body, new MiniC.NilProgram()), indent + 1);
                    break;
                case WhileStmt w:
                    Console.WriteLine($"{pad}While");
                    PrintAst(new MiniC.ConsStmt(w.Body, new MiniC.NilProgram()), indent + 1);
                    break;
                case BlockStmt b:
                    Console.WriteLine($"{pad}Block");
                    PrintAst(b.Body, indent + 1);
                    break;
            }
            prog = cons.Rest;
        }
    }

    PrintAst(result.Ast as MiniC.Program);
}

Console.WriteLine("\n=== Done ===");
