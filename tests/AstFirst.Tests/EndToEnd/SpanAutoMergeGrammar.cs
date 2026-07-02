using AstFirst;

namespace AstFirst.Tests.EndToEnd.SpanAutoTest;

// AstNode.Span の自動マージ (子の Span 統合) と OnReduce での手動上書きの E2E 検証用文法。
// Generator が SAMParser を生成。他文法と混入しないよう別名前空間へ分離。

[Grammar]
[Skip(@"\s+")]
public abstract partial class SAMRoot : AstNode { }

/// <summary>
/// OnReduce なし: reduce 時に子 (# と name) の Span を自動マージし、
/// "#abc" 全体 [0,4) を覆う Span になることを検証するノード。
/// </summary>
public sealed partial class SAMAuto : SAMRoot
{
    [Rule]
    public static void Auto([Token(@"\#")] Token hash, [Token(@"[a-z]+")] Token name) { }
}

/// <summary>
/// OnReduce で意図的に Name.Span のみ (dollar を含まない) を設定するノード。
/// 自動マージなら "$abc" 全体 [0,4) になるところ、OnReduce の手動設定が優先されて
/// [1,4) になること (後方互換) を検証する。
/// </summary>
public sealed partial class SAMManual : SAMRoot
{
    [Rule]
    public static void Manual([Token(@"\$")] Token dollar, [Token(@"[a-z]+")] Token name) { }
    partial void OnReduce() { Span = Name.Span; }
}
