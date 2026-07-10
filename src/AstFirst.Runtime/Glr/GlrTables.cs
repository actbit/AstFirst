using System.Collections.Generic;

namespace AstFirst.Glr;

/// <summary>
/// 軽量 GLR ドライバ (<see cref="LightGlrDriver"/>) が消費するパーステーブル。
/// ParserEmitter が生成する static 配列と同一の 1 次元平坦化形式 (index = state * SymbolCount + sym)。
/// コンフリクトセルの全候補は AltKeys/AltActs で保持し、ドライバが fork の判断材料にする。
/// </summary>
public sealed class GlrTables
{
    /// <summary>アクション種別 (0=Error, 1=Shift, 2=Reduce, 3=Accept)。index = state*SymbolCount+sym。</summary>
    public byte[] ActionKind { get; }
    /// <summary>Shift=遷移先状態、Reduce=規則 id。</summary>
    public int[] ActionValue { get; }
    /// <summary>GOTO 表 (非終端)。index = state*SymbolCount+sym。-1 = なし。</summary>
    public int[] Goto { get; }
    /// <summary>規則 id → 左辺のシンボル id。</summary>
    public int[] ProdLhs { get; }
    /// <summary>規則 id → 右辺長。</summary>
    public int[] ProdLen { get; }
    /// <summary>状態 → デフォルト reduce 規則 id (テーブル圧縮)。-1 = なし。</summary>
    public int[] DefaultReduce { get; }
    /// <summary>Lexer の TokenId → シンボル id。-1 = 未知。</summary>
    public int[] TokenIdToSym { get; }
    /// <summary>コンフリクトセルのキー (state*SymbolCount+sym)。AltActs と対応。</summary>
    public int[] AltKeys { get; }
    /// <summary>各キーのフォールバック候補 (kind*1000000+value にエンコード)。</summary>
    public int[][] AltActs { get; }
    /// <summary>エラーメッセージ用のシンボル名 (任意)。</summary>
    public IReadOnlyList<string?>? SymNames { get; }

    public int StateCount { get; }
    public int SymbolCount { get; }
    public int EofSym { get; }
    public int StartState { get; }

    public GlrTables(byte[] actionKind, int[] actionValue, int[] gotoTable, int[] prodLhs, int[] prodLen,
        int[] defaultReduce, int[] tokenIdToSym, int[] altKeys, int[][] altActs,
        int stateCount, int symbolCount, int eofSym, int startState, IReadOnlyList<string?>? symNames = null)
    {
        ActionKind = actionKind;
        ActionValue = actionValue;
        Goto = gotoTable;
        ProdLhs = prodLhs;
        ProdLen = prodLen;
        DefaultReduce = defaultReduce;
        TokenIdToSym = tokenIdToSym;
        AltKeys = altKeys;
        AltActs = altActs;
        SymNames = symNames;
        StateCount = stateCount;
        SymbolCount = symbolCount;
        EofSym = eofSym;
        StartState = startState;
    }

    /// <summary>状態 state・先読み sym で取りうる全アクション (勝者順・重複なし)。
    /// ActionKind==Error かつ DefaultReduce 有効なら Reduce に展開する。
    /// 勝者が Error で候補もなければ空リスト (スタック死亡)。</summary>
    public IReadOnlyList<(byte Kind, int Value)> Actions(int state, int sym)
    {
        var result = new List<(byte, int)>();
        int idx = state * SymbolCount + sym;
        byte wk = ActionKind[idx];
        int wv = ActionValue[idx];
        int dr = state < DefaultReduce.Length ? DefaultReduce[state] : -1;

        (byte, int) winner = (wk, wv);
        if (wk == 0 && dr >= 0) winner = (2, dr);

        if (winner.Item1 != 0) result.Add(winner);

        for (int a = 0; a < AltKeys.Length; a++)
        {
            if (AltKeys[a] != idx) continue;
            foreach (var e in AltActs[a])
            {
                byte k = (byte)(e / 1000000);
                int v = e % 1000000;
                if (k == 0) continue; // Error 候補は無視
                bool dup = false;
                for (int i = 0; i < result.Count; i++)
                    if (result[i].Item1 == k && result[i].Item2 == v) { dup = true; break; }
                if (!dup) result.Add((k, v));
            }
            break;
        }
        return result;
    }
}
