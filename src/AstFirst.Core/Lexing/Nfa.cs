using System.Collections.Generic;

namespace AstFirst.Core.Lexing;

/// <summary>
/// NFA の遷移。<see cref="Label"/> が null のとき ε 遷移。値型。
/// </summary>
public readonly struct NfaTransition
{
    public CharSet? Label { get; }
    public int Target { get; }
    public bool IsEpsilon => Label is null;

    public NfaTransition(CharSet? label, int target)
    {
        Label = label;
        Target = target;
    }
}

/// <summary>NFA の状態。</summary>
public sealed class NfaState
{
    public int Id { get; }
    public List<NfaTransition> Transitions { get; } = new List<NfaTransition>();
    public bool IsAccept;
    public int AcceptTokenId; // 0 = 未割当。レクサ統合時にトークン種別を設定。

    public NfaState(int id) => Id = id;
}

/// <summary>非決定性有限オートマトン (ε遷移付き)。</summary>
public sealed class Nfa
{
    public IReadOnlyList<NfaState> States { get; }
    public int Start { get; }
    public int Accept { get; }

    public Nfa(IReadOnlyList<NfaState> states, int start, int accept)
    {
        States = states;
        Start = start;
        Accept = accept;
    }
}
