using AstFirst;

namespace AstFirst.Tests.EndToEnd.AbstractTest;

// 中間抽象クラス (プロパティ継承 + 単位規則) の E2E 検証用文法。Generator が ANodeParser を生成。
// 他文法と混入しないよう別名前空間へ分離。

[Grammar]
[Skip(@"\s+")]
public abstract partial class ANode : AstNode { }

/// <summary>
/// 中間抽象: 共通プロパティ (Left/Right) を [Rule] で宣言。抽象なので文法には追加されず
/// (ReduceActionModel にならない)、protected コンストラクタ + readonly フィールドのみ生成される。
/// 具象サブクラス (AAdd) は : base(left, right) で初期化し、Left/Right を再定義しない (readonly 維持)。
/// </summary>
public abstract partial class ABinary : ANode
{
    [Rule]
    public static void Base(ANode left, ANode right) { }
}

[Precedence(1)]
public sealed partial class AAdd : ABinary
{
    [Rule]
    public static void Add(ANode left, [Token(@"\+")] Token op, ANode right) { }
}

public sealed partial class ANum : ANode
{
    [Rule]
    public static void Num([Token(@"[0-9]+")] Token n) { }
}
