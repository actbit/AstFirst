# CSharpParser サンプル

C# の完全文法を AstFirst (LALR(1)) で実装したパーサのサンプル。実コード断片をパースして AST を構築する (意味解析はしない)。

## 特徴

- **LALR(1) で C# 構文を再現**: 式の演算子優先度階層・文・型・宣言・メンバを ECMA-334 / 言語仕様 Annex A の BNF に沿って実装。
- **コンフリクトを文法で解決**: dangling else (1個、shift で C# セマンティクス通り) を除き、postfix (`a.b.c`, `a()()`)、三項 (`a?b:c`)、generic (`List<int>`) を優先度/結合性と文脈分離で LALR(1) の枠内で解決。
- **generic は Member の型のみ**: ジェネリクス (`A<B>`) と比較式 (`a < b`) の `<` は C# 仕様も意味解析で解決する本質的曖昧性のため、generic をフィールド/メソッド/継承の型のみに限定し、ローカル宣言は `var` で衝突を回避。
- **意味解析なし**: 構文解析 + AST 構築のみ。

## 実行

```sh
dotnet run --project samples/CSharpParser
```

## 文法のカスタマイズ

文法本体は `GeneratedGrammar.cs`。`samples/Perf/Perf.Grammars/CSharpFactory.cs` から生成 (benchmark の `Perf.CSharp` と同一)。

規則を編集する場合は `CSharpFactory` を編集し、`dotnet run --project samples/Perf/Perf.Gen` で `GeneratedGrammar.cs` を再生成 (sample と benchmark 両方に書き出される)。
