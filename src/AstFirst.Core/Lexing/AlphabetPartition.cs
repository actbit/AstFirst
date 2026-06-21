using System.Collections.Generic;

namespace AstFirst.Core.Lexing;

/// <summary>
/// NFA の文字遷移ラベルから文字域 [0, 0x10000) を等価クラスに分割したもの。
/// 同じクラスの文字は全て同じ NFA 遷移を持つ。これにより DFA 駆動を
/// 「文字 → クラスID → 遷移先」の配列参照に落とせる (O(1))。
/// </summary>
public sealed class AlphabetPartition
{
    private readonly int[] _boundaries; // ソート済み。先頭 0、末尾 0x10000。

    private AlphabetPartition(int[] boundaries) => _boundaries = boundaries;

    /// <summary>生成コードから境界配列から構築するファクトリ。</summary>
    public static AlphabetPartition FromBoundaries(int[] boundaries)
    {
        if (boundaries == null || boundaries.Length < 2 || boundaries[0] != 0)
            throw new ArgumentException("境界配列は 0 で始まる必要があります。", nameof(boundaries));
        var copy = new int[boundaries.Length];
        for (int i = 0; i < boundaries.Length; i++) copy[i] = boundaries[i];
        return new AlphabetPartition(copy);
    }

    /// <summary>クラス数 = 境界数 - 1。</summary>
    public int ClassCount => _boundaries.Length - 1;

    public IReadOnlyList<int> Boundaries => _boundaries;

    public static AlphabetPartition Build(Nfa nfa)
    {
        var pts = new SortedSet<int> { 0, 0x10000 };
        for (int i = 0; i < nfa.States.Count; i++)
        {
            var trs = nfa.States[i].Transitions;
            for (int j = 0; j < trs.Count; j++)
            {
                var label = trs[j].Label;
                if (label is null) continue;
                for (int k = 0; k < label.Ranges.Count; k++)
                {
                    var r = label.Ranges[k];
                    pts.Add(r.Min);
                    pts.Add((int)r.Max + 1);
                }
            }
        }
        var arr = new int[pts.Count];
        pts.CopyTo(arr);
        return new AlphabetPartition(arr);
    }

    /// <summary>文字 c が属するクラス (0..ClassCount-1)。</summary>
    public int ClassOf(char c)
    {
        // 最大の i で _boundaries[i] <= c。クラス i = [_boundaries[i], _boundaries[i+1])。
        int lo = 0, hi = ClassCount; // _boundaries[ClassCount] == 0x10000
        while (lo < hi)
        {
            int mid = (lo + hi + 1) >> 1;
            if (_boundaries[mid] <= c) lo = mid;
            else hi = mid - 1;
        }
        return lo;
    }

    /// <summary>クラス cls の代表文字 (下限)。同一クラスの文字は同じ遷移を持つ。</summary>
    public char RepresentativeChar(int cls) => (char)_boundaries[cls];
}
