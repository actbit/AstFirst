using System.Globalization;
using System.Text;
using AstFirst;

namespace SampleJson;

/// <summary>JSON パーサのサンプル。AstFirst で JSON (RFC 8259) をパースする。</summary>

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
    // 整数部は 0 単独、または 1-9 始まり。先頭ゼロ (01, 00) を拒否する。
    public JsonNumber([Pattern(@"-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?")] Token num)
    {
        Value = double.Parse(num.Text, CultureInfo.InvariantCulture);
    }
}

public sealed class JsonString : Json
{
    public string Value { get; }
    // 許可されたエスケープ (\" \\ \/ \b \f \n \r \t \uXXXX) のみ受理。不正エスケープは字句エラー。
    public JsonString([Pattern(@"""([^""\\]|\\[""\\/bfnrt]|\\u[0-9a-fA-F]{4})*""")] Token str)
    {
        Value = Unescape(str.Text[1..^1]);
    }

    /// <summary>JSON 文字列のエスケープを解除する (RFC 8259 §7)。キーでも共有。</summary>
    internal static string Unescape(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        int i = 0;
        while (i < raw.Length)
        {
            if (raw[i] == '\\' && i + 1 < raw.Length)
            {
                switch (raw[i + 1])
                {
                    case '"': sb.Append('"'); i += 2; break;
                    case '\\': sb.Append('\\'); i += 2; break;
                    case '/': sb.Append('/'); i += 2; break;
                    case 'b': sb.Append('\b'); i += 2; break;
                    case 'f': sb.Append('\f'); i += 2; break;
                    case 'n': sb.Append('\n'); i += 2; break;
                    case 'r': sb.Append('\r'); i += 2; break;
                    case 't': sb.Append('\t'); i += 2; break;
                    case 'u':
                        sb.Append(char.ConvertFromUtf32(
                            int.Parse(raw.AsSpan(i + 2, 4), NumberStyles.HexNumber)));
                        i += 6;
                        break;
                    default: // 正規表現で絞っているので到達しないはず (念のためそのまま残す)。
                        sb.Append(raw[i]); i++; break;
                }
            }
            else
            {
                sb.Append(raw[i]); i++;
            }
        }
        return sb.ToString();
    }
}

// --- 配列: [ Elements ] ---

public sealed class JsonArray : Json
{
    public JsonElements Elements { get; }
    public JsonArray([Pattern(@"\[")] Token lb, JsonElements elements, [Pattern(@"\]")] Token rb) { Elements = elements; }
}

// 要素リスト: 空 | 先頭要素 , 残り (カンマ区切り、末尾カンマ不可)。
// 先頭はカンマなし、2 個目以降を Tail でカンマ付きで連ねることで [v,] を構文エラーにする。
public abstract class JsonElements : AstNode { }
public sealed class NoElements : JsonElements { public NoElements() { } }
public sealed class ConsElements : JsonElements
{
    public Json Head { get; }
    public JsonElementsTail Tail { get; }
    public ConsElements(Json head, JsonElementsTail tail) { Head = head; Tail = tail; }
}
public abstract class JsonElementsTail : AstNode { }
public sealed class EndElementsTail : JsonElementsTail { public EndElementsTail() { } }
public sealed class ConsElementsTail : JsonElementsTail
{
    public Json Head { get; }
    public JsonElementsTail Tail { get; }
    public ConsElementsTail([Pattern(@",")] Token sep, Json head, JsonElementsTail tail) { Head = head; Tail = tail; }
}

// --- オブジェクト: { Members } または { } ---
// 空は括弧のみの専用規則に分離している (NoMembers ε を置くと "{" の直後の文字列キーを
// 誤って空リストへ還元してしまう LALR(1) lookahead の問題を回避するため)。
public sealed class JsonObject : Json
{
    public JsonMembers? Members { get; }   // null = 空オブジェクト
    public JsonObject([Pattern(@"\{")] Token lb, [Pattern(@"\}")] Token rb) { Members = null; }
    public JsonObject([Pattern(@"\{")] Token lb, JsonMembers members, [Pattern(@"\}")] Token rb) { Members = members; }
}

// メンバーリスト: 1つ以上。空は JsonObject の括弧のみ規則で処理する。
public abstract class JsonMembers : AstNode { }
public sealed class ConsMembers : JsonMembers
{
    public JsonMember Head { get; }
    public JsonMembersTail Tail { get; }
    public ConsMembers(JsonMember head, JsonMembersTail tail) { Head = head; Tail = tail; }
}
public abstract class JsonMembersTail : AstNode { }
public sealed class EndMembersTail : JsonMembersTail { public EndMembersTail() { } }
public sealed class ConsMembersTail : JsonMembersTail
{
    public JsonMember Head { get; }
    public JsonMembersTail Tail { get; }
    public ConsMembersTail([Pattern(@",")] Token sep, JsonMember head, JsonMembersTail tail) { Head = head; Tail = tail; }
}

// メンバー: "key" : value。キーは生トークンで受け取り JsonString.Unescape で解除。
public sealed class JsonMember : AstNode
{
    public string Key { get; }
    public Json Value { get; }
    public JsonMember([Pattern(@"""([^""\\]|\\[""\\/bfnrt]|\\u[0-9a-fA-F]{4})*""")] Token key, [Pattern(@":")] Token colon, Json value)
    {
        Key = JsonString.Unescape(key.Text[1..^1]);
        Value = value;
    }
}
