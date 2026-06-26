using System.Collections.Generic;
using System.Linq;
using SampleJson;

namespace AstFirst.Tests.EndToEnd;

/// <summary>
/// samples/JsonParser の生成パーサ (JsonParser) のエンドツーエンドテスト。
/// スカラー / 配列 / オブジェクト / エスケープ / 仕様違反のエラーを検証する。
/// オブジェクト解析は AstFirst 本体 (ModelToGrammar) のバグ修正後に初めて動くため、
/// その回帰テストも兼ねる。
/// </summary>
public class JsonParserTests
{
    private static Json Parse(string input)
    {
        var r = JsonParser.Parse(input);
        Assert.False(r.HasErrors, "構文エラー (エラーでないべき): " + string.Join("; ", r.Errors));
        return (Json)r.Ast!;
    }

    private static void AssertError(string input)
    {
        var r = JsonParser.Parse(input);
        Assert.True(r.HasErrors, "エラーになるべき入力が受理された: " + input);
    }

    // --- スカラー ---
    [Fact]
    public void ParsesNull() => Assert.IsType<JsonNull>(Parse("null"));

    [Fact]
    public void ParsesTrue() => Assert.True(((JsonBool)Parse("true")).Value);

    [Fact]
    public void ParsesFalse() => Assert.False(((JsonBool)Parse("false")).Value);

    [Fact]
    public void ParsesInteger() => Assert.Equal(42.0, ((JsonNumber)Parse("42")).Value);

    [Fact]
    public void ParsesNegativeExponent()
        => Assert.Equal(-314.0, ((JsonNumber)Parse("-3.14e2")).Value);

    [Fact]
    public void ParsesSimpleString()
        => Assert.Equal("hello", ((JsonString)Parse("\"hello\"")).Value);

    [Fact]
    public void SkipWhitespaceAroundToken()
        => Assert.Equal(42.0, ((JsonNumber)Parse("   42   ")).Value);

    // --- number の仕準拠 (先頭ゼロ拒否) ---
    [Fact]
    public void NumberRejectsLeadingZero() => AssertError("01");

    [Fact]
    public void NumberAcceptsSingleZero() => Assert.Equal(0.0, ((JsonNumber)Parse("0")).Value);

    // --- string のエスケープ解除 (RFC 8259 §7) ---
    [Fact]
    public void StringUnescapesNewline()
        => Assert.Equal("a\nb", ((JsonString)Parse("\"a\\nb\"")).Value);

    [Fact]
    public void StringUnescapesSolidus()
        => Assert.Equal("a/b", ((JsonString)Parse("\"a\\/b\"")).Value);

    [Fact]
    public void StringUnescapesQuote()
        => Assert.Equal("a\"b", ((JsonString)Parse("\"a\\\"b\"")).Value);

    [Fact]
    public void StringUnescapesUnicode()
        => Assert.Equal("A", ((JsonString)Parse("\"\\u0041\"")).Value);

    [Fact]
    public void StringPreservesInnerSpaces()
        => Assert.Equal("a b   c", ((JsonString)Parse("\"a b   c\"")).Value);

    // --- 配列 ---
    [Fact]
    public void ParsesEmptyArray()
        => Assert.IsType<NoElements>(((JsonArray)Parse("[]")).Elements);

    [Fact]
    public void ParsesArrayOfNumbers()
        => Assert.Equal(new[] { 1.0, 2.0, 3.0 }, NumberElements(((JsonArray)Parse("[1, 2, 3]")).Elements));

    [Fact]
    public void ParsesMixedArray()
    {
        var elems = AllElements(((JsonArray)Parse("""["x", 1, true, null]""")).Elements);
        Assert.Equal(4, elems.Count);
        Assert.IsType<JsonString>(elems[0]);
        Assert.IsType<JsonNumber>(elems[1]);
        Assert.IsType<JsonBool>(elems[2]);
        Assert.IsType<JsonNull>(elems[3]);
    }

    // --- オブジェクト (★今回の修正の核心) ---
    [Fact]
    public void ParsesEmptyObject()
        => Assert.IsType<JsonObjectEmpty>(Parse("{}"));

    [Fact]
    public void ParsesSingleMemberObject()
    {
        var o = (JsonObject)Parse("""{"a": 1}""");
        var cons = Assert.IsType<ConsMembers>(o.Members);
        Assert.Equal("a", cons.Head.Key);
        Assert.Equal(1.0, ((JsonNumber)cons.Head.Value).Value);
        Assert.IsType<EndMembersTail>(cons.Tail);
    }

    [Fact]
    public void ParsesMultiMemberObject()
    {
        var o = (JsonObject)Parse("""{"a": 1, "b": 2}""");
        Assert.Equal(new[] { ("a", 1.0), ("b", 2.0) }, NumberMembers(o.Members!));
    }

    [Fact]
    public void ParsesNestedObject()
    {
        var o = (JsonObject)Parse("""{"x": {"y": [1, 2]}}""");
        var outer = Assert.IsType<ConsMembers>(o.Members);
        var inner = Assert.IsType<JsonObject>(outer.Head.Value);
        var arr = Assert.IsType<JsonArray>(Assert.IsType<ConsMembers>(inner.Members).Head.Value);
        Assert.Equal(new[] { 1.0, 2.0 }, NumberElements(arr.Elements));
    }

    [Fact]
    public void ObjectKeyWithSpacesAndSymbols()
    {
        var o = (JsonObject)Parse("""{"a b, c": "d: e"}""");
        var cons = Assert.IsType<ConsMembers>(o.Members);
        Assert.Equal("a b, c", cons.Head.Key);
        Assert.Equal("d: e", ((JsonString)cons.Head.Value).Value);
    }

    [Fact]
    public void ObjectValueCanBeArray()
        => Assert.IsType<JsonArray>(
            Assert.IsType<ConsMembers>(((JsonObject)Parse("""{"a": [true, null]}""")).Members).Head.Value);

    // --- エラー (JSON 仕様違反) ---
    [Fact]
    public void RejectsTrailingCommaInArray() => AssertError("[1,]");

    [Fact]
    public void RejectsTrailingCommaInObject() => AssertError("""{"a":1,}""");

    // --- ヘルパー: リスト走査 ---
    private static List<Json> AllElements(JsonElements elems)
    {
        var list = new List<Json>();
        if (elems is ConsElements c)
        {
            list.Add(c.Head);
            for (var t = c.Tail; t is ConsElementsTail ct; t = ct.Tail)
                list.Add(ct.Head);
        }
        return list;
    }

    private static double[] NumberElements(JsonElements elems)
        => AllElements(elems).Select(e => ((JsonNumber)e).Value).ToArray();

    private static (string Key, double Value)[] NumberMembers(JsonMembers members)
    {
        var list = new List<(string, double)>();
        if (members is ConsMembers c)
        {
            list.Add((c.Head.Key, ((JsonNumber)c.Head.Value).Value));
            for (var t = c.Tail; t is ConsMembersTail ct; t = ct.Tail)
                list.Add((ct.Head.Key, ((JsonNumber)ct.Head.Value).Value));
        }
        return list.ToArray();
    }
}
