using System.Collections.Generic;
using AstFirst.Core.Lexing;

namespace AstFirst.Glr;

/// <summary>
/// Corchuelo et al. のエラー修復 (ER1 挿入 / ER2 削除 / ER3 Forward move)。
/// LALR パーサと LightGlrDriver の両方から呼ばれる共通の修復ロジック。
/// 参考: "Repairing Syntax Errors in LR Parsers" (Corchuelo, Pérez, Ruiz, Toro, Sevilla)。
/// </summary>
public static class ErrorRepair
{
    private const int N = 3;            // ER3 の Forward move シンボル数
    private const int CostInsert = 1;   // 挿入は安い (欠落の補完)
    private const int CostDelete = 2;   // 削除は高い (入力の破棄)

    /// <summary>エラー状態のスタック s から修復を試みる。成功すれば修復適用済みのスタック、失敗すれば null。</summary>
    public static LightGlrDriver.LightGlrStack? TryRepair(
        GlrTables t, IReadOnlyList<LexToken> tokens,
        LightGlrDriver.LightGlrStack s,
        System.Func<int, object?[], SemanticContext, object?> reduce,
        System.Func<LexToken, Token> toToken,
        SemanticContext ctx)
    {
        LightGlrDriver.LightGlrStack? best = null;
        int bestCost = int.MaxValue;
        int qm = s.State;
        var dummyToken = new BasicToken("", default(SourceSpan));

        // ER1: 現状態 qm で shift 可能な終端 t0 (≠$) を挿入候補。
        for (int t0 = 0; t0 < t.SymbolCount; t0++)
        {
            if (t0 == t.EofSym) continue;
            if (t.ActionKind[qm * t.SymbolCount + t0] != 1) continue;
            var probe = s.Clone();
            probe.Push(t.ActionValue[qm * t.SymbolCount + t0], dummyToken);
            if (SimulateForward(t, tokens, reduce, toToken, ctx, probe) && CostInsert < bestCost)
            {
                best = s.Clone();
                best.Push(t.ActionValue[qm * t.SymbolCount + t0], dummyToken);
                bestCost = CostInsert;
            }
        }

        // ER2: 現トークン t1 を削除候補。
        if (s.Pos < tokens.Count)
        {
            var probe = s.Clone();
            probe.Pos = s.Pos + 1;
            if (SimulateForward(t, tokens, reduce, toToken, ctx, probe) && CostDelete < bestCost)
            {
                best = s.Clone();
                best.Pos = s.Pos + 1;
                bestCost = CostDelete;
            }
        }

        return best;
    }

    /// <summary>ER3 Forward move: N シンボル (または accept) までパースを進められるか確認。</summary>
    private static bool SimulateForward(GlrTables t, IReadOnlyList<LexToken> tokens,
        System.Func<int, object?[], SemanticContext, object?> reduce,
        System.Func<LexToken, Token> toToken,
        SemanticContext ctx, LightGlrDriver.LightGlrStack sim)
    {
        int parsed = 0;
        var visited = new HashSet<(int, int)>();
        while (parsed < N)
        {
            int guard = 0;
            while (true)
            {
                if (guard++ > 4096) return false;
                int la = LookaheadSym(t, tokens, sim.Pos);
                if (la < 0) return false;
                if (!visited.Add((sim.State, sim.Pos))) return false;
                var acts = t.Actions(sim.State, la);
                int ra = -1;
                foreach (var a in acts) if (a.Kind == 2) { ra = a.Value; break; }
                if (ra < 0) break;
                try { ApplyReduce(t, reduce, ctx, sim, ra); }
                catch { return false; }
            }
            int la2 = LookaheadSym(t, tokens, sim.Pos);
            if (la2 < 0) return false;
            var acts2 = t.Actions(sim.State, la2);
            bool hasShift = false, hasAccept = false;
            int shiftState = -1;
            foreach (var a in acts2)
            {
                if (a.Kind == 1) { hasShift = true; shiftState = a.Value; }
                else if (a.Kind == 3) hasAccept = true;
            }
            if (hasAccept) return parsed > 0;
            if (!hasShift) return false;
            sim.Push(shiftState, la2 == t.EofSym ? null : (object)toToken(tokens[sim.Pos]));
            sim.Pos++;
            parsed++;
        }
        return true;
    }

    private static void ApplyReduce(GlrTables t, System.Func<int, object?[], SemanticContext, object?> reduce,
        SemanticContext ctx, LightGlrDriver.LightGlrStack s, int prodId)
    {
        int len = t.ProdLen[prodId];
        var children = len == 0 ? System.Array.Empty<object?>() : new object?[len];
        for (int i = 0; i < len; i++) children[i] = s.Values[s.Top - len + i];
        var node = reduce(prodId, children, ctx);
        s.Top -= len;
        int lhs = t.ProdLhs[prodId];
        int gotoState = t.Goto[s.State * t.SymbolCount + lhs];
        s.Push(gotoState, node);
    }

    private static int LookaheadSym(GlrTables t, IReadOnlyList<LexToken> tokens, int pos)
    {
        if (pos >= tokens.Count) return t.EofSym;
        int tid = tokens[pos].TokenId;
        if (tid < 0 || tid >= t.TokenIdToSym.Length) return -1;
        return t.TokenIdToSym[tid];
    }
}
