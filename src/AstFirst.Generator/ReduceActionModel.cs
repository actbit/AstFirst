using System.Collections.Generic;

namespace AstFirst.Generator;

/// <summary>reduce 時のアクション: 対応する AST クラスのコンストラクタ呼び出し。</summary>
public sealed class ReduceActionModel
{
    /// <summary>AST クラスの完全修飾名 (new の対象)。</summary>
    public string AstTypeName { get; }

    /// <summary>[Rule] メソッド名 (RuleName プロパティに設定、複数[Rule]クラスで OnReduce 内の分岐に使用)。</summary>
    public string RuleName { get; }

    /// <summary>コンストラクタ引数 (右辺の子 or SemanticContext 派生型)。</summary>
    public IReadOnlyList<ReduceParamModel> Parameters { get; }

    public ReduceActionModel(string astTypeName, string ruleName, IReadOnlyList<ReduceParamModel> parameters)
    {
        AstTypeName = astTypeName;
        RuleName = ruleName;
        Parameters = parameters;
    }

    /// <summary>コンフリクト表示等で具象クラス名を示すため。</summary>
    public override string ToString() => AstTypeName;
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

/// <summary>
/// [Repeat] から展開したリスト非終端 (Plus: List_T → item | List_T item、Star: + ε) の reduce アクション。
/// List&lt;T&gt; を構築して返す (破壊的に Add。LALR では各リスト値は1回だけ消費されるため安全)。
/// </summary>
public sealed class ListReduceActionModel
{
    /// <summary>リスト要素の完全修飾型名。</summary>
    public string ElementType { get; }

    /// <summary>true = List_T item (再帰・既存リストに Add)、false = item (1要素の新規リスト)。</summary>
    public bool IsRecursive { get; }

    /// <summary>true = ε (空リスト、Star のみ)。IsRecursive=false のとき有効。</summary>
    public bool IsEmpty { get; }

    public ListReduceActionModel(string elementType, bool isRecursive, bool isEmpty = false)
    {
        ElementType = elementType;
        IsRecursive = isRecursive;
        IsEmpty = isEmpty;
    }

    public override string ToString() => "List<" + ElementType + ">";
}

/// <summary>
/// 抽象クラス経由の単位規則 (Base → N、N は抽象非終端) の reduce アクション。
/// N の値 (実際は具象サブクラスのインスタンス) をそのまま返す (新規 AST は作らない)。
/// 中間抽象クラス (Root → Mid → Leaf) の Mid を文法非終端として機能させるため。
/// </summary>
public sealed class PassThroughActionModel
{
    public override string ToString() => "pass-through";
}
