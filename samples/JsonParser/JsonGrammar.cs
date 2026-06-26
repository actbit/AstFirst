using System.Globalization;
using System.Text;
using AstFirst;

namespace SampleJson;

/// <summary>JSON パーサのサンプル。AstFirst で JSON (RFC 8259) をパースする ([Rule] static モデル)。</summary>

[Grammar]
[Skip(@"\s+")]
public abstract partial class Json : AstNode { }

// --- 値の型 ---

public sealed partial class JsonNull : Json
{
    [Rule]
    public static void Null([Token(@"null", Priority = 1)] Token kw) { }
}

public sealed partial class JsonBool : Json
{
    public bool Value { get; private set; }
    [Rule]
    public static void Bool([Token(@"true|false", Priority = 1)] Token kw) { }
    partial void OnReduce() { Value = Kw.Text == "true"; }
}

public sealed partial class JsonNumber : Json
{
    public double Value { get; private set; }
    // 整数部は 0 単独、または 1-9 始まり。先頭ゼロ (01, 00) を拒否する。
    [Rule]
    public static void Number([Token(@"-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?")] Token num) { }
    partial void OnReduce() { Value = double.Parse(Num.Text, CultureInfo.InvariantCulture); }
}

public sealed partial class JsonString : Json
{
    public string Value { get; private set; } = "";
    // 許可されたエスケープ (\" \\ \/ \b \f \n \r \t \uXXXX) のみ受理。不正エスケープは字句エラー。
    [Rule]
    public static void StrToken([Token(@"""([^""\\]|\\[""\\/bfnrt]|\\u[0-9a-fA-F]{4})*""")] Token str) { }
    partial void OnReduce() { Value = Unescape(Str.Text[1..^1]); }

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

public sealed partial class JsonArray : Json
{
    [Rule]
    public static void Array([Token(@"\[")] Token lb, JsonElements elements, [Token(@"\]")] Token rb) { }
}

// 要素リスト: 空 | 先頭要素 , 残り (カンマ区切り、末尾カンマ不可)。
// 先頭はカンマなし、2 個目以降を Tail でカンマ付きで連ねることで [v,] を構文エラーにする。
public abstract partial class JsonElements : AstNode { }
public sealed partial class NoElements : JsonElements
{
    [Rule]
    public static void Empty() { }
}
public sealed partial class ConsElements : JsonElements
{
    [Rule]
    public static void Cons(Json head, JsonElementsTail tail) { }
}
public abstract partial class JsonElementsTail : AstNode { }
public sealed partial class EndElementsTail : JsonElementsTail
{
    [Rule]
    public static void End() { }
}
public sealed partial class ConsElementsTail : JsonElementsTail
{
    [Rule]
    public static void Cons([Token(@",")] Token sep, Json head, JsonElementsTail tail) { }
}

// --- オブジェクト: { Members } または { } ---
// 空は括弧のみの専用規則 (JsonObjectEmpty) に分離している (NoMembers ε を置くと "{" の直後の文字列キーを
// 誤って空リストへ還元してしまう LALR(1) lookahead の問題を回避するため)。
// 新モデルは 1クラス1 [Rule] のため、空/非空を別クラスへ分割する。
public sealed partial class JsonObject : Json
{
    [Rule]
    public static void Object([Token(@"\{")] Token lb, JsonMembers members, [Token(@"\}")] Token rb) { }
}
public sealed partial class JsonObjectEmpty : Json
{
    [Rule]
    public static void Empty([Token(@"\{")] Token lb, [Token(@"\}")] Token rb) { }
}

// メンバーリスト: 1つ以上。空は JsonObjectEmpty で処理する。
public abstract partial class JsonMembers : AstNode { }
public sealed partial class ConsMembers : JsonMembers
{
    [Rule]
    public static void Cons(JsonMember head, JsonMembersTail tail) { }
}
public abstract partial class JsonMembersTail : AstNode { }
public sealed partial class EndMembersTail : JsonMembersTail
{
    [Rule]
    public static void End() { }
}
public sealed partial class ConsMembersTail : JsonMembersTail
{
    [Rule]
    public static void Cons([Token(@",")] Token sep, JsonMember head, JsonMembersTail tail) { }
}

// メンバー: "key" : value。キーは生トークンで受け取り JsonString.Unescape で解除。
// keyTok → partial プロパティ KeyTok。ユーザー宣言の文字列プロパティは Key (衝突回避)。
public sealed partial class JsonMember : AstNode
{
    public string Key { get; private set; } = "";
    [Rule]
    public static void Member([Token(@"""([^""\\]|\\[""\\/bfnrt]|\\u[0-9a-fA-F]{4})*""")] Token keyTok,
                              [Token(@":")] Token colon, Json value) { }
    partial void OnReduce() { Key = JsonString.Unescape(KeyTok.Text[1..^1]); }
}
