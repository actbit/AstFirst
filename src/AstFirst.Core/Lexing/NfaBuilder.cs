using System;
using System.Collections.Generic;

namespace AstFirst.Core.Lexing;

/// <summary>
/// Thompson 構成法で <see cref="RegexAst"/> を <see cref="Nfa"/> に変換する。
/// 各 AST ノードを「開始状態・受理状態」の2状態からなるフラグメントに変換し、
/// ε遷移で結合する。状態数は AST サイズに線形。
/// </summary>
public static class NfaBuilder
{
    public static Nfa Build(RegexAst ast)
    {
        if (ast is null) throw new ArgumentNullException(nameof(ast));
        var b = new Builder();
        var (start, accept) = b.Fragment(ast);
        b.States[accept].IsAccept = true;
        return new Nfa(b.States, start, accept);
    }

    private sealed class Builder
    {
        public readonly List<NfaState> States = new List<NfaState>();

        public int NewState()
        {
            int id = States.Count;
            States.Add(new NfaState(id));
            return id;
        }

        public void AddEpsilon(int from, int to) =>
            States[from].Transitions.Add(new NfaTransition(null, to));

        public void AddChar(int from, int to, CharSet label) =>
            States[from].Transitions.Add(new NfaTransition(label, to));

        // 戻り値: (開始状態, 受理状態)
        public (int start, int accept) Fragment(RegexAst ast)
        {
            switch (ast)
            {
                case EmptyAst:
                {
                    int s = NewState(), a = NewState();
                    AddEpsilon(s, a);
                    return (s, a);
                }
                case LiteralAst lit:
                {
                    int s = NewState(), a = NewState();
                    AddChar(s, a, CharSet.Single(lit.Ch));
                    return (s, a);
                }
                case AnyCharAst:
                {
                    // '.' は改行以外の任意1文字。
                    int s = NewState(), a = NewState();
                    AddChar(s, a, CharSet.Single('\n').Complement());
                    return (s, a);
                }
                case CharSetAst cs:
                {
                    int s = NewState(), a = NewState();
                    AddChar(s, a, cs.Set);
                    return (s, a);
                }
                case ConcatAst concat:
                {
                    var first = Fragment(concat.Parts[0]);
                    int start = first.start;
                    int prevAccept = first.accept;
                    for (int i = 1; i < concat.Parts.Count; i++)
                    {
                        var frag = Fragment(concat.Parts[i]);
                        AddEpsilon(prevAccept, frag.start);
                        prevAccept = frag.accept;
                    }
                    return (start, prevAccept);
                }
                case AlternateAst alt:
                {
                    int start = NewState(), accept = NewState();
                    for (int i = 0; i < alt.Options.Count; i++)
                    {
                        var frag = Fragment(alt.Options[i]);
                        AddEpsilon(start, frag.start);
                        AddEpsilon(frag.accept, accept);
                    }
                    return (start, accept);
                }
                case RepeatAst rep:
                    return rep.Kind switch
                    {
                        RepeatKind.Star => Star(Fragment(rep.Inner)),
                        RepeatKind.Plus => Plus(Fragment(rep.Inner)),
                        _ => Optional(Fragment(rep.Inner)),
                    };
                default:
                    throw new ArgumentException("不明な RegexAst ノードです: " + ast.GetType().Name);
            }
        }

        // A*: 新開始/受理を用意し、A を0回以上。
        private (int, int) Star((int start, int accept) a)
        {
            int start = NewState(), accept = NewState();
            AddEpsilon(start, a.start);
            AddEpsilon(start, accept);     // 0回
            AddEpsilon(a.accept, a.start); // 繰り返し
            AddEpsilon(a.accept, accept);
            return (start, accept);
        }

        // A+: A を1回以上。全体の開始は A の開始。
        private (int, int) Plus((int start, int accept) a)
        {
            int accept = NewState();
            AddEpsilon(a.accept, a.start); // 繰り返し
            AddEpsilon(a.accept, accept);
            return (a.start, accept);
        }

        // A?: A を0回または1回。
        private (int, int) Optional((int start, int accept) a)
        {
            int start = NewState(), accept = NewState();
            AddEpsilon(start, a.start);
            AddEpsilon(start, accept); // 0回
            AddEpsilon(a.accept, accept);
            return (start, accept);
        }
    }
}
