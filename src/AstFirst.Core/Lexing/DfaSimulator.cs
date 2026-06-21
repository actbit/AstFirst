namespace AstFirst.Core.Lexing;

/// <summary>DFA を駆動して入力が受理されるか (完全一致) を判定。テスト用。</summary>
public static class DfaSimulator
{
    public static bool Matches(Dfa dfa, string input)
    {
        int current = dfa.Start;
        var states = dfa.States;
        var alphabet = dfa.Alphabet;
        for (int i = 0; i < input.Length; i++)
        {
            int cls = alphabet.ClassOf(input[i]);
            int next = states[current].Transitions[cls];
            if (next < 0) return false;
            current = next;
        }
        return states[current].IsAccept;
    }
}
