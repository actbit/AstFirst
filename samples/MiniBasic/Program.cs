using MiniBasic;

Console.WriteLine("=== AstFirst MiniBASIC Parser Sample ===\n");

string basic = """
10 PRINT 1 + 2 * 3
20 LET A = 10
30 PRINT A - 3
40 IF A = 10 THEN GOTO 60
50 PRINT 999
60 PRINT A
70 GOTO 90
80 PRINT 888
90 END
""";

var result = LineParser.Parse(basic);

if (result.HasErrors)
{
    Console.WriteLine($"Parse errors: {result.Errors.Count}");
    foreach (var err in result.Errors)
        Console.WriteLine($"  {err}");
}
else
{
    Console.WriteLine("Parse succeeded!\n");

    var line = result.Ast as MiniBasic.Line;
    while (line is ConsLine cons)
    {
        switch (cons.First)
        {
            case PrintStmt p:
                Console.WriteLine($"  PRINT");
                break;
            case LetStmt l:
                Console.WriteLine($"  LET {l.Name}");
                break;
            case IfStmt f:
                Console.WriteLine($"  IF ... THEN {f.TargetLine}");
                break;
            case IfGotoStmt f:
                Console.WriteLine($"  IF ... THEN GOTO {f.TargetLine}");
                break;
            case GotoStmt g:
                Console.WriteLine($"  GOTO {g.TargetLine}");
                break;
            case EndStmt:
                Console.WriteLine($"  END");
                break;
        }
        line = cons.Rest;
    }
}

Console.WriteLine("\n=== Done ===");
