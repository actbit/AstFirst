# JsonParser

**JA:** JSON パーサのサンプル。基本型（`null`/`true`/`false`/`number`/`string`）をパースする。`[Skip(@"\s+")]`、キーワード優先度、正規表現数値のデモ。
**EN:** JSON parser sample. Parses primitive types (`null`/`true`/`false`/`number`/`string`). Demonstrates `[Skip(@"\s+")]`, keyword priority, and regex numeric patterns.

## 実行 / Run

```
dotnet run --project samples/JsonParser
```

## 文法 / Grammar

ルートは `Json`（`[Grammar]`）。各値型が具象クラス:

- `JsonNull` — `null`（キーワード、`Priority = 1`）
- `JsonBool` — `true|false`
- `JsonNumber` — `-?[0-9]+(\.[0-9]+)?([eE][+-]?[0-9]+)?`
- `JsonString` — `"([^"\\]|\\.)*"`

## 拡張ポイント / Extension points

配列 (`[ ... ]`) とオブジェクト (`{ ... }`) は今後の拡張。リスト構文（`ConsJson`/`NilJson` の連鎖）とペア構文を足せば対応できる。
