using System.Collections.Generic;

namespace AstFirst;

/// <summary>
/// A type symbol (reference equality: same instance = same type). Use <see cref="IsAssignableFrom"/> for assignability across the <see cref="BaseType"/> chain.
/// </summary>
/// <remarks>
/// 型を表すシンボル。参照等価 (同一インスタンスが同一型)。
/// 言語固有の型システムはユーザーが TypeSymbol のインスタンスを生成して運用します
/// (例: 整数型・真偽型を単一インスタンスで共有)。継承関係は <see cref="BaseType"/> で表現します。
/// </remarks>
public sealed class TypeSymbol
{
    public string Name { get; }
    public TypeSymbol? BaseType { get; }

    public TypeSymbol(string name, TypeSymbol? baseType = null)
    {
        Name = name;
        BaseType = baseType;
    }

    /// <summary><paramref name="from"/> をこの型に代入できるか (from が同一か派生型)。</summary>
    public bool IsAssignableFrom(TypeSymbol from)
    {
        for (TypeSymbol? t = from; t is not null; t = t.BaseType)
            if (ReferenceEquals(t, this)) return true;
        return false;
    }

    /// <summary>2 つの型がどちらかの方向で代入可能なら互換。</summary>
    public static bool AreCompatible(TypeSymbol a, TypeSymbol b)
        => a.IsAssignableFrom(b) || b.IsAssignableFrom(a);

    public override string ToString() => Name;
}

/// <summary>
/// AST ノードに型を対応付ける (型推論・型チェックの結果を蓄積)。
/// 意味解析のウォーク中で <see cref="SetType"/> で各ノードの型を記録し、後で <see cref="TypeOf"/> で参照する。
/// </summary>
public sealed class TypeContext
{
    private readonly Dictionary<AstNode, TypeSymbol> _types = new();

    /// <summary>ノードに型を記録する (上書き可)。</summary>
    public void SetType(AstNode node, TypeSymbol type) => _types[node] = type;

    /// <summary>ノードの型を取得 (未設定なら null)。</summary>
    public TypeSymbol? TypeOf(AstNode node) => _types.TryGetValue(node, out var t) ? t : null;

    /// <summary>ノードに型が設定済みか。</summary>
    public bool HasType(AstNode node) => _types.ContainsKey(node);
}
