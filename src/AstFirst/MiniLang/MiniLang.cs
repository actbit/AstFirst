using AstFirst;

namespace MiniLang;

/// <summary>MiniLang: 変数宣言・print・四則演算のサンプル言語。</summary>

[Grammar]
[Skip(@"\s+")]
public abstract class Stmt : AstNode { }

/// <summary>let x = expr;</summary>
public sealed class LetStmt : Stmt
{
    public string Name { get; }
    public Expr Value { get; }
    public LetStmt([Pattern(@"let", Priority = 1)] Token kw, [Pattern(@"[A-Za-z_]\w*")] Token name,
                   [Pattern(@"=")] Token eq, Expr value, [Pattern(@";")] Token semi)
    {
        Name = name.Text;
        Value = value;
    }
}

/// <summary>print expr;</summary>
public sealed class PrintStmt : Stmt
{
    public Expr Value { get; }
    public PrintStmt([Pattern(@"print", Priority = 1)] Token kw, Expr value, [Pattern(@";")] Token semi)
    {
        Value = value;
    }
}

public abstract class Expr : AstNode { }

public sealed class NumExpr : Expr
{
    public int Value { get; }
    public NumExpr([Pattern(@"[0-9]+")] Token num) { Value = int.Parse(num.Text); }
}

public sealed class VarExpr : Expr
{
    public string Name { get; }
    public VarExpr([Pattern(@"[A-Za-z_]\w*")] Token name) { Name = name.Text; }
}

[Precedence(1)]
public sealed class AddExpr : Expr
{
    public Expr Left { get; }
    public Expr Right { get; }
    public AddExpr(Expr left, [Pattern(@"\+")] Token op, Expr right) { Left = left; Right = right; }
}

[Precedence(2)]
public sealed class MulExpr : Expr
{
    public Expr Left { get; }
    public Expr Right { get; }
    public MulExpr(Expr left, [Pattern(@"\*")] Token op, Expr right) { Left = left; Right = right; }
}
