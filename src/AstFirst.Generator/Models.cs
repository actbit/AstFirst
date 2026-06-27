using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace AstFirst.Generator;

/// <summary>DSL から抽出した文法モデル。等価比較可能 (IncrementalGenerator のキャッシュ判定用)。
/// シンボル/構文ノードは一切持たず、文字列/整数/bool のみ。</summary>
public sealed class GrammarModel : IEquatable<GrammarModel>
{
    public string RootTypeFullName { get; }
    public string? Mode { get; }                     // [Grammar(Mode=...)] の Mode
    public IReadOnlyList<NodeModel> Nodes { get; }
    public IReadOnlyList<TokenDefModel> TokenDefs { get; }
    public IReadOnlyList<string> SkipPatterns { get; }
    /// <summary>(string) コンストラクタを持たない Token 派生型 (G7 で new DerivedType(token.Text) を生成できない)。診断用。</summary>
    public IReadOnlyList<string> TokenDerivedWarnings { get; }

    /// <summary>[Grammar] ルートクラスのソース位置 (診断報告用)。Equals/GetHashCode には含めない (IncrementalGenerator のキャッシュ判定を壊さないため)。</summary>
    public Location? RootLocation { get; }

    public GrammarModel(string rootTypeFullName, IReadOnlyList<NodeModel> nodes, IReadOnlyList<TokenDefModel> tokenDefs,
        IReadOnlyList<string>? skipPatterns = null, string? mode = null, Location? rootLocation = null,
        IReadOnlyList<string>? tokenDerivedWarnings = null)
    {
        RootTypeFullName = rootTypeFullName;
        Nodes = nodes;
        TokenDefs = tokenDefs;
        SkipPatterns = skipPatterns ?? Array.Empty<string>();
        Mode = mode;
        RootLocation = rootLocation;
        TokenDerivedWarnings = tokenDerivedWarnings ?? Array.Empty<string>();
    }

    public bool Equals(GrammarModel? other) =>
        other is not null && RootTypeFullName == other.RootTypeFullName
        && SeqEqual(Nodes, other.Nodes) && SeqEqual(TokenDefs, other.TokenDefs);

    /// <summary>いずれかのノードが IOnSecondPassEnter/Exit を実装するか。未実装なら WalkSecondPass を生成しない (空走査回避)。</summary>
    public bool HasSecondPass
    {
        get
        {
            for (int i = 0; i < Nodes.Count; i++)
                if (Nodes[i].HasSecondPassEnter || Nodes[i].HasSecondPassExit) return true;
            return false;
        }
    }

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

/// <summary>AstNode 派生の1クラス。1つの [Rule] static メソッド = 1つの生成規則。</summary>
public sealed class NodeModel : IEquatable<NodeModel>
{
    public string FullName { get; }
    public string BaseFullName { get; }       // 直接の基底 (非終端 = 親)
    public bool IsAbstract { get; }
    public IReadOnlyList<RuleModel> Rules { get; }      // [Rule] static メソッド (1クラス1つだがリストで保持)
    public IReadOnlyList<ChildModel> Children { get; }   // [Rule] 引数の子 (partial プロパティ + Listener 用)
    /// <summary>基底クラスから継承するプロパティ名 (PascalCase・順序付き)。具象クラスの partial で再定義を避け、: base(...) に渡す。</summary>
    public IReadOnlyList<string> InheritedPropertyNames { get; }
    public int PrecedencePriority { get; }    // [Precedence] の Priority (0=未設定)
    public AstFirst.Core.Parsing.Associativity PrecedenceAssoc { get; }
    /// <summary>IOnSecondPassEnter を実装するか (WalkSecondPass 生成判定・呼び分け用)。</summary>
    public bool HasSecondPassEnter { get; }
    /// <summary>IOnSecondPassExit を実装するか。</summary>
    public bool HasSecondPassExit { get; }

    public NodeModel(string fullName, string baseFullName, bool isAbstract, IReadOnlyList<RuleModel> rules,
        IReadOnlyList<ChildModel>? children = null,
        IReadOnlyList<string>? inheritedPropertyNames = null,
        int precedencePriority = 0, AstFirst.Core.Parsing.Associativity precedenceAssoc = AstFirst.Core.Parsing.Associativity.Left,
        bool hasSecondPassEnter = false, bool hasSecondPassExit = false)
    {
        FullName = fullName;
        BaseFullName = baseFullName;
        IsAbstract = isAbstract;
        Rules = rules;
        Children = children ?? Array.Empty<ChildModel>();
        InheritedPropertyNames = inheritedPropertyNames ?? Array.Empty<string>();
        PrecedencePriority = precedencePriority;
        PrecedenceAssoc = precedenceAssoc;
        HasSecondPassEnter = hasSecondPassEnter;
        HasSecondPassExit = hasSecondPassExit;
    }

