using System.Collections.Generic;
using System.Linq;

namespace AstFirst.Core.Lexing;

/// <summary>正規表現パース時のエラー。</summary>
public sealed class RegexParseException : Exception
{
    public int Position { get; }
    public RegexParseException(string message, int position) : base(message) => Position = position;
}

/// <summary>正規表現の抽象構文木。</summary>
public abstract class RegexAst
{
    /// <summary>テスト・デバッグ用の正規準拠文字列表現。</summary>
    public abstract string ToCanonicalString();
}

/// <summary>空 (epsilon)。空文字列にマッチ。表示は &lt;e&gt;。</summary>
public sealed class EmptyAst : RegexAst
{
    public override string ToCanonicalString() => "<e>";
}

/// <summary>単一リテラル文字。</summary>
public sealed class LiteralAst : RegexAst
{
    public char Ch { get; }
    public LiteralAst(char ch) => Ch = ch;
    public override string ToCanonicalString() => Ch.ToString();
}

/// <summary>任意の1文字 ('.')。改行以外。</summary>
public sealed class AnyCharAst : RegexAst
{
    public override string ToCanonicalString() => ".";
}

/// <summary>文字クラス。</summary>
public sealed class CharSetAst : RegexAst
{
    public CharSet Set { get; }
    public CharSetAst(CharSet set) => Set = set;
    public override string ToCanonicalString() => "[" + Set + "]";
}

/// <summary>連接。</summary>
public sealed class ConcatAst : RegexAst
{
    public IReadOnlyList<RegexAst> Parts { get; }
    public ConcatAst(IReadOnlyList<RegexAst> parts) => Parts = parts;
    public override string ToCanonicalString()
        => "(" + string.Join("", Parts.Select(p => p.ToCanonicalString())) + ")";
}

/// <summary>選択 (|)。</summary>
public sealed class AlternateAst : RegexAst
{
    public IReadOnlyList<RegexAst> Options { get; }
    public AlternateAst(IReadOnlyList<RegexAst> options) => Options = options;
    public override string ToCanonicalString()
        => "(" + string.Join("|", Options.Select(o => o.ToCanonicalString())) + ")";
}

public enum RepeatKind { Star, Plus, Optional }

/// <summary>繰り返し (* + ?)。</summary>
public sealed class RepeatAst : RegexAst
{
    public RegexAst Inner { get; }
    public RepeatKind Kind { get; }
    public RepeatAst(RegexAst inner, RepeatKind kind) { Inner = inner; Kind = kind; }
    public override string ToCanonicalString()
    {
        char s = Kind switch { RepeatKind.Star => '*', RepeatKind.Plus => '+', _ => '?' };
        return Inner.ToCanonicalString() + s.ToString();
    }
}
