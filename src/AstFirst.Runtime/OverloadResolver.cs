using System.Collections.Generic;

namespace AstFirst;

/// <summary>
/// 関数オーバーロードの解決。候補 <see cref="FunctionSymbol"/> のリストと実引数の型から、
/// 最も適合するものを選ぶ (完全一致 → 暗黙変換で適合 → 不在/曖昧)。判定不能時は <see cref="DiagnosticBag"/> に診断を追加する。
/// </summary>
public static class OverloadResolver
{
    /// <summary>
    /// 候補 <paramref name="candidates"/> から実引数型 <paramref name="argTypes"/> に最も適合する関数を選ぶ。
    /// 完全一致を優先し、なければ暗黙変換で適合するもの、複数あれば曖昧として null。
    /// 適合なしなどは <paramref name="bag"/> に Error 診断を追加 (<paramref name="name"/> をメッセージに含む)。
    /// </summary>
    public static FunctionSymbol? Resolve(
        IReadOnlyList<FunctionSymbol> candidates,
        IReadOnlyList<TypeSymbol> argTypes,
        DiagnosticBag bag,
        SourceSpan span,
        string name)
    {
        // 1. 引数数で候補を絞る
        var arityMatched = new List<FunctionSymbol>();
        foreach (var c in candidates)
            if (c.Parameters.Count == argTypes.Count) arityMatched.Add(c);

        if (arityMatched.Count == 0)
        {
            bag.Error($"'{name}' に引数 {argTypes.Count} 個のオーバーロードはありません", span);
            return null;
        }

        // 2. 完全一致 (各引数型が同一インスタンス) を探す
        foreach (var c in arityMatched)
            if (IsExact(c, argTypes)) return c;

        // 3. 暗黙変換で適合するものを集める
        var applicable = new List<FunctionSymbol>();
        foreach (var c in arityMatched)
            if (IsApplicable(c, argTypes)) applicable.Add(c);

        if (applicable.Count == 0)
        {
            bag.Error($"'{name}' に引数型が適合するオーバーロードはありません", span);
            return null;
        }

        if (applicable.Count == 1) return applicable[0];

        // 4. 複数適合 → 曖昧
        bag.Error($"'{name}' の呼び出しは曖昧です ({applicable.Count} 個のオーバーロードが適合)", span);
        return null;
    }

    private static bool IsExact(FunctionSymbol f, IReadOnlyList<TypeSymbol> argTypes)
    {
        for (int i = 0; i < argTypes.Count; i++)
            if (!ReferenceEquals(f.Parameters[i].Type, argTypes[i])) return false;
        return true;
    }

    private static bool IsApplicable(FunctionSymbol f, IReadOnlyList<TypeSymbol> argTypes)
    {
        // 引数→仮引数 または 仮引数→引数 の暗黙変換が可能なら適合 (継承・widening 両方向を寛容に扱う)
        for (int i = 0; i < argTypes.Count; i++)
        {
            var param = f.Parameters[i].Type;
            if (!param.IsImplicitlyConvertible(argTypes[i]) && !argTypes[i].IsImplicitlyConvertible(param))
                return false;
        }
        return true;
    }
}
