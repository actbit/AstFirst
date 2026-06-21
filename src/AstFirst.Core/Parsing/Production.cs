namespace AstFirst.Core.Parsing;

/// <summary>生成規則 LHS -> Rhs[0] Rhs[1] ...。Tag には AST クラス等のメタ情報を載せる。</summary>
public sealed class Production
{
    public int Id { get; }
    public Symbol Lhs { get; }
    public Symbol[] Rhs { get; }
    public object? Tag { get; }

    public Production(int id, Symbol lhs, Symbol[] rhs, object? tag = null)
    {
        Id = id;
        Lhs = lhs;
        Rhs = rhs;
        Tag = tag;
    }

    public int Length => Rhs.Length;

    public override string ToString()
    {
        var rhs = Lhs + " ->";
        for (int i = 0; i < Rhs.Length; i++) rhs += " " + Rhs[i];
        return rhs;
    }
}
