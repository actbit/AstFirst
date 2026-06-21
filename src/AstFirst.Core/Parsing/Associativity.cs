namespace AstFirst.Core.Parsing;

/// <summary>結合性。shift-reduce 衝突で同優先度の時の解決に使う。</summary>
public enum Associativity
{
    /// <summary>左結合: a+b+c = (a+b)+c。衝突で reduce を選ぶ。</summary>
    Left,
    /// <summary>右結合: a=b=c = a=(b=c)。衝突で shift を選ぶ。</summary>
    Right,
    /// <summary>非結合: 比較演算子等。衝突でエラー。</summary>
    NonAssoc,
}

/// <summary>終端の優先度と結合性。</summary>
public readonly struct Precedence(int priority, Associativity associativity)
{
    public int Priority { get; } = priority;
    public Associativity Associativity { get; } = associativity;

    public bool IsDefault => Priority == 0 && Associativity == Associativity.Left;
}
