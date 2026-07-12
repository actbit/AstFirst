using System.Collections.Generic;
using AstFirst.Core.Lexing;

namespace AstFirst.Glr;

/// <summary>軽量 GLR (Tomita-lite) の解析結果。勝者順の候補 AST と構文エラー。</summary>
public sealed class GlrResult
{
    public IReadOnlyList<object?> Candidates { get; }
    public IReadOnlyList<ParseError> Errors { get; }
    public GlrResult(IReadOnlyList<object?> candidates, IReadOnlyList<ParseError> errors)
    {
        Candidates = candidates;
        Errors = errors;
    }
}

/// <summary>
/// 軽量 GLR (Generalized LR) ドライバ (Tomita-lite)。
/// 単一スタック時は fast path (List/Queue/HashSet バイパス) で LALR に近い性能。
/// コンフリクトセルでのみ fork し、収束でマージ。完全 SPPF は作らない。
/// </summary>
public static class LightGlrDriver
{
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
            // === Fast path: 単一スタック (List/Queue/HashSet バイパス) ===
            if (active.Count == 1 && active[0].Alive)
            {
                var s = active[0];
                bool needSlowPath = ProcessSingleStack(t, tokens, ctx, reduce, toToken,
                    s, accepted, errors, ref lastErrorPos);
                if (!needSlowPath)
                {
                    if (!s.Alive) { active.Clear(); break; }
                    continue;
                }
                // slow path へフォールスルー
            }

            // === Slow path: 複数スタック (GLR fork) ===
            active = ReduceAll(t, tokens, ctx, reduce, active);
            if (active.Count == 0) break;

