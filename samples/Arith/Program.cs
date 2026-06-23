using Arith;

Console.WriteLine("=== AstFirst 括弧付き四則演算 (REPL) ===");
Console.WriteLine("式を入力してください (空行で終了)。例: 1 + 2 * (3 - 1)");
Console.WriteLine("対応: + - * / ( )  整数演算\n");

while (true)
{
    Console.Write("> ");
    var input = Console.ReadLine();
    if (input is null || string.IsNullOrWhiteSpace(input)) break;

    var r = ExprParser.Parse(input);
    if (r.Errors.Count > 0)
    {
        Console.WriteLine("  構文エラー: " + string.Join("; ", r.Errors));
        Console.WriteLine();
        continue;
    }

    Console.WriteLine("  AST:");
    PrintTree(r.Ast as Expr, "  ", true);
    Console.Write("  = ");
    try
    {
        Console.WriteLine(Eval(r.Ast as Expr));
    }
    catch (DivideByZeroException)
    {
        Console.WriteLine("(0 による除算)");
    }
    Console.WriteLine();
}

Console.WriteLine("終了");

// AST をボックス文字で木表示。
void PrintTree(Expr? node, string indent, bool isLast)
{
    if (node is null) return;
    Console.Write(indent);
    Console.Write(isLast ? "└─ " : "├─ ");
    switch (node)
    {
        case NumExpr n:
            Console.WriteLine(n.Value);
            break;
        case AddExpr:
            Console.WriteLine("+");
            PrintChildren(indent, isLast, node);
            break;
        case SubExpr:
            Console.WriteLine("-");
            PrintChildren(indent, isLast, node);
            break;
        case MulExpr:
            Console.WriteLine("*");
            PrintChildren(indent, isLast, node);
            break;
        case DivExpr:
            Console.WriteLine("/");
            PrintChildren(indent, isLast, node);
            break;
        case ParenExpr p:
            Console.WriteLine("(...)");
            PrintTree(p.Inner, indent + (isLast ? "   " : "│  "), true);
            break;
    }
}

// 二項演算の Left / Right を表示。
void PrintChildren(string indent, bool parentIsLast, Expr node)
{
    var newIndent = indent + (parentIsLast ? "   " : "│  ");
    Expr[] children = node switch
    {
        AddExpr a => new[] { a.Left, a.Right },
        SubExpr s => new[] { s.Left, s.Right },
        MulExpr m => new[] { m.Left, m.Right },
        DivExpr d => new[] { d.Left, d.Right },
        _ => Array.Empty<Expr>(),
    };
    for (int i = 0; i < children.Length; i++)
        PrintTree(children[i], newIndent, i == children.Length - 1);
}

// AST を評価 (整数演算)。
int Eval(Expr? node) => node switch
{
    NumExpr n => n.Value,
    AddExpr a => Eval(a.Left) + Eval(a.Right),
    SubExpr s => Eval(s.Left) - Eval(s.Right),
    MulExpr m => Eval(m.Left) * Eval(m.Right),
    DivExpr d => Eval(d.Left) / Eval(d.Right),
    ParenExpr p => Eval(p.Inner),
    _ => 0,
};
