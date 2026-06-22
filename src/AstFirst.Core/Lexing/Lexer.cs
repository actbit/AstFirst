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
        _hiddenTokens = new HashSet<int>();
        for (int i = 0; i < rules.Count; i++)
            if (rules[i].IsHidden) _hiddenTokens.Add(rules[i].TokenId);
    }

    public List<LexToken> Tokenize()
    {
        var result = new List<LexToken>();
        var src = _source.AsSpan();
        var alphabet = _dfa.Alphabet;
        var states = _dfa.States;
        int pos = 0;
        int line = 1, column = 1; // 現在位置の行・列 (1 ベース)
        while (pos < src.Length)
        {
            int current = _dfa.Start;
            int lastAcceptToken = -1;
            int lastAcceptPos = pos;
            int i = pos;
            while (i < src.Length)
            {
                int cls = alphabet.ClassOf(src[i]);
                int next = states[current].Transitions[cls];
                if (next < 0) break;
                current = next;
                i++;
                if (states[current].IsAccept)
                {
                    lastAcceptToken = states[current].AcceptTokenId;
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

            if (!_hiddenTokens.Contains(lastAcceptToken))
            {
                // Substring せず、元ソースの (Start, End) と行・列だけ保持。Text は遅延生成。
                result.Add(new LexToken(lastAcceptToken, _source, pos, lastAcceptPos, startLine, startColumn, endLine, endColumn));
            }
            pos = lastAcceptPos;
        }
        return result;
    }
}
