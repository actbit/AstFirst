# MiniBasic

**JA:** 行番号付き BASIC のサンプル（`PRINT`/`LET`/`IF-THEN-GOTO`/`GOTO`/`END`）。キーワード優先度とコンストラクタオーバーロード（`THEN GOTO N` / `THEN N`）のデモ。
**EN:** Line-numbered BASIC sample (`PRINT`/`LET`/`IF-THEN-GOTO`/`GOTO`/`END`). Demonstrates keyword priority and constructor overloading (`THEN GOTO N` / `THEN N`).

## 実行 / Run

```
dotnet run --project samples/MiniBasic
```

## 文法 / Grammar

ルートは `Line`（行リスト）。各文が `Stmt` の具象クラス:

- `10 PRINT expr`
- `20 LET x = expr`（または `20 x = expr`）
- `30 IF expr THEN GOTO 10`（または `30 IF expr THEN 10`）— コンストラクタオーバーロードで2形式
- `40 GOTO 10`
- `50 END`

## 特徴 / Features

- 行番号は `[0-9]+`、キーワード（`PRINT`/`LET`/`IF` 等）は `Priority = 1` で識別子（`[A-Z]`）に勝つ
- `IF` 文のコンストラクタオーバーロードで `THEN GOTO N` と `THEN N` の両方を受け付ける
