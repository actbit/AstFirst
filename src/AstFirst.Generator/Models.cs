using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace AstFirst.Generator;

/// <summary>パーザの実行モード (Generator 用。Runtime の <c>AstFirst.ParseMode</c> と値が一致。
/// Generator は Runtime を直接参照しないためミラーリングする)。</summary>
public enum ParseMode
{
    /// <summary>LALR(1) 確定パーサ (既定)。</summary>
    Lalr,
    /// <summary>軽量 GLR: コンフリクトセルで並行 fork し、収束でマージ。</summary>
    LightGlr,
}

/// <summary>DSL から抽出した文法モデル。等価比較可能 (IncrementalGenerator のキャッシュ判定用)。
/// シンボル/構文ノードは一切持たず、文字列/整数/bool のみ。</summary>
public sealed class GrammarModel : IEquatable<GrammarModel>
{
    public string RootTypeFullName { get; }
    public string? Mode { get; }                     // [Grammar(Mode=...)] の Mode
    public ParseMode ParseMode { get; }              // [Grammar(ParseMode=...)] の ParseMode (既定 Lalr)
    public IReadOnlyList<NodeModel> Nodes { get; }
    public IReadOnlyList<TokenDefModel> TokenDefs { get; }
    public IReadOnlyList<string> SkipPatterns { get; }
    /// <summary>(string) コンストラクタを持たない Token 派生型 (G7 で new DerivedType(token.Text) を生成できない)。診断用。</summary>
    public IReadOnlyList<string> TokenDerivedWarnings { get; }
    /// <summary>[OnReduce]/[Enter]/[Exit] 属性付き意味解析ルール ([Grammar] ルートクラスの static メソッド)。</summary>
    public IReadOnlyList<AnalyzeRuleModel> AnalyzeRules { get; }

    /// <summary>[Grammar] ルートクラスのソース位置 (診断報告用)。Equals/GetHashCode には含めない (IncrementalGenerator のキャッシュ判定を壊さないため)。</summary>
    public Location? RootLocation { get; }

    public GrammarModel(string rootTypeFullName, IReadOnlyList<NodeModel> nodes, IReadOnlyList<TokenDefModel> tokenDefs,
        IReadOnlyList<string>? skipPatterns = null, string? mode = null, Location? rootLocation = null,
        IReadOnlyList<string>? tokenDerivedWarnings = null, IReadOnlyList<AnalyzeRuleModel>? analyzeRules = null,
        ParseMode parseMode = ParseMode.Lalr)
    {
        RootTypeFullName = rootTypeFullName;
        Nodes = nodes;
        TokenDefs = tokenDefs;
        SkipPatterns = skipPatterns ?? Array.Empty<string>();
        Mode = mode;
        RootLocation = rootLocation;
        TokenDerivedWarnings = tokenDerivedWarnings ?? Array.Empty<string>();
        AnalyzeRules = analyzeRules ?? Array.Empty<AnalyzeRuleModel>();
        ParseMode = parseMode;
    }

    public bool Equals(GrammarModel? other) =>
        other is not null && RootTypeFullName == other.RootTypeFullName
        && SeqEqual(Nodes, other.Nodes) && SeqEqual(TokenDefs, other.TokenDefs)
        && SeqEqual(AnalyzeRules, other.AnalyzeRules) && ParseMode == other.ParseMode;

    /// <summary>いずれかのノードが IOnSecondPassEnter/Exit を実装するか、[Enter]/[Exit] ルールがあるか。
    /// いずれもなければ Walker/Walk を生成しない (空走査回避・ゼロコスト)。</summary>
    public bool HasSecondPass
    {
        get
        {
            for (int i = 0; i < Nodes.Count; i++)
                if (Nodes[i].HasSecondPassEnter || Nodes[i].HasSecondPassExit) return true;
            for (int i = 0; i < AnalyzeRules.Count; i++)
                if (AnalyzeRules[i].Phase is AnalyzePhase.Enter or AnalyzePhase.Exit) return true;
            return false;
        }
    }