            var shifted = new List<LightGlrStack>();
            foreach (var s in active)
            {
                if (!s.Alive) continue;
                int la = ErrorRepair.LookaheadSym(t, tokens, s.Pos);
                if (la < 0) { s.Alive = false; continue; }
                var acts = t.Actions(s.State, la);
                int shiftCount = 0; int firstShiftState = -1;
                bool hasAccept = false;
                foreach (var act in acts)
                {
                    if (act.Kind == 1) { shiftCount++; if (shiftCount == 1) firstShiftState = act.Value; }
                    else if (act.Kind == 3) hasAccept = true;
                }
                if (shiftCount == 1 && !hasAccept)
                {
                    object? val = la == t.EofSym ? null : (object)toToken(tokens[s.Pos]);
                    s.Push(firstShiftState, val);
                    s.Pos = s.Pos + 1;
                    shifted.Add(s);
                }
                else
                {
                    foreach (var act in acts)
                    {
                        if (act.Kind == 1)
                        {
                            var ns = s.Clone();
                            object? val = la == t.EofSym ? null : (object)toToken(tokens[s.Pos]);
                            ns.Push(act.Value, val);
                            ns.Pos = s.Pos + 1;
                            shifted.Add(ns);
                        }
                    }
                }
                if (hasAccept)
                {
                    var top = s.PeekValue();
                    var candidate = top is null ? s.Values[s.Top - 2] : top;
                    if (candidate is AstNode an && an.AcceptState == AcceptState.Rejected)
                        s.Alive = false;
                    else
                        accepted.Add(candidate);
                }
                if (shiftCount == 0 && !hasAccept)
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
                else if (hasAccept && shiftCount == 0) s.Alive = false;
            }
            active = Dedup(shifted);
        }

        return new GlrResult(accepted, errors);
    }

    /// <summary>単一スタックの fast path。reduce cascade + shift/accept/error をインライン処理。
    /// fork が必要 (reduce-reduce コンフリクト等) になったら true を返して slow path へ。</summary>
    private static bool ProcessSingleStack(GlrTables t, IReadOnlyList<LexToken> tokens,
        SemanticContext ctx, System.Func<int, object?[], SemanticContext, object?> reduce,
        System.Func<LexToken, Token> toToken,
        LightGlrStack s, List<object?> accepted, List<ParseError> errors, ref int lastErrorPos)
    {
        // Reduce cascade (Queue/HashSet/List バイパス、in-place)
        int guard = 0;
        while (true)
        {
            if (guard++ > 10000) { s.Alive = false; return false; }
            int la = ErrorRepair.LookaheadSym(t, tokens, s.Pos);
            if (la < 0) { s.Alive = false; return false; }
            var acts = t.Actions(s.State, la);
            int reduceVal = -1, reduceCount = 0;
            foreach (var a in acts)
            {
                if (a.Kind == 2) { reduceCount++; if (reduceCount == 1) reduceVal = a.Value; }
            }
            if (reduceCount == 0) break;
            if (reduceCount > 1) return true; // fork 必要 → slow path
            try { ErrorRepair.ApplyReduce(t, reduce, ctx, s, reduceVal); }
            catch { s.Alive = false; return false; }
        }

        // NotifyAccepted (ルート確定)
        if (s.Top > 0 && s.PeekValue() is AstNode survivor) survivor.NotifyAccepted(ctx);

        // Shift / accept / error
        int la2 = ErrorRepair.LookaheadSym(t, tokens, s.Pos);
        if (la2 < 0) { s.Alive = false; return false; }
        var acts2 = t.Actions(s.State, la2);
        int shiftState = -1, shiftCount = 0;
        bool hasAccept = false;
        foreach (var a in acts2)
        {
            if (a.Kind == 1) { shiftCount++; if (shiftCount == 1) shiftState = a.Value; }
            else if (a.Kind == 3) hasAccept = true;
        }
        if (hasAccept)
        {
            var top = s.PeekValue();
            var candidate = top is null ? s.Values[s.Top - 2] : top;
            if (!(candidate is AstNode an && an.AcceptState == AcceptState.Rejected))
                accepted.Add(candidate);
        }
        if (shiftCount > 1) return true; // fork 必要 → slow path
        if (shiftCount == 1)
        {
            object? val = la2 == t.EofSym ? null : (object)toToken(tokens[s.Pos]);
            s.Push(shiftState, val);
            s.Pos++;
            if (hasAccept) { s.Alive = false; return false; }
            return false; // fast path 継続
        }
        if (!hasAccept)
        {
            // Error: Corchuelo repair
            if (s.Pos - lastErrorPos >= 3)
            {
                errors.Add(MakeError(t, tokens, s));
                lastErrorPos = s.Pos;
            }
            var repaired = ErrorRepair.TryRepair(t, tokens, s, reduce, toToken, ctx);
            if (repaired != null)
            {
                // repaired スタックで続き (s を入れ替え)
                s.States = repaired.States; s.Values = repaired.Values;
                s.Top = repaired.Top; s.Pos = repaired.Pos;
                return false;
            }
            s.Alive = false;
        }
        else s.Alive = false;
        return false;
    }

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
                if (s.PeekValue() is AstNode an && an.AcceptState == AcceptState.Rejected)
                {
                    s.Alive = false; continue;
                }
                if (seen.Add((s.State, s.Pos)))
                {
                    if (s.PeekValue() is AstNode survivor) survivor.NotifyAccepted(ctx);
                    done.Add(s);
                }
                else s.Alive = false;
                continue;
            }

            if (reduceActs.Count == 1)
            {
                try { ErrorRepair.ApplyReduce(t, reduce, ctx, s, reduceActs[0]); }
                catch { s.Alive = false; }
                work.Enqueue(s);
            }
            else
            {
                var snapshot = s.Clone();
                for (int i = 0; i < reduceActs.Count; i++)
                {
                    var target = i == 0 ? s : snapshot.Clone();
                    try { ErrorRepair.ApplyReduce(t, reduce, ctx, target, reduceActs[i]); }
                    catch { target.Alive = false; }
                    work.Enqueue(target);
                }
            }
        }
        return done;
    }

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
