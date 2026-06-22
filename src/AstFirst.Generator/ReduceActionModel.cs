using System.Collections.Generic;

namespace AstFirst.Generator;

/// <summary>reduce 時のアクション: 対応する AST クラスのコンストラクタ呼び出し。</summary>
public sealed class ReduceActionModel
{
    /// <summary>AST クラスの完全修飾名 (new の対象)。</summary>
    public string AstTypeName { get; }

    /// <summary>コンストラクタ引数 (右辺の子 or SemanticContext 派生型)。</summary>
    public IReadOnlyList<ReduceParamModel> Parameters { get; }

    public ReduceActionModel(string astTypeName, IReadOnlyList<ReduceParamModel> parameters)
    {
        AstTypeName = astTypeName;
        Parameters = parameters;
    }
}

/// <summary>reduce 時のコンストラクタ引数。</summary>
public sealed class ReduceParamModel
{
    /// <summary>SemanticContext 派生型の引数なら ctx を注入。</summary>
    public bool IsContext { get; }

    /// <summary>キャスト先の型名。</summary>
    public string CastTypeName { get; }

    /// <summary>右辺の子のインデックス (IsContext=false のとき有効)。</summary>
    public int ChildIndex { get; }

    public ReduceParamModel(bool isContext, string castTypeName, int childIndex)
    {
        IsContext = isContext;
        CastTypeName = castTypeName;
        ChildIndex = childIndex;
    }
}
