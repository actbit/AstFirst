using System.Collections.Generic;
using AstFirst.Core.Lexing;

namespace AstFirst.Glr;

/// <summary>軽量 GLR (Tomita-lite) の解析結果。勝者順の候補 AST と構文エラー。</summary>
public sealed class GlrResult
{
    /// <summary>accept に達した候補 AST (優先度/到達順)。非曖昧入力では通常 1 個。</summary>
    public IReadOnlyList<object?> Candidates { get; }
    /// <summary>構文エラー (位置付き)。</summary>
    public IReadOnlyList<ParseError> Errors { get; }

    public GlrResult(IReadOnlyList<object?> candidates, IReadOnlyList<ParseError> errors)
    {
        Candidates = candidates;
        Errors = errors;
    }
}

/// <summary>
/// 軽量 GLR (Generalized LR) ドライバ (Tomita-lite)。
/// LALR(1) テーブル (<see cref="GlrTables"/>) の上で、コンフリクトセルでは全候補を並行 fork し、
/// 入力が進むと (state, pos) が同じスタックを dedup-merge して一本化する。
/// 完全 SPPF は作らず、各候補は独立配列スタック (浅い fork が前提)。本質的曖昧性を扱う。
/// </summary>
public static class LightGlrDriver
{
    /// <summary>入力トークンを GLR パースし、候補 AST とエラーを返す。
    /// <paramref name="reduce"/> は (prodId, children, ctx) から部分木を構築するデリゲート (生成コードが ReduceNode をラップして渡す)。
    /// <paramref name="toToken"/> は LexToken を AstFirst.Token に変換するデリゲート。</summary>
    public static GlrResult Run(GlrTables t, IReadOnlyList<LexToken> tokens,
        SemanticContext ctx,
        System.Func<int, object?[], SemanticContext, object?> reduce,
        System.Func<LexToken, Token> toToken)
    {
        var active = new List<LightGlrStack> { LightGlrStack.New(t.StartState) };
        var accepted = new List<object?>();
        var errors = new List<ParseError>();
        int lastErrorPos = -10;

        while (active.Count > 0)
        {
            // --- reduce phase: 各スタックで reduce を cascade + fork し、reduce が収束したら shift 待ちへ ---
            active = ReduceAll(t, tokens, ctx, reduce, active);

            if (active.Count == 0) break;

            // --- shift phase: 各スタックで shift (または accept)。コンフリクト候補で fork ---
            var shifted = new List<LightGlrStack>();
            foreach (var s in active)
            {
                if (!s.Alive) continue;
                int la = ErrorRepair.LookaheadSym(t, tokens, s.Pos);
                if (la < 0) { s.Alive = false; continue; } // 未知トークン
                var acts = t.Actions(s.State, la);
                bool hasShift = false, hasAccept = false;
                foreach (var act in acts)
                {
                    if (act.Kind == 1) // Shift
                    {
                        var ns = s.Clone();
                        object? val = la == t.EofSym ? null : (object)toToken(tokens[s.Pos]);
                        ns.Push(act.Value, val);
                        ns.Pos = s.Pos + 1;
                        shifted.Add(ns);
                        hasShift = true;
                    }
                    else if (act.Kind == 3) // Accept
                    {
                        hasAccept = true;
                    }
                }
                if (hasAccept)
                {
                    // AstFirst のテーブルは $ を shift する (S'→S $)。accept 時のスタックトップが $ (null) なら
                    // 開始記号の値はその下。LALR の Accept (top-- で $ を捨てて result = その下) と同義。
                    var top = s.PeekValue();
                    var candidate = top is null ? s.Values[s.Top - 2] : top;
                    // Accept/Reject: Rejected な候補は破棄 (fork の他方が生き残る、または dead)
                    if (candidate is AstNode an && an.AcceptState == AcceptState.Rejected)
                        s.Alive = false;
                    else
                        accepted.Add(candidate);
                }
                if (!hasShift)
                {
                    // shift 先がない。accept 済みなら結果は回収済み。そうでなければ Corchuelo et al. の修復
                    // (ER1 挿入 / ER2 削除 / ER3 Forward move で N シンボル先まで確認) を最小コストで試みる。
                    if (!hasAccept)
                    {
                        if (s.Pos - lastErrorPos >= 3)
                        {
                            errors.Add(MakeError(t, tokens, s));
                            lastErrorPos = s.Pos;
                        }
                        var repaired = ErrorRepair.TryRepair(t, tokens, s, reduce, toToken, ctx);
                        if (repaired != null) shifted.Add(repaired);
                        else s.Alive = false;
                    }
                    else s.Alive = false;   // accept 済み: これ以上追わない
                }
            }
            active = Dedup(shifted);
        }

        return new GlrResult(accepted, errors);
    }

