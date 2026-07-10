using System.Collections.Generic;
using System.Linq;

namespace AstFirst;

/// <summary>
/// 型の種別。単純型・関数型・配列型を区別する (<see cref="TypeSymbol.Kind"/>)。
/// </summary>
public enum TypeKind
{
    /// <summary>名前付きの単純型 (既定)。参照等価。</summary>
    Simple,
    /// <summary>関数型 (<see cref="FunctionTypeSymbol"/>)。構造等価。</summary>
    Function,
    /// <summary>配列型 (<see cref="ArrayTypeSymbol"/>)。構造等価。</summary>
    Array,
}

/// <summary>
/// 型変換の種別。<see cref="TypeSymbol.ClassifyConversion"/> が返す。
/// </summary>
public enum ConversionKind
{
    /// <summary>変換不可。</summary>
    None,
    /// <summary>同一型 (変換不要)。</summary>
    Identity,
    /// <summary>暗黙の変換 (派生→基底・widening 等)。キャスト不要。</summary>
    Implicit,
    /// <summary>明示的な変換 (キャストが必要)。既定では使われない (派生型で拡張)。</summary>
    Explicit,
}

/// <summary>
/// A type symbol. Simple types use reference equality (same instance = same type); use <see cref="IsAssignableFrom"/> for assignability across the <see cref="BaseType"/> chain.
/// 関数型・配列型は派生クラスで構造等価を提供します。
/// </summary>
/// <remarks>
/// 型を表すシンボル。単純型は参照等価 (同一インスタンスが同一型)。
/// 言語固有の型システムはユーザーが TypeSymbol のインスタンスを生成して運用します
/// (例: 整数型・真偽型を単一インスタンスで共有)。継承関係は <see cref="BaseType"/> で表現します。
/// 関数型 (<see cref="FunctionTypeSymbol"/>)・配列型 (<see cref="ArrayTypeSymbol"/>) は
/// 派生クラスでシグネチャによる構造等価と独自の代入可能性を提供します。
/// </remarks>
public class TypeSymbol
{
    public string Name { get; }
    public TypeSymbol? BaseType { get; }

    /// <summary>型の種別。既定は <see cref="TypeKind.Simple"/>。派生で override。</summary>
    public virtual TypeKind Kind => TypeKind.Simple;

    public TypeSymbol(string name, TypeSymbol? baseType = null)
    {
        Name = name;
        BaseType = baseType;
    }

    /// <summary><paramref name="from"/> をこの型に代入できるか (from が同一か派生型)。</summary>
    /// <remarks>派生型 (<see cref="FunctionTypeSymbol"/>/<see cref="ArrayTypeSymbol"/>) は構造的ルールで override します。</remarks>
    public virtual bool IsAssignableFrom(TypeSymbol from)
    {
        for (TypeSymbol? t = from; t is not null; t = t.BaseType)
            if (ReferenceEquals(t, this)) return true;
        return false;
    }

    /// <summary>
    /// この型の値を <paramref name="to"/> 型に変換する際の種別。
    /// 既定: 同一型は <see cref="ConversionKind.Identity"/>、派生→基底は <see cref="ConversionKind.Implicit"/>、それ以外は <see cref="ConversionKind.None"/>。
    /// 派生型で widening 変換 (int→long 等) を追加可能です。
    /// </summary>
    public virtual ConversionKind ClassifyConversion(TypeSymbol to)
    {
        if (ReferenceEquals(this, to)) return ConversionKind.Identity;
        if (to.IsAssignableFrom(this)) return ConversionKind.Implicit;
        return ConversionKind.None;
    }

    /// <summary>暗黙的に変換可能か (<see cref="ClassifyConversion"/> が Identity または Implicit)。</summary>
    public bool IsImplicitlyConvertible(TypeSymbol to)
        => ClassifyConversion(to) is ConversionKind.Identity or ConversionKind.Implicit;

    /// <summary>2 つの型がどちらかの方向で代入可能なら互換。</summary>
    public static bool AreCompatible(TypeSymbol a, TypeSymbol b)
        => a.IsAssignableFrom(b) || b.IsAssignableFrom(a);

