using SampleJson;

Console.WriteLine("=== AstFirst JSON Parser Sample ===\n");

// --- 正常系 (スカラー) ---
Show("null", "null");
Show("true", "true");
Show("false", "false");
Show("整数 42", "42");
Show("負の小数 -3.14e2", "-3.14e2");
Show("文字列 hello", @"""hello""");
Show("エスケープ \\n", @"""escaped\nstring""");
Show("ユニコードエスケープ \\u0041", @"""A""");   // A → A
Show("スラッシュエスケープ \\/", @"""a\/b""");      // \/ → /
Show("空白スキップ", "   42   ");

// --- 正常系 (配列・オブジェクト) ---
Show("配列 (数値)", "[1, 2, 3]");
Show("配列 (文字列混在)", """["a", "b", 1]""");
Show("空配列", "[]");
Show("オブジェクト (1要素)", """{"a": 1}""");
Show("オブジェクト (複数)", """{"a": 1, "b": [true, null]}""");
Show("空オブジェクト", "{}");
Show("ネスト", """{"x": {"y": [1, 2]}}""");
Show("文字列内のスペース/記号", """{"a b, c": "d: e"}""");

// --- 異常系 (JSON 仕様違反) ---
ShowError("先頭ゼロ 01 (不正)", "01");
ShowError("末尾カンマ [1,] (不正)", "[1,]");
ShowError("""末尾カンマ {"a":1,} (不正)""", """{"a":1,}""");
ShowError("未定義文字 @@@", "@@@");

Console.WriteLine("\n=== Done ===");

void Show(string title, string input)
{
    try
    {
        var r = JsonParser.Parse(input);
        if (r.Errors.Count > 0)
            Console.WriteLine($"{title} → 構文エラー: {string.Join("; ", r.Errors)}");
        else
            Console.WriteLine($"{title} → {Render(r.Ast as Json)}");
    }
    catch (AstFirst.Core.Lexing.LexException ex)
    {
        Console.WriteLine($"{title} → LexException: {ex.Message}");
    }
}

void ShowError(string title, string input)
{
    try
    {
        var r = JsonParser.Parse(input);
        if (r.Errors.Count > 0)
            Console.WriteLine($"{title} → 構文エラー (期待通り): {string.Join("; ", r.Errors)}");
        else
            Console.WriteLine($"{title} → {Render(r.Ast as Json)}  ※エラーにならなかった (想定外)");
    }
    catch (AstFirst.Core.Lexing.LexException ex)
    {
        Console.WriteLine($"{title} → LexException (期待通り): {ex.Message}");
    }
}

// AST → JSON 文字列 (ラウンドトリップ表示用)。
string Render(Json? json) => json switch
{
    null => "(null)",
    JsonNull => "null",
    JsonBool b => b.Value ? "true" : "false",
    JsonNumber n => n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
    JsonString s => "\"" + s.Value + "\"",
    JsonArray a => "[" + string.Join(", ", RenderElems(a.Elements)) + "]",
    JsonObject o => o.Members is { } m ? "{" + string.Join(", ", RenderMembers(m)) + "}" : "{}",
    _ => json.GetType().Name,
};

List<string> RenderElems(JsonElements elems)
{
    var list = new List<string>();
    if (elems is ConsElements c)
    {
        list.Add(Render(c.Head));
        for (var t = c.Tail; t is ConsElementsTail ct; t = ct.Tail)
            list.Add(Render(ct.Head));
    }
    return list;
}

List<string> RenderMembers(JsonMembers members)
{
    var list = new List<string>();
    if (members is ConsMembers c)
    {
        list.Add("\"" + c.Head.Key + "\": " + Render(c.Head.Value));
        for (var t = c.Tail; t is ConsMembersTail ct; t = ct.Tail)
            list.Add("\"" + ct.Head.Key + "\": " + Render(ct.Head.Value));
    }
    return list;
}
