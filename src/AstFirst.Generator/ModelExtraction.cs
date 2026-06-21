using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace AstFirst.Generator;

/// <summary>
/// [Grammar] ルートクラスから AstNode/Token 派生と [Pattern] を走査し
/// <see cref="GrammarModel"/> に変換する。Roslyn シンボルを文字列/整数の POCO に落とす
/// (IncrementalGenerator のキャッシュが壊れないように)。
/// </summary>
public static class ModelExtraction
{
    private const string AstNodeFullName = "AstFirst.AstNode";
    private const string TokenFullName = "AstFirst.Token";
    private const string SemanticContextFullName = "AstFirst.SemanticContext";

    public static GrammarModel? Extract(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol rootType) return null;
        return Extract(context.SemanticModel.Compilation, rootType);
    }

    public static GrammarModel Extract(Compilation compilation, INamedTypeSymbol rootType)
    {
        var astNodeBase = compilation.GetTypeByMetadataName(AstNodeFullName);
        var tokenBase = compilation.GetTypeByMetadataName(TokenFullName);
        var contextBase = compilation.GetTypeByMetadataName(SemanticContextFullName);

        var nodes = new List<NodeModel>();
        var tokenDefs = new List<TokenDefModel>();

        foreach (var type in GetAllTypes(compilation.Assembly.GlobalNamespace))
        {
            if (type.TypeKind != TypeKind.Class) continue;

            if (astNodeBase is not null && InheritsFrom(type, astNodeBase))
                nodes.Add(ExtractNode(type, contextBase));

            if (tokenBase is not null && InheritsFrom(type, tokenBase))
                foreach (var td in ExtractTokenDefsFromCtors(type)) tokenDefs.Add(td);
            else
                foreach (var td in ExtractInlineTokenDefs(type, tokenBase)) tokenDefs.Add(td);
        }

        nodes.Sort((a, b) => a.FullName.CompareTo(b.FullName));

        // [Skip] パターン ([Grammar] クラスまたはアセンブリ) を収集。
        var skipPatterns = new List<string>();
        foreach (var a in rootType.GetAttributes())
            if (a.AttributeClass?.Name == "SkipAttribute" && a.ConstructorArguments.Length > 0 && a.ConstructorArguments[0].Value is string ss)
                skipPatterns.Add(ss);

        return new GrammarModel(rootType.ToDisplayString(), nodes, Dedup(tokenDefs), skipPatterns);
    }

    private static NodeModel ExtractNode(INamedTypeSymbol type, INamedTypeSymbol? contextBase)
    {
        var ctors = new List<CtorModel>();
        foreach (var ctor in type.Constructors)
        {
            if (ctor.IsStatic || ctor.DeclaredAccessibility == Accessibility.Private) continue;
            ctors.Add(new CtorModel(ExtractParams(ctor.Parameters, contextBase).ToList()));
        }
        var baseName = type.BaseType?.ToDisplayString() ?? "";

        int precPriority = 0;
        var precAssoc = AstFirst.Core.Parsing.Associativity.Left;
        foreach (var a in type.GetAttributes())
        {
            if (a.AttributeClass?.Name != "PrecedenceAttribute") continue;
            if (a.ConstructorArguments.Length > 0 && a.ConstructorArguments[0].Value is int pr) precPriority = pr;
            foreach (var na in a.NamedArguments)
            {
                if (na.Key == "IsNonAssociative" && na.Value.Value is bool inn && inn) precAssoc = AstFirst.Core.Parsing.Associativity.NonAssoc;
                else if (na.Key == "IsRightAssociative" && na.Value.Value is bool ir && ir) precAssoc = AstFirst.Core.Parsing.Associativity.Right;
            }
        }
        return new NodeModel(type.ToDisplayString(), baseName, type.IsAbstract, ctors, precPriority, precAssoc);
    }

    private static IEnumerable<ParamModel> ExtractParams(IEnumerable<IParameterSymbol> parameters, INamedTypeSymbol? contextBase)
    {
        foreach (var p in parameters)
        {
            var isContext = contextBase is not null && InheritsFrom(p.Type, contextBase);
            var (pattern, priority, assoc) = GetPattern(p);
            yield return new ParamModel(p.Type.ToDisplayString(), p.Name, pattern, isContext, priority, assoc);
        }
    }

    /// <summary>[Pattern] から (Regex, Priority, IsRightAssociative) を取得。未設定なら (null,0,false)。</summary>
    private static (string? regex, int priority, AstFirst.Core.Parsing.Associativity assoc) GetPattern(ISymbol symbol)
    {
        foreach (var a in symbol.GetAttributes())
        {
            if (a.AttributeClass?.Name != "PatternAttribute") continue;
            string? regex = a.ConstructorArguments.Length > 0 && a.ConstructorArguments[0].Value is string s ? s : null;
            int priority = 0;
            bool isRight = false;
            bool isNon = false;
            foreach (var na in a.NamedArguments)
            {
                if (na.Key == "Priority" && na.Value.Value is int pr) priority = pr;
                if (na.Key == "IsRightAssociative" && na.Value.Value is bool ir) isRight = ir;
                if (na.Key == "IsNonAssociative" && na.Value.Value is bool inn) isNon = inn;
            }
            var assoc = isNon ? AstFirst.Core.Parsing.Associativity.NonAssoc
                       : isRight ? AstFirst.Core.Parsing.Associativity.Right
                       : AstFirst.Core.Parsing.Associativity.Left;
            return (regex, priority, assoc);
        }
        return (null, 0, AstFirst.Core.Parsing.Associativity.Left);
    }

    private static IEnumerable<TokenDefModel> ExtractTokenDefsFromCtors(INamedTypeSymbol tokenType)
    {
        foreach (var ctor in tokenType.Constructors)
        {
            foreach (var p in ctor.Parameters)
            {
                var (pattern, priority, _) = GetPattern(p);
                if (pattern is null) continue;
                yield return new TokenDefModel(tokenType.ToDisplayString(), pattern, priority, isHidden: false);
            }
        }
    }

    private static IEnumerable<TokenDefModel> ExtractInlineTokenDefs(INamedTypeSymbol type, INamedTypeSymbol? tokenBase)
    {
        foreach (var ctor in type.Constructors)
        {
            foreach (var p in ctor.Parameters)
            {
                var (pattern, priority, _) = GetPattern(p);
                if (pattern is null) continue;
                if (tokenBase is not null && !InheritsFromOrEquals(p.Type, tokenBase)) continue;
                var key = p.Type.ToDisplayString();
                yield return new TokenDefModel(key, pattern, priority, isHidden: false);
            }
        }
    }

    private static string? GetStringAttribute(ISymbol symbol, string attrName, int ctorArgIndex)
    {
        foreach (var a in symbol.GetAttributes())
        {
            if (a.AttributeClass?.Name == attrName)
            {
                if (a.ConstructorArguments.Length > ctorArgIndex
                    && a.ConstructorArguments[ctorArgIndex].Value is string s)
                    return s;
            }
        }
        return null;
    }

    private static int GetIntAttribute(ISymbol symbol, string attrName, int ctorArgIndex)
    {
        foreach (var a in symbol.GetAttributes())
        {
            if (a.AttributeClass?.Name == attrName)
            {
                if (a.ConstructorArguments.Length > ctorArgIndex
                    && a.ConstructorArguments[ctorArgIndex].Value is int i)
                    return i;
            }
        }
        return 0;
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
            int c = a.Key.CompareTo(b.Key);
            return c != 0 ? c : a.Pattern.CompareTo(b.Pattern);
        });
        return result;
    }
}
