using System.Collections.Generic;
using AstFirst;
using Calc;

namespace AstFirst.Tests.EndToEnd;

/// <summary>
/// 電卓文法から生成された ExprListener で Walk (Enter→子→Exit) が機能することを検証。
/// </summary>
public class ListenerEndToEndTests
{
    private sealed class CountingListener : ExprListener
    {
        public int Entered;
        public override void EnterEach(AstNode node) => Entered++;
    }

    [Fact]
    public void Walk_VisitsEveryNode()
    {
        // 1+2*3 → AddExpr(NumExpr(1), MulExpr(NumExpr(2), NumExpr(3))) = 5 ノード
        var result = ExprParser.Parse("1+2*3");
        var l = new CountingListener();
        l.Walk((AstNode)result.Ast!);
        Assert.Equal(5, l.Entered);
    }

    private sealed class OrderListener : ExprListener
    {
        public readonly List<string> Events = new();
        public override void EnterAddExpr(AddExpr node) => Events.Add("EnterAddExpr");
        public override void ExitAddExpr(AddExpr node) => Events.Add("ExitAddExpr");
        public override void EnterNumExpr(NumExpr node) => Events.Add("EnterNumExpr");
        public override void ExitNumExpr(NumExpr node) => Events.Add("ExitNumExpr");
    }

    [Fact]
    public void Walk_EnterBeforeChildren_ExitAfter()
    {
        // 1+2 → AddExpr(NumExpr(1), NumExpr(2))
        var result = ExprParser.Parse("1+2");
        var l = new OrderListener();
        l.Walk((AstNode)result.Ast!);
        // EnterAddExpr → (EnterNumExpr, ExitNumExpr) x2 → ExitAddExpr
        Assert.Equal("EnterAddExpr", l.Events[0]);
        Assert.Equal("ExitAddExpr", l.Events[^1]);
        Assert.True(l.Events.IndexOf("EnterAddExpr") < l.Events.IndexOf("ExitAddExpr"));
        Assert.Equal(6, l.Events.Count); // 1 AddExpr + 2 NumExpr, Enter/Exit each
    }
}
