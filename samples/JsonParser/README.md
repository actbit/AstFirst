# JsonParser

**JA:** JSON パーサのサンプル。RFC 8259 に準拠した JSON をパースする（`null` / `true` / `false` / `number` / `string` / `array` / `object`）。`[Skip(@"\s+")]`、キーワード優先度、正規表現（数値・文字列エスケープ）、カンマ区切りリスト（末尾カンマ不可）のデモ。
**EN:** JSON parser sample. Parses RFC 8259 JSON (`null` / `true` / `false` / `number` / `string` / `array` / `object`). Demonstrates `[Skip(@"\s+")]`, keyword priority, regex (numeric / string escapes), and comma-separated lists (no trailing comma).

## 実行 / Run

```
dotnet run --project samples/JsonParser
```

## 文法 / Grammar

ルートは `Json`（`[Grammar]`）。各値型が具象クラス:

- `JsonNull` — `null`（キーワード、`Priority = 1`）
- `JsonBool` — `true|false`
- `JsonNumber` — `-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?`（先頭ゼロ `01` を拒否）
- `JsonString` — `"([^"\\]|\\["\\/bfnrt]|\\u[0-9a-fA-F]{4})*"`（8 種のエスケープ + `\uXXXX` を受理・解除）
- `JsonArray` — `[ value, value, ... ]`（空 `[]` 可）
- `JsonObject` — `{ "key": value, ... }`（空 `{}` 可）

配列・オブジェクトの要素リストは右再帰の Cons/End で表現する。末尾カンマ（`[1,]` や `{"a":1,}`）は構文エラー。オブジェクトの空は括弧のみの専用規則（`{ }`）で処理する。

## 制限 / Limitations

- number は `double` で保持するため、2^53 を超える大きな整数は精度が落ちる。サンプルの範囲では妥当。
- 文字列内の生の制御文字（U+0000–U+001F）の拒否は未対応（エスケープ `\b` `\f` `\n` `\r` `\t` `\uXXXX` 等は受理・解除する）。

## 関連 / See also

`JsonMember` のように `AstNode` を直接継承した具象クラスを cons 引数で参照すると、そのクラス自身が非終端として扱われる（`src/AstFirst.Generator/ModelToGrammar.cs`）。