    /// <summary>全スタックで reduce を再帰的に適用 (cascade)。コンフリクトセルで複数 reduce 候補があれば fork。
    /// reduce がなくなったスタックは shift 待ちリストへ (同一 (state,pos) は dedup)。</summary>
    private static List<LightGlrStack> ReduceAll(GlrTables t, IReadOnlyList<LexToken> tokens,
        SemanticContext ctx, System.Func<int, object?[], SemanticContext, object?> reduce, List<LightGlrStack> active)
    {
        var done = new List<LightGlrStack>();
        var seen = new HashSet<(int, int)>();
        var work = new Queue<LightGlrStack>();
        foreach (var s in active) if (s.Alive) work.Enqueue(s);

        while (work.Count > 0)
        {
            var s = work.Dequeue();
            if (!s.Alive) continue;
            int la = ErrorRepair.LookaheadSym(t, tokens, s.Pos);
            if (la < 0) { s.Alive = false; continue; }

            var acts = t.Actions(s.State, la);
            var reduceActs = new List<int>();
            foreach (var a in acts) if (a.Kind == 2) reduceActs.Add(a.Value);

            if (reduceActs.Count == 0)
            {
                // reduce 収束 → shift 待ち. Rejected な候補は破棄 (Accept/Reject 統合)。
                if (s.PeekValue() is AstNode an && an.AcceptState == AcceptState.Rejected)
                {
                    s.Alive = false;
                    continue;
                }
                // 同一 state,pos は先着を残す (Tomita の最適化)
                if (seen.Add((s.State, s.Pos))) done.Add(s);
                else s.Alive = false;
                continue;
            }

            // 複数 reduce 候補 → 最初を s に、残りを reduce 前の snapshot の clone に適用 (fork)。
            // snapshot は reduce 前のスタック (i==0 で s が変更される前に取る)。
            var snapshot = s.Clone();
            for (int i = 0; i < reduceActs.Count; i++)
            {
                var target = i == 0 ? s : snapshot.Clone();
                ErrorRepair.ApplyReduce(t, reduce, ctx, target, reduceActs[i]);
                work.Enqueue(target);
            }
        }
        return done;
    }


    /// <summary>同一 (state, pos) のスタックを統合 (先着を残す)。これで fork の指数爆発を防ぐ。</summary>
    private static List<LightGlrStack> Dedup(List<LightGlrStack> stacks)
    {
        var seen = new HashSet<(int, int)>();
        var result = new List<LightGlrStack>();
        foreach (var s in stacks)
        {
            if (!s.Alive) continue;
            if (seen.Add((s.State, s.Pos))) result.Add(s);
            else s.Alive = false;
        }
        return result;
    }

    private static ParseError MakeError(GlrTables t, IReadOnlyList<LexToken> tokens, LightGlrStack s)
    {
        int pos = s.Pos < tokens.Count ? tokens[s.Pos].Start : (tokens.Count > 0 ? tokens[tokens.Count - 1].End : 0);
        var exp = new System.Text.StringBuilder();
        for (int e = 0; e < t.SymbolCount; e++)
        {
            if (t.ActionKind[s.State * t.SymbolCount + e] == 0) continue;
            if (exp.Length > 0) exp.Append(", ");
            exp.Append(e == t.EofSym ? "EOF" : (t.SymNames is not null && e < t.SymNames.Count ? t.SymNames[e] : "#" + e));
        }
        return new ParseError("予期しないトークン" + (exp.Length > 0 ? " (期待: " + exp + ")" : ""), pos);
    }

    public sealed class LightGlrStack
    {
        public int[] States;
        public object?[] Values;
        public int Top;
        public int Pos;
        public bool Alive = true;

        public LightGlrStack(int[] states, object?[] values, int top, int pos)
        {
            States = states; Values = values; Top = top; Pos = pos;
        }

        public static LightGlrStack New(int startState)
        {
            var s = new LightGlrStack(new int[64], new object?[64], 0, 0);
            s.States[s.Top++] = startState;
            return s;
        }

        public int State => States[Top - 1];
        public object? PeekValue() => Values[Top - 1];

        public void Push(int state, object? value)
        {
            if (Top >= States.Length)
            {
                System.Array.Resize(ref States, States.Length * 2);
                System.Array.Resize(ref Values, Values.Length * 2);
            }
            States[Top] = state;
            Values[Top] = value;
            Top++;
        }

        public LightGlrStack Clone()
        {
            var states = new int[States.Length];
            var values = new object?[Values.Length];
            System.Array.Copy(States, states, States.Length);
            System.Array.Copy(Values, values, Values.Length);
            return new LightGlrStack(states, values, Top, Pos) { Alive = Alive };
        }
    }
}
