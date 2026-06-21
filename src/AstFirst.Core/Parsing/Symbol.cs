namespace AstFirst.Core.Parsing;

/// <summary>文法記号。終端 (トークン) または非終端。値型。</summary>
public readonly struct Symbol : System.IEquatable<Symbol>
{
    public int Id { get; }
    public string Name { get; }
    public bool IsTerminal { get; }

    public Symbol(int id, string name, bool isTerminal)
    {
        Id = id;
        Name = name;
        IsTerminal = isTerminal;
    }

    public bool Equals(Symbol other) => Id == other.Id;
    public override bool Equals(object? obj) => obj is Symbol s && Equals(s);
    public override int GetHashCode() => Id;
    public override string ToString() => IsTerminal ? "'" + Name + "'" : Name;
}
