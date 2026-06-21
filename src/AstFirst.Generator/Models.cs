using System;
using System.Collections.Generic;
using System.Linq;

namespace AstFirst.Generator;

/// <summary>DSL から抽出した文法モデル。等価比較可能 (IncrementalGenerator のキャッシュ判定用)。
/// シンボル/構文ノードは一切持たず、文字列/整数/bool のみ。</summary>
public sealed class GrammarModel : IEquatable<GrammarModel>
{
    public string RootTypeFullName { get; }
    public IReadOnlyList<NodeModel> Nodes { get; }
    public IReadOnlyList<TokenDefModel> TokenDefs { get; }

    public GrammarModel(string rootTypeFullName, IReadOnlyList<NodeModel> nodes, IReadOnlyList<TokenDefModel> tokenDefs)
    {
        RootTypeFullName = rootTypeFullName;
        Nodes = nodes;
        TokenDefs = tokenDefs;
    }

    public bool Equals(GrammarModel? other) =>
        other is not null && RootTypeFullName == other.RootTypeFullName
        && SeqEqual(Nodes, other.Nodes) && SeqEqual(TokenDefs, other.TokenDefs);

    public override bool Equals(object? obj) => obj is GrammarModel m && Equals(m);
    public override int GetHashCode()
    {
        int h = StringComparer.Ordinal.GetHashCode(RootTypeFullName);
        for (int i = 0; i < Nodes.Count; i++) h = unchecked(h * 31 + Nodes[i].GetHashCode());
        return h;
    }

    private static bool SeqEqual<T>(IReadOnlyList<T> a, IReadOnlyList<T> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (!Equals(a[i], b[i])) return false;
        return true;
    }
}

/// <summary>AstNode 派生の1クラス。複数コンストラクタ = 複数生成規則。</summary>
public sealed class NodeModel : IEquatable<NodeModel>
{
    public string FullName { get; }
    public string BaseFullName { get; }       // 直接の基底 (非終端)
    public bool IsAbstract { get; }
    public IReadOnlyList<CtorModel> Constructors { get; }

    public NodeModel(string fullName, string baseFullName, bool isAbstract, IReadOnlyList<CtorModel> constructors)
    {
        FullName = fullName;
        BaseFullName = baseFullName;
        IsAbstract = isAbstract;
        Constructors = constructors;
    }

    public bool Equals(NodeModel? other) =>
        other is not null && FullName == other.FullName && BaseFullName == other.BaseFullName
        && IsAbstract == other.IsAbstract && Constructors.SequenceEqual(other.Constructors);
    public override bool Equals(object? obj) => obj is NodeModel n && Equals(n);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(FullName);
}

/// <summary>1つのコンストラクタ = 1つの生成規則の右辺。</summary>
public sealed class CtorModel : IEquatable<CtorModel>
{
    public IReadOnlyList<ParamModel> Parameters { get; }
    public CtorModel(IReadOnlyList<ParamModel> parameters) => Parameters = parameters;
    public bool Equals(CtorModel? other) => other is not null && Parameters.SequenceEqual(other.Parameters);
    public override bool Equals(object? obj) => obj is CtorModel c && Equals(c);
    public override int GetHashCode() => Parameters.Count;
}

/// <summary>コンストラクタ引数。</summary>
public sealed class ParamModel : IEquatable<ParamModel>
{
    public string TypeFullName { get; }
    public string? Name { get; }
    public string? Pattern { get; }     // [Pattern]
    public bool IsContext { get; }      // [Context]
    public int Priority { get; }        // [Priority]

    public ParamModel(string typeFullName, string? name, string? pattern, bool isContext, int priority)
    {
        TypeFullName = typeFullName;
        Name = name;
        Pattern = pattern;
        IsContext = isContext;
        Priority = priority;
    }

    public bool Equals(ParamModel? other) =>
        other is not null && TypeFullName == other.TypeFullName && Name == other.Name
        && Pattern == other.Pattern && IsContext == other.IsContext && Priority == other.Priority;
    public override bool Equals(object? obj) => obj is ParamModel p && Equals(p);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(TypeFullName);
}

/// <summary>字句定義: [Pattern] の正規表現と優先度。</summary>
public sealed class TokenDefModel : IEquatable<TokenDefModel>
{
    public string Key { get; }          // トークン種別の識別キー (型名 or 引数名)
    public string Pattern { get; }
    public int Priority { get; }
    public bool IsHidden { get; }

    public TokenDefModel(string key, string pattern, int priority, bool isHidden)
    {
        Key = key;
        Pattern = pattern;
        Priority = priority;
        IsHidden = isHidden;
    }

    public bool Equals(TokenDefModel? other) =>
        other is not null && Key == other.Key && Pattern == other.Pattern
        && Priority == other.Priority && IsHidden == other.IsHidden;
    public override bool Equals(object? obj) => obj is TokenDefModel t && Equals(t);
    public override int GetHashCode() => (Key, Pattern).GetHashCode();
}
