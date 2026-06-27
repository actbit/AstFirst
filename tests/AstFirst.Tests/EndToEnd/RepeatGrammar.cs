using AstFirst;

namespace AstFirst.Tests.EndToEnd.RepeatTest;

// [Repeat] (リスト表現) の E2E 検証用文法。Generator が RProgramParser を生成。
// 他文法 (SymStmt / SNode 等) と混入しないよう別名前空間へ分離。

/// <summary>[Repeat] リスト表現のルート非終端。</summary>
[Grammar]
[Skip(@"\s+")]
public abstract partial class RProgram : AstNode { }

/// <summary>
/// RProgram → [Repeat] RItem は List_RItem → RItem | List_RItem RItem (1回以上) に展開される。
/// Items は IReadOnlyList&lt;RItem&gt;。
/// </summary>
public sealed partial class RProgramBody : RProgram
{
    [Rule]
    public static void Body([Repeat] RItem items) { }
}

public sealed partial class RItem : AstNode
{
    [Rule]
    public static void Item([Token(@"[a-z]+")] Token text) { }
}
