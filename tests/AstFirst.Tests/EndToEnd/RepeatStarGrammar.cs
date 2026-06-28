using AstFirst;

namespace AstFirst.Tests.EndToEnd.RepeatStarTest;

// [Repeat(Min=0)] (0回以上=Star) の E2E 検証用文法。Generator が RSProgramParser を生成。
// Plus (RepeatGrammar) と対で、空リストの受理を検証。

[Grammar]
[Skip(@"\s+")]
public abstract partial class RSProgram : AstNode { }

/// <summary>
/// RSProgram → [Repeat(Min=0)] RSItem は List_RSItem → RSItem | List_RSItem RSItem | ε に展開。
/// 空入力で空リスト (Count=0) になる。Items は IReadOnlyList&lt;RSItem&gt;。
/// </summary>
public sealed partial class RSProgramBody : RSProgram
{
    [Rule]
    public static void Body([Repeat(Min = 0)] RSItem items) { }
}

public sealed partial class RSItem : AstNode
{
    [Rule]
    public static void Item([Token(@"[a-z]+")] Token text) { }
}
