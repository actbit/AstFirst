using System.Collections.Generic;
using AstFirst;

namespace SampleJson;

/// <summary>JSON パーサのサンプル。AstFirst で JSON をパースする。</summary>

[Grammar]
[Skip(@"\s+")]
public abstract class Json : AstNode { }

// --- 値の型 ---

public sealed class JsonNull : Json
{
    public JsonNull([Pattern(@"null", Priority = 1)] Token kw) { }
}

public sealed class JsonBool : Json
{
    public bool Value { get; }
    public JsonBool([Pattern(@"true|false", Priority = 1)] Token kw) { Value = kw.Text == "true"; }
}

public sealed class JsonNumber : Json
{
    public double Value { get; }
    public JsonNumber([Pattern(@"-?[0-9]+(\.[0-9]+)?([eE][+-]?[0-9]+)?")] Token num)
    {
        Value = double.Parse(num.Text, System.Globalization.CultureInfo.InvariantCulture);
    }
}

public sealed class JsonString : Json
{
    public string Value { get; }
    public JsonString([Pattern(@"""([^""\\]|\\.)*""")] Token str)
    {
        // 前後の " を除去、簡易エスケープ解除
        var raw = str.Text[1..^1];
        Value = raw.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\n", "\n").Replace("\\t", "\t");
    }
}

// このサンプルでは、JsonNull / JsonBool / JsonNumber / JsonString のみをパース。
// 配列・オブジェクトは今後の拡張 (リスト構文、ペア構文の追加) で対応。
