using System.Collections.Generic;

namespace AstFirst;

/// <summary>
/// 宣言されたシンボルの読み取り専用インターフェース (変数・関数・型など)。
/// <see cref="SymbolEntry"/> および型付きシンボル (<see cref="VariableSymbol"/>/<see cref="FunctionSymbol"/>) が実装します。
/// </summary>
public interface ISymbol
{
    /// <summary>シンボル名。</summary>
    string Name { get; }

    /// <summary>宣言位置。</summary>
    SourceSpan Span { get; }

    /// <summary>宣言されたスコープの深さ (ルート=0)。</summary>
    int Depth { get; }
}

/// <summary>
/// 変数シンボル。名前・宣言位置・型を持つ。不変。
/// <see cref="ScopedSymbolTable"/> では <see cref="SymbolEntry"/> の <see cref="SymbolEntry.Value"/> に格納して運用する
/// (<see cref="SymbolEntry.AsVariable"/> で取り出す)。
/// </summary>
public sealed class VariableSymbol : ISymbol
{
    public string Name { get; }
    public SourceSpan Span { get; }
    public int Depth { get; }

    /// <summary>変数の型。</summary>
    public TypeSymbol Type { get; }

    public VariableSymbol(string name, SourceSpan span, int depth, TypeSymbol type)
    {
        Name = name;
        Span = span;
        Depth = depth;
        Type = type;
    }

    public override string ToString() => Name + ": " + Type;
}

/// <summary>
/// 関数の仮引数。名前・型を持つ。
/// </summary>
public sealed class FunctionParam
{
    public string Name { get; }
    public TypeSymbol Type { get; }

    public FunctionParam(string name, TypeSymbol type)
    {
        Name = name;
        Type = type;
    }

    public override string ToString() => Name + ": " + Type;
}

/// <summary>
/// 関数シンボル。名前・宣言位置・関数型・仮引数リストを持つ。不変。
/// <see cref="ScopedSymbolTable"/> では <see cref="SymbolEntry"/> の <see cref="SymbolEntry.Value"/> に格納して運用する
/// (<see cref="SymbolEntry.AsFunction"/> で取り出す)。オーバーロード解決は <see cref="OverloadResolver"/> で行う。
/// </summary>
public sealed class FunctionSymbol : ISymbol
{
    public string Name { get; }
    public SourceSpan Span { get; }
    public int Depth { get; }

    /// <summary>関数の型 (戻り値型 + 引数型リスト)。</summary>
    public FunctionTypeSymbol Type { get; }

    /// <summary>名前付き仮引数リスト。</summary>
    public IReadOnlyList<FunctionParam> Parameters { get; }

    public FunctionSymbol(string name, SourceSpan span, int depth, FunctionTypeSymbol type, IReadOnlyList<FunctionParam> parameters)
    {
        Name = name;
        Span = span;
        Depth = depth;
        Type = type;
        Parameters = parameters;
    }

    public override string ToString() => Type + " " + Name;
}