    public override bool Equals(object? obj) => obj is GrammarModel m && Equals(m);
    public override int GetHashCode()
    {
        int h = StringComparer.Ordinal.GetHashCode(RootTypeFullName);
        for (int i = 0; i < Nodes.Count; i++) h = unchecked(h * 31 + Nodes[i].GetHashCode());
        for (int i = 0; i < AnalyzeRules.Count; i++) h = unchecked(h * 31 + AnalyzeRules[i].GetHashCode());
        h = unchecked(h * 31 + (int)ParseMode);
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
    public IReadOnlyList<ChildModel> Children { get; }   // [Rule] 引数の子 (partial プロパティ + Walker 用)
    /// <summary>基底クラスから継承するプロパティ名 (PascalCase・順序付き)。具象クラスの partial で再定義を避け、: base(...) に渡す。</summary>
    public IReadOnlyList<string> InheritedPropertyNames { get; }
    public int PrecedencePriority { get; }    // [Precedence] の Priority (0=未設定)
    public AstFirst.Core.Parsing.Associativity PrecedenceAssoc { get; }
    /// <summary>IOnSecondPassEnter を実装するか (Walker 生成判定・呼び分け用)。</summary>
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
    /// <summary>-1=非リスト、0=Star (0回以上)，1=Plus (1回以上)。[Repeat] 付き引数は IReadOnlyList&lt;T&gt; に展開。</summary>
    public int RepeatMin { get; }
    public bool IsRepeat => RepeatMin >= 0;  // [Repeat] 付き (IReadOnlyList<T> に展開)
    public int Priority { get; }        // [Token]/[Pattern] の Priority

    public ParamModel(string typeFullName, string? name, string? pattern, bool isContext, bool isChild, int priority, bool isToken = false, int repeatMin = -1)
    {
        TypeFullName = typeFullName;
        Name = name;
        Pattern = pattern;
        IsContext = isContext;
        IsChild = isChild;
        IsToken = isToken;
        RepeatMin = repeatMin;
        Priority = priority;
    }

    public bool Equals(ParamModel? other) =>
        other is not null && TypeFullName == other.TypeFullName && Name == other.Name
        && Pattern == other.Pattern && IsContext == other.IsContext && IsChild == other.IsChild
        && IsToken == other.IsToken && RepeatMin == other.RepeatMin && Priority == other.Priority;
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

/// <summary>AST ノードの子 (AstNode 派生の [Rule] 引数 = partial プロパティ)。Walker 生成で子の再帰ウォークにも使う。</summary>
public sealed class ChildModel : IEquatable<ChildModel>
{
    public string PropertyName { get; }     // [Rule] 引数名 (= partial プロパティ名)
    public string TypeFullName { get; }
    public bool IsNullable { get; }
    /// <summary>-1=非リスト、0=Star、1=Plus。[Repeat] 付きは IReadOnlyList&lt;T&gt;。GetChildren で foreach 展開。</summary>
    public int RepeatMin { get; }
    public bool IsRepeat => RepeatMin >= 0;  // [Repeat] 付き (GetChildren で foreach 展開)

    public ChildModel(string propertyName, string typeFullName, bool isNullable, int repeatMin = -1)
    {
        PropertyName = propertyName;
        TypeFullName = typeFullName;
        IsNullable = isNullable;
        RepeatMin = repeatMin;
    }

    public bool Equals(ChildModel? other) =>
        other is not null && PropertyName == other.PropertyName
        && TypeFullName == other.TypeFullName && IsNullable == other.IsNullable && RepeatMin == other.RepeatMin;
    public override bool Equals(object? obj) => obj is ChildModel c && Equals(c);
    public override int GetHashCode() => (PropertyName, TypeFullName).GetHashCode();
}

/// <summary>意味解析ルールのフェーズ。</summary>
public enum AnalyzePhase
{
    /// <summary>1パス目: reduce 時 (ボトムアップ)。[OnReduce] 属性。Walker 不要 (コンストラクタ経路)。</summary>
    OnReduce,
    /// <summary>2パス目: ノードに入る時 (トップダウン)。[Enter] 属性。Walker の Enter フェーズ。</summary>
    Enter,
    /// <summary>2パス目: ノードを出る時。[Exit] 属性。Walker の Exit フェーズ。</summary>
    Exit,
}

/// <summary>[OnReduce]/[Enter]/[Exit] 属性付き static メソッド = 意味解析ルール。
/// [Grammar] ルートクラス内に宣言。第1引数が対象ノード、第2引数が ctx。</summary>
public sealed class AnalyzeRuleModel : IEquatable<AnalyzeRuleModel>
{
    public AnalyzePhase Phase { get; }
    /// <summary>対象ノード型 (第1引数の AstNode 派生型の完全名)。</summary>
    public string TargetNodeFullName { get; }
    /// <summary>[Grammar] ルートクラスのメソッド名 (静的呼出しの対象)。</summary>
    public string MethodName { get; }
    /// <summary>ctx 型 (第2引数の SemanticContext 派生型の完全名)。</summary>
    public string CtxTypeFullName { get; }
    /// <summary>[Grammar] ルートクラスの完全名 (静的呼出しのレシーバ)。</summary>
    public string GrammarClassFullName { get; }

    public AnalyzeRuleModel(AnalyzePhase phase, string targetNodeFullName, string methodName, string ctxTypeFullName, string grammarClassFullName)
    {
        Phase = phase;
        TargetNodeFullName = targetNodeFullName;
        MethodName = methodName;
        CtxTypeFullName = ctxTypeFullName;
        GrammarClassFullName = grammarClassFullName;
    }

    public bool Equals(AnalyzeRuleModel? other) =>
        other is not null && Phase == other.Phase && TargetNodeFullName == other.TargetNodeFullName
        && MethodName == other.MethodName && CtxTypeFullName == other.CtxTypeFullName
        && GrammarClassFullName == other.GrammarClassFullName;
    public override bool Equals(object? obj) => obj is AnalyzeRuleModel a && Equals(a);
    public override int GetHashCode() => (Phase, TargetNodeFullName, MethodName).GetHashCode();
}
