using System;
using System.Collections.Generic;

namespace AstFirst.Core.Lexing;

/// <summary>
/// 正規表現文字列を <see cref="RegexAst"/> にパースする再帰下降パーサ。
/// 文法:
///   alternation := concat ('|' concat)*
///   concat      := repeat*
///   repeat      := atom ('*' | '+' | '?')*
///   atom        := '(' alternation ')' | '[' charclass ']' | '.' | escape | literal
/// サポート: リテラル, ., \d\D\w\w\s\S, 文字クラス [..] [^..] と範囲, (), *, +, ?, |
/// </summary>
public sealed class RegexParser
{
    private readonly string _pattern;
    private int _pos;

    private RegexParser(string pattern) => _pattern = pattern;

    public static RegexAst Parse(string pattern)
    {
        if (pattern is null) throw new ArgumentNullException(nameof(pattern));
        var p = new RegexParser(pattern);
        var ast = p.ParseAlternation();
        if (!p.IsAtEnd) throw p.Error("末尾に解析できない文字があります");
        return ast;
    }

    private char Peek() => _pos < _pattern.Length ? _pattern[_pos] : '\0';
    private char Peek(int offset)
    {
        int i = _pos + offset;
        return i >= 0 && i < _pattern.Length ? _pattern[i] : '\0';
    }
    private char Advance() => _pattern[_pos++];
    private bool IsAtEnd => _pos >= _pattern.Length;

    private void Expect(char c)
    {
        if (Peek() != c) throw Error($"'{c}' が必要です");
        Advance();
    }

    private RegexParseException Error(string msg) => new RegexParseException(msg + $" (位置 {_pos})", _pos);

    // alternation := concat ('|' concat)*
    private RegexAst ParseAlternation()
    {
        var first = ParseConcat();
        if (Peek() != '|') return first;
        var options = new List<RegexAst> { first };
        while (Peek() == '|') { Advance(); options.Add(ParseConcat()); }
        return new AlternateAst(options);
    }

    // concat := repeat*
    private RegexAst ParseConcat()
    {
        var parts = new List<RegexAst>();
        while (!IsAtEnd && Peek() != '|' && Peek() != ')')
            parts.Add(ParseRepeat());
        if (parts.Count == 0) return new EmptyAst();
        if (parts.Count == 1) return parts[0];
        return new ConcatAst(parts);
    }

    // repeat := atom ('*' | '+' | '?')*
    private RegexAst ParseRepeat()
    {
        var atom = ParseAtom();
        while (true)
        {
            char c = Peek();
            if (c == '*') { Advance(); atom = new RepeatAst(atom, 0, null); }
            else if (c == '+') { Advance(); atom = new RepeatAst(atom, 1, null); }
            else if (c == '?') { Advance(); atom = new RepeatAst(atom, 0, 1); }
            else if (c == '{') atom = ParseRange(atom);
            else break;
        }
        return atom;
    }

    // {m} {m,} {m,n} {,n}
    private RegexAst ParseRange(RegexAst atom)
    {
        Expect('{');
        var sb1 = new System.Text.StringBuilder();
        while (Peek() != ',' && Peek() != '}' && Peek() != '\0') sb1.Append(Advance());
        int min = ParseRangeNumber(sb1);
        if (Peek() == '}')
        {
            Advance();
            return new RepeatAst(atom, min, min); // {m}
        }
        Advance(); // ','
        var sb2 = new System.Text.StringBuilder();
        while (Peek() != '}' && Peek() != '\0') sb2.Append(Advance());
        Expect('}');
        int? max = sb2.Length > 0 ? ParseRangeNumber(sb2) : (int?)null;
        return new RepeatAst(atom, min, max);
    }

    /// <summary>量指定子の数値をパース。空は 0、数値でなければ RegexParseException。</summary>
    private int ParseRangeNumber(System.Text.StringBuilder sb)
    {
        if (sb.Length == 0) return 0;
        if (!int.TryParse(sb.ToString(), out int v))
            throw Error($"量指定子 {{m,n}} の数値が不正です: '{sb}'");
        return v;
    }

    // atom := '(' alternation ')' | '[' charclass ']' | '.' | escape | literal
    private RegexAst ParseAtom()
    {
        char c = Peek();
        if (c == '(')
        {
            Advance();
            var inner = ParseAlternation();
            Expect(')');
            return inner;
        }
        if (c == '[') return ParseCharClass();
        if (c == '.') { Advance(); return new AnyCharAst(); }
        if (c == '\\') { Advance(); return ParseEscapeAtom(); }
        if (c == '\0') throw Error("予期しない入力の終端です");
        if (c == '*' || c == '+' || c == '?' || c == '|' || c == ')')
            throw Error($"予期しないメタ文字 '{c}'");
        // 上位サロゲート (補助面文字の1文字目) → 下位サロゲートも読んで ConcatAst
        if (c >= 0xD800 && c <= 0xDBFF)
        {
            Advance();
            if (Peek() >= 0xDC00 && Peek() <= 0xDFFF)
            {
                char lo = Advance();
                return new ConcatAst(new[] { new LiteralAst(c), new LiteralAst(lo) });
            }
            return new LiteralAst(c);
        }
        Advance();
        return new LiteralAst(c);
    }

