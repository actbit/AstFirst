using System;
using System.Collections.Generic;
using AstFirst;
using AstFirst.Core.Lexing;
using AstFirst.Glr;

namespace AstFirst.Tests.Runtime.Glr;

/// <summary>軽量 GLR ドライバの単体テスト。Generator に依存せず、手作り GlrTables で
/// shift/reduce/accept/fork/dedup/dead の制御フローを検証する。</summary>
public class LightGlrDriverTests
{
    // 文法: S → 'a' (sym 0=$, 1=a, 2=S). state0: shift a→1, goto S→2. state1: reduce S→a. state2: accept.
    // withConflict: 同じ右辺の第2規則 S→'a' (prod1) を足し、state1/$ を reduce-reduce コンフリクトにする。
    private static GlrTables MakeTables(bool withConflict = false)
    {
        const int sc = 3;       // sym: 0=$, 1=a, 2=S
        const int states = 3;
        var actionKind = new byte[states * sc];
        var actionValue = new int[states * sc];
        var gotoTable = new int[states * sc];
        Array.Fill(gotoTable, -1);

        actionKind[0 * sc + 1] = 1; actionValue[0 * sc + 1] = 1;   // state0: shift 'a' -> state1
        gotoTable[0 * sc + 2] = 2;                                  // state0: goto S -> state2
        actionKind[1 * sc + 0] = 2; actionValue[1 * sc + 0] = 0;   // state1: reduce S->a (prod0) on $
        actionKind[2 * sc + 0] = 3;                                 // state2: accept on $

        int[] prodLhs = withConflict ? new[] { 2, 2 } : new[] { 2 };
        int[] prodLen = withConflict ? new[] { 1, 1 } : new[] { 1 };

        int[] altKeys = Array.Empty<int>();
        int[][] altActs = Array.Empty<int[]>();
        if (withConflict)
        {
            altKeys = new[] { 1 * sc + 0 };                         // state1, $
            altActs = new[] { new[] { 2 * 1000000 + 1 } };          // 追加候補: Reduce(prod1)
        }

        return new GlrTables(actionKind, actionValue, gotoTable, prodLhs, prodLen,
            defaultReduce: new[] { -1, -1, -1 }, tokenIdToSym: new[] { 1 },
            altKeys, altActs, stateCount: states, symbolCount: sc, eofSym: 0, startState: 0);
    }

    private static object? ReduceById(int prodId, object?[] children, SemanticContext ctx)
        => "S" + prodId + "(" + children.Length + ")";

    private static Token ToToken(LexToken lt) => new BasicToken(lt.Span, default(SourceSpan));

    [Fact]
    public void ParsesSimpleGrammar_SingleCandidate()
    {
        var t = MakeTables();
        var tokens = new List<LexToken> { new LexToken(0, "a", 0, 1) };
        var result = LightGlrDriver.Run(t, tokens, new BasicSemanticContext(), ReduceById, ToToken);

        Assert.Single(result.Candidates);
        Assert.Equal("S0(1)", result.Candidates[0]);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ForksOnConflict_ConvergesToWinner()
    {
        var t = MakeTables(withConflict: true);
        var tokens = new List<LexToken> { new LexToken(0, "a", 0, 1) };
        var result = LightGlrDriver.Run(t, tokens, new BasicSemanticContext(), ReduceById, ToToken);

        // reduce-reduce コンフリクトで fork しても、GOTO 先が同じ (state2,pos1) なので dedup され
        // 優先候補 (prod0) のみが accept に残る。
        Assert.Single(result.Candidates);
        Assert.Equal("S0(1)", result.Candidates[0]);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void EmptyInput_ReportsError()
    {
        var t = MakeTables();
        var tokens = new List<LexToken>();
        var result = LightGlrDriver.Run(t, tokens, new BasicSemanticContext(), ReduceById, ToToken);

        Assert.Empty(result.Candidates);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void UnexpectedToken_ReportsError()
    {
        // state0 は 'a' (sym1) しか受理しない。EOF (sym0) で開始 → 即座に dead。
        var t = MakeTables();
        // tokens を空にすると EOF になり dead。ここでは sym の届かない状況を再現:
        // TokenId=0 -> sym=1(a) だが入力に a があっても state1 到達後 EOF で reduce→accept するので、
        // 代わりに「未知の TokenId」(-1 を返すよう index 外) を与えて dead を起こす。
        var tokens = new List<LexToken> { new LexToken(5, "?", 0, 1) }; // TokenId 5 は TokenIdToSym 範囲外 → -1
        var result = LightGlrDriver.Run(t, tokens, new BasicSemanticContext(), ReduceById, ToToken);

        Assert.Empty(result.Candidates);
    }
}
