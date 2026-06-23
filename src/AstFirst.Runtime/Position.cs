namespace AstFirst;

/// <summary>ソース上の位置 (オフセット・行・列)。1ベースの行/列。</summary>
public readonly struct Position(int offset, int line, int column) : IEquatable<Position>
{
    public int Offset { get; } = offset;
    public int Line { get; } = line;
    public int Column { get; } = column;

    public bool Equals(Position other) => Offset == other.Offset && Line == other.Line && Column == other.Column;
    public override bool Equals(object? obj) => obj is Position p && Equals(p);
    public override int GetHashCode() => (Offset, Line, Column).GetHashCode();
    public override string ToString() => "(" + Line + "," + Column + ")";
}

/// <summary>ソース範囲 [Start, End)。</summary>
public readonly struct SourceSpan(Position start, Position end) : IEquatable<SourceSpan>
{
    public Position Start { get; } = start;
    public Position End { get; } = end;

    public bool IsEmpty => Start.Equals(End);
    public bool Equals(SourceSpan other) => Start.Equals(other.Start) && End.Equals(other.End);
    public override bool Equals(object? obj) => obj is SourceSpan s && Equals(s);
    public override int GetHashCode() => (Start, End).GetHashCode();
    public override string ToString() => Start + "-" + End;

    /// <summary>2 つの span を覆う最小の span。</summary>
    public static SourceSpan Merge(SourceSpan a, SourceSpan b)
    {
        var start = (a.Start.Offset <= b.Start.Offset) ? a.Start : b.Start;
        var end = (a.End.Offset >= b.End.Offset) ? a.End : b.End;
        return new SourceSpan(start, end);
    }

    public static SourceSpan Merge(SourceSpan a, SourceSpan b, params SourceSpan[] rest)
    {
        var s = Merge(a, b);
        for (int i = 0; i < rest.Length; i++) s = Merge(s, rest[i]);
        return s;
    }
}
