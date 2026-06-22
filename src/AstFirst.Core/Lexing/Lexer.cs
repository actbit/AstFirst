using System;
using System.Collections.Generic;

namespace AstFirst.Core.Lexing;

/// <summary>
/// レクサが生成したトークン。字面は元ソースのスライスで持ち、
/// <see cref="Text"/> は必要な時だけ生成する (トークン化時のアロケーションなし)。
/// 行・列 (<see cref="StartLine"/> 等) は 1 ベース。
/// </summary>
public readonly struct LexToken
{
    public int TokenId { get; }
    public string Source { get; }
    public int Start { get; }
    public int End { get; }
    public int StartLine { get; }
    public int StartColumn { get; }
    public int EndLine { get; }
    public int EndColumn { get; }
    public int Length => End - Start;

    /// <summary>トークンの字面のスライス。コピーなし。</summary>
    public ReadOnlyMemory<char> Span => Source.AsMemory(Start, Length);

    /// <summary>字面の文字列表現 (必要な時だけ生成)。</summary>
    public string Text => Source.Substring(Start, Length);

    public LexToken(int tokenId, string source, int start, int end,
        int startLine = 1, int startColumn = 1, int endLine = 1, int endColumn = 1)
    {
        TokenId = tokenId;
        Source = source;
        Start = start;
        End = end;
        StartLine = startLine;
        StartColumn = startColumn;
        EndLine = endLine;
        EndColumn = endColumn;
    }
}

public sealed class LexException : Exception
{
    public int Position { get; }
    public LexException(string message, int position) : base(message) => Position = position;
}

/// <summary>
/// 統合 DFA を駆動してソースをトークン化する。最長一致＋優先度でトークンを決定。
/// <see cref="LexerRule.IsHidden"/> のトークンは結果から除外する。
/// 各トークンの開始/終了の行・列 (1 ベース) も計算する。
/// トークン化のコア (<see cref="TokenizeCore"/>) はジャグ配列 (int[][]) を直接受け取り、
/// 生成コードは Dfa/DfaState オブジェクトを構築せずに static 配列を渡せる。
/// ジャグ配列は各状態の遷移が1次元配列 (int[]) なので、2次元配列 (int[,]) よりアクセスが速い。
/// </summary>
public sealed class Lexer
{
    private readonly Dfa _dfa;
    private readonly HashSet<int> _hiddenTokens;
    private readonly string _source;

    public Lexer(Dfa dfa, IReadOnlyList<LexerRule> rules, string source)
    {
        _dfa = dfa;
        _source = source;
        _hiddenTokens = BuildHidden(rules);
    }

    /// <summary>DFA オブジェクト経由でトークン化 (手書き DFA 等のユースケース)。</summary>
    public List<LexToken> Tokenize()
    {
        int stateCount = _dfa.States.Count;
        // 各 DfaState.Transitions (int[]) をそのまま参照で集める (コピーなし)。
        var transitions = new int[stateCount][];
        var accept = new int[stateCount];
        for (int s = 0; s < stateCount; s++)
        {
            var st = _dfa.States[s];
            transitions[s] = st.Transitions;
            accept[s] = st.IsAccept ? st.AcceptTokenId : 0;
        }
        return TokenizeCore(transitions, accept, _dfa.Alphabet, _dfa.Start, _hiddenTokens, _source);
    }

    /// <summary>
    /// 事前直列化した配列 (遷移テーブル/受理トークン) を直接参照してトークン化。
    /// 生成コードは static 配列とキャッシュ済みの AlphabetPartition/rules を渡す
    /// (Dfa/DfaState オブジェクトの構築・コピーを毎回行わない)。
    /// </summary>
    public static List<LexToken> Tokenize(int[][] transitions, int[] acceptTokenIds, AlphabetPartition alphabet,
        int startState, IReadOnlyList<LexerRule> rules, string source)
        => TokenizeCore(transitions, acceptTokenIds, alphabet, startState, BuildHidden(rules), source);

    private static HashSet<int> BuildHidden(IReadOnlyList<LexerRule> rules)
    {
        var hidden = new HashSet<int>();
        for (int i = 0; i < rules.Count; i++)
            if (rules[i].IsHidden) hidden.Add(rules[i].TokenId);
        return hidden;
    }

    /// <summary>
    /// トークン化のコア。最長一致＋優先度でトークンを決定し、hidden を除外し、
    /// 各トークンの行・列 (1 ベース) を計算する。
    /// </summary>
    private static List<LexToken> TokenizeCore(int[][] transitions, int[] acceptTokenIds, AlphabetPartition alphabet,
        int startState, HashSet<int> hiddenTokens, string source)
    {
        // 入力長からトークン数を概算して初期容量を確保 (List の拡張コピーを抑える)。
        var result = new List<LexToken>(source.Length / 4 + 16);
        var src = source.AsSpan();
        var classTable = alphabet.GetClassTable(); // 文字→クラス を O(1) 参照 (2分探索を回避)
        int pos = 0;
        int line = 1, column = 1; // 現在位置の行・列 (1 ベース)
        while (pos < src.Length)
        {
            int current = startState;
            int lastAcceptToken = -1;
            int lastAcceptPos = pos;
            int i = pos;
            int[] curTransitions = transitions[startState];
            while (i < src.Length)
            {
                int cls = classTable[src[i]];
                int next = curTransitions[cls];
                if (next < 0) break;
                current = next;
                curTransitions = transitions[next];
                i++;
                int acceptId = acceptTokenIds[current];
                if (acceptId != 0)
                {
                    lastAcceptToken = acceptId;
                    lastAcceptPos = i;
                }
            }

            if (lastAcceptToken < 0)
                throw new LexException($"未認識の文字 '{src[pos]}' があります (位置 {pos})", pos);

            int startLine = line, startColumn = column;
            // トークン範囲 [pos, lastAcceptPos) の文字で行・列を進める。
            // '\n' で改行、'\r' は列に数えない (\r\n の \r を無視)。
            for (int j = pos; j < lastAcceptPos; j++)
            {
                if (src[j] == '\n') { line++; column = 1; }
                else if (src[j] != '\r') column++;
            }
            int endLine = line, endColumn = column;

            if (!hiddenTokens.Contains(lastAcceptToken))
            {
                // Substring せず、元ソースの (Start, End) と行・列だけ保持。Text は遅延生成。
                result.Add(new LexToken(lastAcceptToken, source, pos, lastAcceptPos, startLine, startColumn, endLine, endColumn));
            }
            pos = lastAcceptPos;
        }
        return result;
    }
}
