using AstFirst;

namespace MiniLang;

/// <summary>MiniLang: 変数宣言・print・四則演算 ([Rule] static モデル)。</summary>

[Grammar]
[Skip(@"\s+")]
public abstract partial class Stmt : AstNode { }

/// <summary>let x = expr;</summary>
public sealed partial class LetStmt : Stmt
{
    public string Name { get; private set; } = "";
    [Rule]
    public static void Let([Token(@"let", Priority = 1)] Token kw, [Token(@"[A-Za-z_]\w*")] Token nameTok,
                           [Token(@"=")] Token eq, Expr value, [Token(@";")] Token semi) { }
    partial void OnReduce()
    {
        Name = NameTok.Text;
    }
}

/// <summary>print expr;</summary>
public sealed partial class PrintStmt : Stmt
{
    [Rule]
    public static void Print([Token(@"print", Priority = 1)] Token kw, Expr value, [Token(@";")] Token semi) { }
}

public abstract partial class Expr : AstNode { }

public sealed partial class NumExpr : Expr
{
    public int Value { get; private set; }
    [Rule]
    public static void NumToken([Token(@"[0-9]+")] Token num) { }
    partial void OnReduce()
    {
        Value = int.Parse(Num.Text);
    }
}

public sealed partial class VarExpr : Expr
{
    public string Name { get; private set; } = "";
    [Rule]
    public static void VarToken([Token(@"[A-Za-z_]\w*")] Token nameTok) { }
    partial void OnReduce()
    {
        Name = NameTok.Text;
    }
}

[Precedence(1)]
public sealed partial class AddExpr : Expr
{
    [Rule]
    public static void Add(Expr left, [Token(@"\+")] Token op, Expr right) { }
}

[Precedence(2)]
public sealed partial class MulExpr : Expr
{
    [Rule]
    public static void Mul(Expr left, [Token(@"\*")] Token op, Expr right) { }
}
