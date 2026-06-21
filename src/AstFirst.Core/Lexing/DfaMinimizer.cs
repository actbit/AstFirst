using System.Collections.Generic;

namespace AstFirst.Core.Lexing;

/// <summary>
/// DFA を分割細分化反復 (Hopcroft/Moore 系) で最小化する。
/// 受理状態は AcceptTokenId ごとに区別し、各入力クラスでの遷移先分割が
/// 同じ状態群を1状態に統合する。異なる AcceptTokenId の受理状態は統合されない。
/// </summary>
public static class DfaMinimizer
{
    public static Dfa Minimize(Dfa dfa)
    {
        int n = dfa.States.Count;
        if (n <= 1) return dfa;
        int classCount = dfa.Alphabet.ClassCount;

        // 初期分割: 非受理=0、受理=AcceptTokenId+1 (異トークンの受理状態を区別)。
        int[] part = new int[n];
        for (int s = 0; s < n; s++)
            part[s] = dfa.States[s].IsAccept ? (dfa.States[s].AcceptTokenId + 1) : 0;

        // シグネチャ (part[s] と各クラスの遷移先 part[]) が同じ状態を同じ分割へ。
        // 収束まで反復。
        bool changed = true;
        while (changed)
        {
            changed = false;
            var sigs = new int[n][];
            for (int s = 0; s < n; s++)
            {
                var sig = new int[classCount + 1];
                sig[0] = part[s];
                var trs = dfa.States[s].Transitions;
                for (int c = 0; c < classCount; c++)
                {
                    int dest = trs[c];
                    sig[c + 1] = dest < 0 ? -1 : part[dest];
                }
                sigs[s] = sig;
            }
            int[] newPart = PartitionBySignature(sigs);
            for (int s = 0; s < n; s++)
            {
                if (newPart[s] != part[s]) { changed = true; break; }
            }
            part = newPart;
        }

        return BuildMinimized(dfa, part);
    }

    private static int[] PartitionBySignature(int[][] sigs)
    {
        var map = new Dictionary<SigKey, int>();
        int[] result = new int[sigs.Length];
        for (int s = 0; s < sigs.Length; s++)
        {
            var key = new SigKey(sigs[s]);
            if (!map.TryGetValue(key, out int id)) { id = map.Count; map[key] = id; }
            result[s] = id;
        }
        return result;
    }

    private static Dfa BuildMinimized(Dfa dfa, int[] part)
    {
        int n = dfa.States.Count;
        int classCount = dfa.Alphabet.ClassCount;
        int partCount = 0;
        for (int s = 0; s < n; s++)
            if (part[s] + 1 > partCount) partCount = part[s] + 1;

        var newStates = new DfaState[partCount];
        for (int p = 0; p < partCount; p++)
            newStates[p] = new DfaState(p, classCount);

        // 各分割の代表状態から遷移・受理情報を転写。
        for (int p = 0; p < partCount; p++)
        {
            int rep = -1;
            for (int s = 0; s < n; s++)
                if (part[s] == p) { rep = s; break; }
            var srcTrs = dfa.States[rep].Transitions;
            var dstTrs = newStates[p].Transitions;
            for (int c = 0; c < classCount; c++)
            {
                int dest = srcTrs[c];
                dstTrs[c] = dest < 0 ? -1 : part[dest];
            }
            newStates[p].IsAccept = dfa.States[rep].IsAccept;
            newStates[p].AcceptTokenId = dfa.States[rep].AcceptTokenId;
        }

        return new Dfa(newStates, part[dfa.Start], dfa.Alphabet);
    }

    private sealed class SigKey
    {
        private readonly int[] _sig;
        public SigKey(int[] sig) => _sig = sig;

        public override bool Equals(object? obj)
        {
            if (obj is not SigKey other) return false;
            if (_sig.Length != other._sig.Length) return false;
            for (int i = 0; i < _sig.Length; i++)
                if (_sig[i] != other._sig[i]) return false;
            return true;
        }

        public override int GetHashCode()
        {
            int h = 0;
            for (int i = 0; i < _sig.Length; i++)
                h = unchecked(h * 31 + _sig[i]);
            return h;
        }
    }
}