    public override string ToString() => Name;
}

/// <summary>
/// 関数型シンボル (戻り値型 + 引数型リスト)。構造等価 (同じシグネチャ = 同一型)。
/// 代入可能性は、戻り値型が共変・引数型が反変 (delegate 互換性)。
/// </summary>
public class FunctionTypeSymbol : TypeSymbol
{
    public TypeSymbol ReturnType { get; }
    public IReadOnlyList<TypeSymbol> ParameterTypes { get; }

    public override TypeKind Kind => TypeKind.Function;

    public FunctionTypeSymbol(TypeSymbol returnType, IReadOnlyList<TypeSymbol> parameterTypes, string? name = null)
        : base(name ?? ComputeName(returnType, parameterTypes), null)
    {
        ReturnType = returnType;
        ParameterTypes = parameterTypes;
    }

    /// <summary>
    /// <paramref name="from"/> をこの関数型に代入できるか。
    /// 戻り値型は共変、引数型は反変で互換 (C#/TypeScript の delegate 互換性)。
    /// </summary>
    public override bool IsAssignableFrom(TypeSymbol from)
    {
        if (from is FunctionTypeSymbol other)
        {
            if (ParameterTypes.Count != other.ParameterTypes.Count) return false;
            if (!ReturnType.IsAssignableFrom(other.ReturnType)) return false;
            for (int i = 0; i < ParameterTypes.Count; i++)
                if (!ParameterTypes[i].IsAssignableFrom(other.ParameterTypes[i])) return false;
            return true;
        }
        return base.IsAssignableFrom(from);
    }

    /// <summary>同じ戻り値型・同じ引数型リスト (順序含む) で等価。</summary>
    public override bool Equals(object? obj)
        => obj is FunctionTypeSymbol f && ReturnType.Equals(f.ReturnType) && ParameterTypes.SequenceEqual(f.ParameterTypes);

    public override int GetHashCode()
    {
        int hash = ReturnType.GetHashCode();
        foreach (var p in ParameterTypes)
            hash = unchecked(hash * 31 + p.GetHashCode());
        return hash;
    }

    private static string ComputeName(TypeSymbol returnType, IReadOnlyList<TypeSymbol> parameterTypes)
        => "(" + string.Join(", ", parameterTypes) + ") => " + returnType;
}

/// <summary>
/// 配列型シンボル (要素型 + 次元数)。構造等価 (同じ要素型・同じランク = 同一型)。
/// 代入可能性は、要素型が共変でランクが一致する場合 (C#/Java の配列共変性)。
/// </summary>
public class ArrayTypeSymbol : TypeSymbol
{
    public TypeSymbol ElementType { get; }
    public int Rank { get; }

    public override TypeKind Kind => TypeKind.Array;

    public ArrayTypeSymbol(TypeSymbol elementType, int rank = 1, string? name = null)
        : base(name ?? ComputeName(elementType, rank), null)
    {
        ElementType = elementType;
        Rank = rank;
    }

    /// <summary>
    /// <paramref name="from"/> をこの配列型に代入できるか。
    /// 要素型が共変 (<see cref="ElementType"/>.IsAssignableFrom(other.ElementType)) でランクが一致する場合。
    /// </summary>
    public override bool IsAssignableFrom(TypeSymbol from)
    {
        if (from is ArrayTypeSymbol other)
        {
            if (Rank != other.Rank) return false;
            return ElementType.IsAssignableFrom(other.ElementType);
        }
        return base.IsAssignableFrom(from);
    }

    /// <summary>同じ要素型・同じランクで等価。</summary>
    public override bool Equals(object? obj)
        => obj is ArrayTypeSymbol a && Rank == a.Rank && ElementType.Equals(a.ElementType);

    public override int GetHashCode() => unchecked(ElementType.GetHashCode() * 31 + Rank);

    private static string ComputeName(TypeSymbol elementType, int rank)
        => elementType + (rank == 1 ? "[]" : "[" + new string(',', rank - 1) + "]");
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