    private RegexAst ParseEscapeAtom()
    {
        if (IsAtEnd) throw Error("エスケープの後に文字がありません");
        char c = Advance();
        switch (c)
        {
            case 'd': return new CharSetAst(Digits);
            case 'D': return new CharSetAst(Digits.Complement());
            case 'w': return new CharSetAst(Word);
            case 'W': return new CharSetAst(Word.Complement());
            case 's': return new CharSetAst(Whitespace);
            case 'S': return new CharSetAst(Whitespace.Complement());
            case 'n': return new LiteralAst('\n');
            case 't': return new LiteralAst('\t');
            case 'r': return new LiteralAst('\r');
            case '0': return new LiteralAst('\0');
            case 'u': return ParseUnicodeEscape(4);
            case 'U': return ParseUnicodeEscape(8);
            default: return new LiteralAst(c); // \+ \* \( \. 等 → リテラル
        }
    }

    /// <summary>\uXXXX (4桁) または \UXXXXXXXX (8桁) をパース。補助面はサロゲートペアに分解。</summary>
    private RegexAst ParseUnicodeEscape(int hexDigits)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < hexDigits; i++)
        {
            char h = Peek();
            if (!((h >= '0' && h <= '9') || (h >= 'a' && h <= 'f') || (h >= 'A' && h <= 'F')))
                throw Error("\\" + (hexDigits == 8 ? 'U' : 'u') + " の後に" + hexDigits + "桁の16進数が必要です");
            sb.Append(Advance());
        }
        int code = int.Parse(sb.ToString(), System.Globalization.NumberStyles.HexNumber);
        if (code <= 0xFFFF)
            return new LiteralAst((char)code);
        if (code > 0x10FFFF)
            throw Error("Unicode コードポイント U+" + code.ToString("X") + " は大きすぎます (最大 U+10FFFF)");
        int v = code - 0x10000;
        char hi = (char)(0xD800 + (v >> 10));
        char lo = (char)(0xDC00 + (v & 0x3FF));
        return new ConcatAst(new[] { new LiteralAst(hi), new LiteralAst(lo) });
    }

    private RegexAst ParseCharClass()
    {
        Expect('[');
        bool negate = Peek() == '^';
        if (negate) Advance();
        CharSet set = CharSet.Empty;
        // 先頭の ']' はリテラル (正規表現の慣習)
        if (Peek() == ']') { set = set.Union(CharSet.Single(']')); Advance(); }
        while (!IsAtEnd && Peek() != ']')
        {
            CharSet first = ReadClassAtom();
            if (Peek() == '-' && Peek(1) != ']' && Peek(1) != '\0')
            {
                Advance(); // '-'
                CharSet second = ReadClassAtom();
                char lo = SingleOf(first);
                char hi = SingleOf(second);
                if (lo > hi) throw Error($"文字クラスの範囲 '{lo}-{hi}' が不正です");
                set = set.Union(CharSet.FromRanges(new CharRange(lo, hi)));
            }
            else
            {
                set = set.Union(first);
            }
        }
        Expect(']');
        if (negate) set = set.Complement();
        return new CharSetAst(set);
    }

    private CharSet ReadClassAtom()
    {
        char c = Advance();
        if (c == '\\')
        {
            if (IsAtEnd) throw Error("エスケープの後に文字がありません");
            char e = Advance();
            switch (e)
            {
                case 'd': return Digits;
                case 'D': return Digits.Complement();
                case 'w': return Word;
                case 'W': return Word.Complement();
                case 's': return Whitespace;
                case 'S': return Whitespace.Complement();
                case 'n': return CharSet.Single('\n');
                case 't': return CharSet.Single('\t');
                case 'r': return CharSet.Single('\r');
                case 'u':
                case 'U':
                    throw new RegexParseException("文字クラス [..] 内では \\u/\\U (補助面文字) は使えません。トップレベルで指定してください。", -1);
                default: return CharSet.Single(e); // \] \- \\ 等 → リテラル
            }
        }
        return CharSet.Single(c);
    }

    private static char SingleOf(CharSet s)
    {
        if (s.Count == 1 && s.Ranges[0].Min == s.Ranges[0].Max) return s.Ranges[0].Min;
        throw new RegexParseException("文字クラスの範囲指定に単一文字でない要素が含まれます", -1);
    }

    private static readonly CharSet Digits = CharSet.FromRanges(new CharRange('0', '9'));
    private static readonly CharSet Word = CharSet.FromRanges(
        new CharRange('0', '9'), new CharRange('A', 'Z'),
        new CharRange('_', '_'), new CharRange('a', 'z'));
    private static readonly CharSet Whitespace = CharSet.FromChars(' ', '\t', '\n', '\r', '\f', '\v');
}
