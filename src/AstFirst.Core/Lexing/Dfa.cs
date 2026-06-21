using System;

namespace AstFirst.Core.Lexing;

/// <summary>DFA の状態。遷移はクラスID → 行き先状態ID。未遷移は -1。</summary>
public sealed class DfaState
{
    public int Id { get; }
    public int[] Transitions { get; }
    public bool IsAccept;
    public int AcceptTokenId; // 0 = 未割当。レクサ統合時にトークン種別を設定。

    public DfaState(int id, int classCount)
    {
        Id = id;
        Transitions = new int[classCount];
        Transitions.AsSpan().Fill(-1);
    }
}

/// <summary>決定性有限オートマトン。</summary>
public sealed class Dfa
{
    public IReadOnlyList<DfaState> States { get; }
    public int Start { get; }
    public AlphabetPartition Alphabet { get; }

    public Dfa(IReadOnlyList<DfaState> states, int start, AlphabetPartition alphabet)
    {
        States = states;
        Start = start;
        Alphabet = alphabet;
    }
}