    public bool Equals(NodeModel? other) =>
        other is not null && FullName == other.FullName && BaseFullName == other.BaseFullName
        && IsAbstract == other.IsAbstract && Rules.SequenceEqual(other.Rules) && Children.SequenceEqual(other.Children)
        && InheritedPropertyNames.SequenceEqual(other.InheritedPropertyNames)
        && PrecedencePriority == other.PrecedencePriority && PrecedenceAssoc == other.PrecedenceAssoc
        && HasSecondPassEnter == other.HasSecondPassEnter && HasSecondPassExit == other.HasSecondPassExit;
    public override bool Equals(object? obj) => obj is NodeModel n && Equals(n);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(FullName);
}

/// <summary>[Rule] static メソッド = 1つの生成規則の右辺。</summary>
public sealed class RuleModel : IEquatable<RuleModel>
{
    /// <summary>[Rule] メソッドの名前 (任意・識別用)。</summary>
    public string MethodName { get; }
    public IReadOnlyList<ParamModel> Parameters { get; }
    public RuleModel(string methodName, IReadOnlyList<ParamModel> parameters) { MethodName = methodName; Parameters = parameters; }
    public bool Equals(RuleModel? other) => other is not null && MethodName == other.MethodName && Parameters.SequenceEqual(other.Parameters);
    public override bool Equals(object? obj) => obj is RuleModel r && Equals(r);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(MethodName);
}

/// <summary>[Rule] メソッドの引数。型ベースで分類。</summary>
public sealed class ParamModel : IEquatable<ParamModel>
{
    public string TypeFullName { get; }
    public string? Name { get; }
    public string? Pattern { get; }     // [Token]/[Pattern] (終端)
    public bool IsContext { get; }      // SemanticContext 派生型の引数 (ctx・最後)
    public bool IsChild { get; }        // AstNode 派生 (右辺の子・partial プロパティ生成対象)
    public bool IsToken { get; }        // Token 派生型 (共通 Token 以外)。TokenDef の Key を型名にする。
    public bool IsRepeat { get; }       // [Repeat] (1回以上の繰り返し)。IReadOnlyList<T> に展開。
    public int Priority { get; }        // [Token]/[Pattern] の Priority

    public ParamModel(string typeFullName, string? name, string? pattern, bool isContext, bool isChild, int priority, bool isToken = false, bool isRepeat = false)
    {
        TypeFullName = typeFullName;
        Name = name;
        Pattern = pattern;
        IsContext = isContext;
        IsChild = isChild;
        IsToken = isToken;
        IsRepeat = isRepeat;
        Priority = priority;
    }

    public bool Equals(ParamModel? other) =>
        other is not null && TypeFullName == other.TypeFullName && Name == other.Name
        && Pattern == other.Pattern && IsContext == other.IsContext && IsChild == other.IsChild
        && IsToken == other.IsToken && IsRepeat == other.IsRepeat && Priority == other.Priority;
    public override bool Equals(object? obj) => obj is ParamModel p && Equals(p);
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(TypeFullName);
}

/// <summary>字句定義: [Token]/[Pattern] の正規表現と優先度。</summary>
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

/// <summary>AST ノードの子 (AstNode 派生の [Rule] 引数 = partial プロパティ)。Listener 生成で子の再帰ウォークにも使う。</summary>
public sealed class ChildModel : IEquatable<ChildModel>
{
    public string PropertyName { get; }     // [Rule] 引数名 (= partial プロパティ名)
    public string TypeFullName { get; }
    public bool IsNullable { get; }
    public bool IsRepeat { get; }           // [Repeat]: IReadOnlyList<T>。GetChildren で foreach 展開。

    public ChildModel(string propertyName, string typeFullName, bool isNullable, bool isRepeat = false)
    {
        PropertyName = propertyName;
        TypeFullName = typeFullName;
        IsNullable = isNullable;
        IsRepeat = isRepeat;
    }

    public bool Equals(ChildModel? other) =>
        other is not null && PropertyName == other.PropertyName
        && TypeFullName == other.TypeFullName && IsNullable == other.IsNullable && IsRepeat == other.IsRepeat;
    public override bool Equals(object? obj) => obj is ChildModel c && Equals(c);
    public override int GetHashCode() => (PropertyName, TypeFullName).GetHashCode();
}
