using AstFirst;

namespace AstFirst.Compat;

// 互換性検証用の最小文法。net6.0/net8.0/net10.0 で Generator がパーサを生成し、
// Runtime でパースできるかを確認する。[Precedence] と複数ノードを使う現実的な縮減セット。

[Grammar]
[Skip(@"\s+")]
public abstract partial class CompatExpr : AstNode { }

public sealed partial class CompatNum : CompatExpr
{
    public int Value { get; private set; }
    [Rule]
    public static void Num([Token(@"[0-9]+")] Token numTok) { }
    partial void OnReduce() => Value = int.Parse(NumTok.Text);
}

[Precedence(1)]
public sealed partial class CompatAdd : CompatExpr
{
    [Rule]
    public static void Add(CompatExpr left, [Token(@"\+")] Token op, CompatExpr right) { }
}

public static class Program
{
    public static int Main()
    {
        // 1+2+3 → CompatAdd(CompatAdd(1,2),3)。左結合で reduce されることを確認。
        var result = CompatExprParser.Parse("1+2+3");
        if (result.HasErrors)
        {
            Console.WriteLine("FAIL: parse errors: " + string.Join(", ", result.Errors));
            return 1;
        }

        // トップレベルは CompatAdd、左の子も CompatAdd (1+2)。
        if (result.Ast is not CompatAdd add)
        {
            Console.WriteLine("FAIL: expected CompatAdd, got " + result.Ast?.GetType().Name);
            return 2;
        }
        if (add.Left is not CompatAdd || add.Right is not CompatNum { Value: 3 })
        {
            Console.WriteLine("FAIL: unexpected structure");
            return 3;
        }

        Console.WriteLine("OK: 1+2+3 parsed as CompatAdd(CompatAdd(1,2),3) on " + Environment.Version);
        return 0;
    }
}
