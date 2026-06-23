# Arith

**JA:** 括弧付き四則演算 (`+` `-` `*` `/` `()`) のサンプル。REPL で式を入力すると **AST を木構造で表示** し、評価結果を出す（空行で終了）。`[Precedence]` による演算子優先度・左結合と、括弧のネストのデモ。

## 実行 / Run

```
dotnet run --project samples/Arith
```

## 出力例 / Example

```
> 1 + 2 * 3
  AST:
  └─ +
     ├─ 1
     └─ *
        ├─ 2
        └─ 3
  = 7
> (1 + 2) * 3
  AST:
  └─ *
     ├─ (...)
     │  └─ +
     │     ├─ 1
     │     └─ 2
     └─ 3
  = 9
>
```

## 文法 / Grammar

ルートは `Expr`（`[Grammar]`）。`[Skip(@"\s+")]` で空白をスキップ。

- `NumExpr` — `[0-9]+`
- `AddExpr` / `SubExpr` — `Expr (+|-) Expr`（`[Precedence(1)]`、左結合）
- `MulExpr` / `DivExpr` — `Expr (*|/) Expr`（`[Precedence(2)]`、左結合、`+` `-` より強い）
- `ParenExpr` — `( Expr )`

## 関連 / See also

演算子の優先度/結合性の解決は `LalrTable` が `[Precedence]` で行う（`*` `/` が `+` `-` より強く結合）。左結合のため `10 - 2 - 3` は `(10-2)-3 = 5` になる。
