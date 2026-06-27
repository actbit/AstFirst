using System.Collections.Generic;
using AstFirst;

namespace AstFirst.Tests.EndToEnd.SecondPassTest;

// ListenerEndToEndTests 用の文法。Generator がテストプロジェクト内で SNodeParser を生成。
// 新モデルの 2パス目: Parse 後に WalkSecondPass が各ノードの OnSecondPassEnter(子の前) → 子再帰 → OnSecondPassExit(子の後)
// をトップダウンで自動呼出する。SymStmt 文法 (AstFirst.Tests.EndToEnd) と混入しないよう別名前空間へ分離。

/// <summary>OnSecondPass の E2E 検証用文法。</summary>
[Grammar]
[Skip(@"\s+")]
public abstract partial class SNode : AstNode { }

[Precedence(1)]
public sealed partial class SAdd : SNode, IOnSecondPassEnter, IOnSecondPassExit
{
    [Rule]
    public static void Add(SNode left, [Token(@"\+")] Token op, SNode right) { }
    public void OnSecondPassEnter(SemanticContext ctx) => SecondPassTrace.Enter(this);
    public void OnSecondPassExit(SemanticContext ctx) => SecondPassTrace.Exit(this);
}

[Precedence(2)]
public sealed partial class SMul : SNode, IOnSecondPassEnter, IOnSecondPassExit
{
    [Rule]
    public static void Mul(SNode left, [Token(@"\*")] Token op, SNode right) { }
    public void OnSecondPassEnter(SemanticContext ctx) => SecondPassTrace.Enter(this);
    public void OnSecondPassExit(SemanticContext ctx) => SecondPassTrace.Exit(this);
}

public sealed partial class SNum : SNode, IOnSecondPassEnter, IOnSecondPassExit
{
    [Rule]
    public static void Num([Token(@"[0-9]+")] Token n) { }
    public void OnSecondPassEnter(SemanticContext ctx) => SecondPassTrace.Enter(this);
    public void OnSecondPassExit(SemanticContext ctx) => SecondPassTrace.Exit(this);
}

/// <summary>OnSecondPass の呼び出しを記録 (Enter/Exit × ノード型)。テスト間で Reset すること。</summary>
public static class SecondPassTrace
{
    public static readonly List<string> Events = new();
    public static void Enter(AstNode n) => Events.Add("Enter" + n.GetType().Name);
    public static void Exit(AstNode n) => Events.Add("Exit" + n.GetType().Name);
    public static void Reset() => Events.Clear();
}
