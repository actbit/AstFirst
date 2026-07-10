using AstFirst.Tests.EndToEnd.SecondPassTest;

namespace AstFirst.Tests.EndToEnd;

/// <summary>
/// 新モデルの 2パス目トラバーサル (OnSecondPass / Walker) の E2E 検証。
/// 旧モデルの Listener.Walk (Enter→子→Exit) に代わり、Parse 後に Walker が
/// 各ノードの OnSecondPassEnter(子の前) → 子再帰 → OnSecondPassExit(子の後) をトップダウンで自動呼出する。
/// </summary>
public class ListenerEndToEndTests
{
    [Fact]
    public void Walk_VisitsEveryNode()
    {
        // 1+2*3 → SAdd(SNum(1), SMul(SNum(2), SNum(3))) = 5 ノード
        SecondPassTrace.Reset();
        SNodeParser.Parse("1+2*3");
        Assert.Equal(5, SecondPassTrace.Events.Count(e => e.StartsWith("Enter")));
    }

    [Fact]
    public void Walk_EnterBeforeChildren_ExitAfter()
    {
        // 1+2 → SAdd(SNum(1), SNum(2))
        SecondPassTrace.Reset();
        SNodeParser.Parse("1+2");
        var ev = SecondPassTrace.Events;
        Assert.Equal("EnterSAdd", ev[0]);
        Assert.Equal("ExitSAdd", ev[^1]);
        Assert.True(ev.IndexOf("EnterSAdd") < ev.IndexOf("ExitSAdd"));
        Assert.Equal(6, ev.Count); // 1 SAdd + 2 SNum, Enter/Exit each
    }
}
