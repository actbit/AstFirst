namespace AstFirst.Core.Parsing;

/// <summary>生成規則 LHS -> Rhs[0] Rhs[1] ...。Tag には AST クラス等のメタ情報を載せる。</summary>
public sealed class Production
{
    public int Id { get; }
    public Symbol Lhs { get; }
    public Symbol[] Rhs { get; }
    public object? Tag { get; }
    /// <summary>規則に直接付けた優先度/結合性（%prec 相当）。
    /// トークン経由でなく規則単位で precedence を持つことで、同じ終端（generic の &gt; と比較の &gt; など）
    /// を含む複数規則で別々の優先度を設定できる。</summary>
    public Precedence? RulePrecedence { get; }

    public Production(int id, Symbol lhs, Symbol[] rhs, object? tag = null, Precedence? rulePrecedence = null)
    {
        Id = id;
        Lhs = lhs;
        Rhs = rhs;
        Tag = tag;
        RulePrecedence = rulePrecedence;
    }

    public int Length => Rhs.Length;

    public override string ToString()
    {
        var rhs = Lhs + " ->";
        for (int i = 0; i < Rhs.Length; i++) rhs += " " + Rhs[i];
        return rhs;
    }
}
