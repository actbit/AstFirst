using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace AstFirst.Generator;

/// <summary>
/// [Grammar] ルートクラスから [Rule] static メソッドと [Token]/[Pattern] を走査し
/// <see cref="GrammarModel"/> に変換する。Roslyn シンボルを文字列/整数の POCO に落とす
/// (IncrementalGenerator のキャッシュが壊れないように)。
/// </summary>
public static class ModelExtraction
{
    private const string AstNodeFullName = "AstFirst.AstNode";
    private const string TokenFullName = "AstFirst.Token";
    private const string SemanticContextFullName = "AstFirst.SemanticContext";

    public static ImmutableArray<GrammarModel> ExtractAll(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol rootType) return ImmutableArray<GrammarModel>.Empty;
        var location = context.TargetNode?.GetLocation();
        var models = ImmutableArray.CreateBuilder<GrammarModel>(context.Attributes.Length);
        foreach (var grammarAttribute in context.Attributes)
            models.Add(Extract(context.SemanticModel.Compilation, rootType, location, grammarAttribute));
        return models.MoveToImmutable();
    }

    public static GrammarModel Extract(Compilation compilation, INamedTypeSymbol rootType, Location? rootLocation = null)
    {
        var grammarAttribute = rootType.GetAttributes()
            .LastOrDefault(attribute => IsAstFirstAttribute(attribute, "GrammarAttribute"));
        return Extract(compilation, rootType, rootLocation, grammarAttribute);
    }

    private static GrammarModel Extract(Compilation compilation, INamedTypeSymbol rootType, Location? rootLocation, AttributeData? grammarAttribute)
    {
        var astNodeBase = compilation.GetTypeByMetadataName(AstNodeFullName);
        var tokenBase = compilation.GetTypeByMetadataName(TokenFullName);
        var contextBase = compilation.GetTypeByMetadataName(SemanticContextFullName);
        var secondPassEnter = compilation.GetTypeByMetadataName("AstFirst.IOnSecondPassEnter");
        var secondPassExit = compilation.GetTypeByMetadataName("AstFirst.IOnSecondPassExit");

        string? mode = null;
        var parseMode = ParseMode.Lalr;
        var discovery = GrammarDiscovery.NamespaceAndTypeHierarchy;
        if (grammarAttribute is not null)
            foreach (var na in grammarAttribute.NamedArguments)
            {
                if (na.Key == "Mode" && na.Value.Value is string m) mode = m;
                if (na.Key == "ParseMode" && na.Value.Value is int pm) parseMode = (ParseMode)pm;
                if (na.Key == "Discovery" && na.Value.Value is int d) discovery = (GrammarDiscovery)d;
            }

        var nodes = new List<NodeModel>();
        var tokenDefs = new List<TokenDefModel>();
        var tokenDerivedWarnings = new List<string>();

        var allTypes = GetAllTypes(compilation.Assembly.GlobalNamespace).ToList();
        foreach (var type in allTypes)
        {
            if (type.TypeKind != Microsoft.CodeAnalysis.TypeKind.Class) continue;
            if (type.DeclaredAccessibility != Accessibility.Public) continue;

            bool sameNamespace = SymbolEqualityComparer.Default.Equals(type.ContainingNamespace, rootType.ContainingNamespace);
            bool inRootHierarchy = InheritsFromOrEquals(type, rootType);
            bool explicitPart = IsGrammarPart(type, rootType);
            bool includeNode = discovery switch
            {
                GrammarDiscovery.TypeHierarchy => inRootHierarchy || explicitPart,
                GrammarDiscovery.Namespace => sameNamespace || explicitPart,
                _ => sameNamespace || inRootHierarchy || explicitPart,
            };

            if (includeNode && astNodeBase is not null && InheritsFrom(type, astNodeBase))
                nodes.Add(ExtractNode(type, contextBase, astNodeBase, tokenBase, secondPassEnter, secondPassExit));
        }

        nodes.Sort((a, b) => string.CompareOrdinal(a.FullName, b.FullName));

        // 中間抽象のプロパティ継承: 各ノードの基底クラス (直接・間接) の Children を継承プロパティとして収集。
        nodes = ResolveInheritedProperties(nodes);

        // AstNode 派生クラスの [Rule] メソッドからトークン定義を抽出 (Token 型+[Token]/[Pattern] 引数)。
        foreach (var n in nodes)
            foreach (var td in ExtractTokenDefsFromRules(n))
                tokenDefs.Add(td);

        // 文法で実際に参照される Token 派生型だけを検証する。Token の名前空間には依存しない。
        var usedTokenTypes = new HashSet<string>();
        foreach (var n in nodes)
            foreach (var r in n.Rules)
                foreach (var p in r.Parameters)
                    if (p.IsToken && p.TypeFullName != TokenFullName)
                        usedTokenTypes.Add(p.TypeFullName);
        foreach (var type in allTypes)
            if (usedTokenTypes.Contains(type.ToDisplayString()) && !HasStringConstructor(type))
                tokenDerivedWarnings.Add(type.ToDisplayString());

        // [Skip] パターン ([Grammar] クラスまたはアセンブリ) を収集。
        var skipPatterns = new List<string>();
        foreach (var a in rootType.GetAttributes())
            if (IsAstFirstAttribute(a, "SkipAttribute") && a.ConstructorArguments.Length > 0 && a.ConstructorArguments[0].Value is string ss)
                skipPatterns.Add(ss);
        foreach (var a in compilation.Assembly.GetAttributes())
            if (IsAstFirstAttribute(a, "SkipAttribute") && a.ConstructorArguments.Length > 0 && a.ConstructorArguments[0].Value is string ss)
                skipPatterns.Add(ss);

        // [OnReduce]/[Enter]/[Exit] 属性付き意味解析ルール ([Grammar] ルートクラスの static メソッド) を収集。
        var analyzeRules = ExtractAnalyzeRules(rootType, astNodeBase, contextBase);

        return new GrammarModel(rootType.ToDisplayString(), nodes, Dedup(tokenDefs), skipPatterns, mode, rootLocation, tokenDerivedWarnings, analyzeRules, parseMode, discovery);
    }

    /// <summary>[OnReduce]/[Enter]/[Exit] 属性付き意味解析ルール ([Grammar] ルートクラスの static メソッド) を収集。
    /// 第1引数=対象 AstNode 派生型、第2引数=ctx。不正シグネチャは無視。</summary>
    private static List<AnalyzeRuleModel> ExtractAnalyzeRules(INamedTypeSymbol rootType, INamedTypeSymbol? astNodeBase, INamedTypeSymbol? contextBase)
    {
        var rules = new List<AnalyzeRuleModel>();
        var rootFullName = rootType.ToDisplayString();
        foreach (var member in rootType.GetMembers())
        {
            if (member is not IMethodSymbol method) continue;
            if (!method.IsStatic) continue;
            AnalyzePhase phase;
            if (HasAttribute(method, "EnterAttribute")) phase = AnalyzePhase.Enter;
            else if (HasAttribute(method, "ExitAttribute")) phase = AnalyzePhase.Exit;
            else if (HasAttribute(method, "OnReduceAttribute")) phase = AnalyzePhase.OnReduce;
            else continue;
            var ps = method.Parameters;
            // 第1引数 = AstNode 派生の対象ノード。
            if (ps.Length < 1 || astNodeBase is null || !InheritsFromOrEquals(ps[0].Type, astNodeBase)) continue;
            string targetNode = ps[0].Type.ToDisplayString();
            string ctxType = SemanticContextFullName;
            if (ps.Length >= 2 && contextBase is not null && InheritsFromOrEquals(ps[1].Type, contextBase))
                ctxType = ps[1].Type.ToDisplayString();
            rules.Add(new AnalyzeRuleModel(phase, targetNode, method.Name, ctxType, rootFullName));
        }
        return rules;
    }

    /// <summary>[Rule] 属性付き static メソッドを抽出して NodeModel を構築。</summary>
    private static NodeModel ExtractNode(INamedTypeSymbol type, INamedTypeSymbol? contextBase, INamedTypeSymbol? astNodeBase, INamedTypeSymbol? tokenBase, INamedTypeSymbol? secondPassEnter, INamedTypeSymbol? secondPassExit)
    {
        var rules = new List<RuleModel>();
        foreach (var member in type.GetMembers())
        {
            if (member is not IMethodSymbol method) continue;
            if (!method.IsStatic) continue;
            if (!HasAttribute(method, "RuleAttribute")) continue;
            var ps = ExtractParams(method.Parameters, contextBase, astNodeBase, tokenBase).ToList();
            rules.Add(new RuleModel(method.Name, ps));
        }

        var baseName = type.BaseType?.ToDisplayString() ?? "";

        int precPriority = 0;
        var precAssoc = AstFirst.Core.Parsing.Associativity.Left;
        foreach (var a in type.GetAttributes())
        {
            if (!IsAstFirstAttribute(a, "PrecedenceAttribute")) continue;
            if (a.ConstructorArguments.Length > 0 && a.ConstructorArguments[0].Value is int pr) precPriority = pr;
            foreach (var na in a.NamedArguments)
            {
                if (na.Key == "IsNonAssociative" && na.Value.Value is bool inn && inn) precAssoc = AstFirst.Core.Parsing.Associativity.NonAssoc;
                else if (na.Key == "IsRightAssociative" && na.Value.Value is bool ir && ir) precAssoc = AstFirst.Core.Parsing.Associativity.Right;
            }
        }
        var children = ExtractChildrenFromRules(rules);
        bool hasEnter = secondPassEnter is not null && ImplementsInterface(type, secondPassEnter);
        bool hasExit = secondPassExit is not null && ImplementsInterface(type, secondPassExit);
        return new NodeModel(type.ToDisplayString(), baseName, type.IsAbstract, rules, children, null, precPriority, precAssoc, hasEnter, hasExit);
    }

    /// <summary>型が指定インターフェースを実装するか (継承含む)。</summary>
    private static bool ImplementsInterface(INamedTypeSymbol type, INamedTypeSymbol iface)
    {
        foreach (var i in type.AllInterfaces)
            if (SymbolEqualityComparer.Default.Equals(i, iface)) return true;
        return false;
    }

    /// <summary>各ノードの基底クラス (直接・間接) の Children を継承プロパティとして収集し、NodeModel を再構築。
    /// 抽象基底クラスの [Rule] で宣言したプロパティを、具象サブクラスが : base(...) で初期化できるようにする。</summary>
    private static List<NodeModel> ResolveInheritedProperties(List<NodeModel> nodes)
    {
        var byFullName = new Dictionary<string, NodeModel>();
        foreach (var n in nodes) byFullName[n.FullName] = n;
        var result = new List<NodeModel>(nodes.Count);
        foreach (var n in nodes)
        {
            var inherited = new List<string>();
            var baseName = n.BaseFullName;
            while (!string.IsNullOrEmpty(baseName) && baseName != AstNodeFullName)
            {
                if (!byFullName.TryGetValue(baseName, out var baseNode)) break;
                foreach (var c in baseNode.Children)
                    if (!inherited.Contains(c.PropertyName))
                        inherited.Add(c.PropertyName);
                baseName = baseNode.BaseFullName;
            }
            result.Add(new NodeModel(n.FullName, n.BaseFullName, n.IsAbstract, n.Rules, n.Children,
                inherited, n.PrecedencePriority, n.PrecedenceAssoc, n.HasSecondPassEnter, n.HasSecondPassExit));
        }
        return result;
    }

    /// <summary>[Rule] メソッドの引数を型ベースで分類。</summary>
    private static IEnumerable<ParamModel> ExtractParams(IEnumerable<IParameterSymbol> parameters, INamedTypeSymbol? contextBase, INamedTypeSymbol? astNodeBase, INamedTypeSymbol? tokenBase)
    {
        foreach (var p in parameters)
        {
            var isContext = contextBase is not null && InheritsFromOrEquals(p.Type, contextBase);
            var isChild = astNodeBase is not null && p.Type.TypeKind == Microsoft.CodeAnalysis.TypeKind.Class && InheritsFromOrEquals(p.Type, astNodeBase);
            var isToken = tokenBase is not null && InheritsFrom(p.Type, tokenBase);
            int repeatMin = HasAttribute(p, "RepeatAttribute") ? GetRepeatMin(p) : -1;
            var (pattern, priority, kind) = GetPattern(p);
            yield return new ParamModel(p.Type.ToDisplayString(), p.Name, pattern, isContext, isChild, priority, isToken, repeatMin, kind);
        }
    }

    /// <summary>[Token]/[Pattern] から (Regex, Priority, Kind) を取得。未設定なら (null,0,null)。</summary>
    private static (string? regex, int priority, string? kind) GetPattern(Microsoft.CodeAnalysis.ISymbol symbol)
    {
        foreach (var a in symbol.GetAttributes())
        {
            if (!IsAstFirstAttribute(a, "PatternAttribute") && !IsAstFirstAttribute(a, "TokenAttribute")) continue;
            string? regex = a.ConstructorArguments.Length > 0 && a.ConstructorArguments[0].Value is string s ? s : null;
            int priority = 0;
            string? kind = null;
            foreach (var na in a.NamedArguments)
            {
                if (na.Key == "Priority" && na.Value.Value is int pr) priority = pr;
                if (na.Key == "Kind" && na.Value.Value is string k) kind = k;
            }
            return (regex, priority, kind);
        }
        return (null, 0, null);
    }

    /// <summary>[Repeat] の Min (0=Star=0回以上、1=Plus=1回以上)。既定は 1 (Plus)。</summary>
    private static int GetRepeatMin(Microsoft.CodeAnalysis.ISymbol symbol)
    {
        foreach (var a in symbol.GetAttributes())
        {
            if (!IsAstFirstAttribute(a, "RepeatAttribute")) continue;
            int min = 1;
            foreach (var na in a.NamedArguments)
                if (na.Key == "Min" && na.Value.Value is int m) min = m;
            return min;
        }
        return 1;
    }

    /// <summary>[Rule] の引数から AstNode 派生の子を収集 (partial プロパティ + Walker 用)。</summary>
    private static IReadOnlyList<ChildModel> ExtractChildrenFromRules(IReadOnlyList<RuleModel> rules)
    {
        var children = new List<ChildModel>();
        foreach (var r in rules)
            foreach (var p in r.Parameters)
            {
                if (!p.IsChild || p.Name is null) continue;
                bool isNullable = p.TypeFullName.EndsWith("?");
                var prop = CodeEmitter.Pascalize(p.Name);
                if (children.Exists(c => c.PropertyName == prop)) continue;
                // [Repeat] の子は IReadOnlyList<T> 型 (リスト展開された結果を格納)。
                var typeFullName = p.IsRepeat
                    ? "System.Collections.Generic.IReadOnlyList<" + p.TypeFullName + ">"
                    : p.TypeFullName;
                children.Add(new ChildModel(prop, typeFullName, isNullable, p.RepeatMin));
            }
        children.Sort((a, b) => string.CompareOrdinal(a.PropertyName, b.PropertyName));
        return children;
    }

    /// <summary>全 [Rule] メソッドの Token 型+[Token]/[Pattern] 引数からトークン定義を抽出。</summary>
    private static IEnumerable<TokenDefModel> ExtractTokenDefsFromRules(NodeModel node)
    {
        foreach (var r in node.Rules)
            foreach (var p in r.Parameters)
            {
                if (p.Pattern is null) continue;
                // Token 派生型 (共通 Token 以外) ならその型をキーに、それ以外は共通 Token 型。
                var key = p.IsToken ? p.TypeFullName : TokenFullName;
                yield return new TokenDefModel(key, p.Pattern, p.Priority, isHidden: false, p.Kind);
            }
    }

    private static bool HasAttribute(Microsoft.CodeAnalysis.ISymbol symbol, string attrName)
    {
        foreach (var a in symbol.GetAttributes())
            if (IsAstFirstAttribute(a, attrName)) return true;
        return false;
    }

    private static bool IsAstFirstAttribute(AttributeData attribute, string attributeName)
        => attribute.AttributeClass?.ToDisplayString() == "AstFirst." + attributeName;

    private static bool IsGrammarPart(INamedTypeSymbol type, INamedTypeSymbol rootType)
    {
        foreach (var attribute in type.GetAttributes())
        {
            if (!IsAstFirstAttribute(attribute, "GrammarPartAttribute")
                || attribute.ConstructorArguments.Length == 0)
                continue;
            if (attribute.ConstructorArguments[0].Value is INamedTypeSymbol configuredRoot
                && SymbolEqualityComparer.Default.Equals(configuredRoot, rootType))
                return true;
        }
        return false;
    }

    private static bool InheritsFromOrEquals(ITypeSymbol type, INamedTypeSymbol baseType)
    {
        if (SymbolEqualityComparer.Default.Equals(type, baseType)) return true;
        return InheritsFrom(type, baseType);
    }

    private static bool InheritsFrom(ITypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var t = type.BaseType; t is not null; t = t.BaseType)
            if (SymbolEqualityComparer.Default.Equals(t, baseType)) return true;
        return false;
    }

    /// <summary>生成された Parser から呼び出せる (string) コンストラクタがあるか。</summary>
    private static bool HasStringConstructor(INamedTypeSymbol type)
    {
        foreach (var ctor in type.Constructors)
        {
            if (ctor.IsStatic
                || ctor.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal))
                continue;
            var parms = ctor.Parameters;
            if (parms.Length == 1 && parms[0].Type.SpecialType == SpecialType.System_String)
                return true;
        }
        return false;
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol ns)
    {
        foreach (var t in ns.GetTypeMembers())
        {
            yield return t;
            foreach (var nested in GetNested(t)) yield return nested;
        }
        foreach (var child in ns.GetNamespaceMembers())
            if (child is INamespaceSymbol cns)
                foreach (var t in GetAllTypes(cns)) yield return t;
    }

    private static IEnumerable<INamedTypeSymbol> GetNested(INamedTypeSymbol type)
    {
        foreach (var t in type.GetTypeMembers())
        {
            yield return t;
            foreach (var n in GetNested(t)) yield return n;
        }
    }

    private static IReadOnlyList<TokenDefModel> Dedup(List<TokenDefModel> defs)
    {
        // 同一キー+パターンは1つに。異パターン同キーは優先度最小を採用せずそのまま残す (衝突として後で報告)。
        var seen = new Dictionary<(string, string), TokenDefModel>();
        foreach (var d in defs)
        {
            var k = (d.Key, d.Pattern);
            if (!seen.ContainsKey(k)) seen[k] = d;
        }
        var result = seen.Values.ToList();
        result.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.Key, b.Key);
            return c != 0 ? c : string.CompareOrdinal(a.Pattern, b.Pattern);
        });
        return result;
    }
}
