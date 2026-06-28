# MiniC

**JA:** 軽量C言語サンプル。変数宣言・代入・`print`・`if`/`while`・ブロック文・`bool` リテラル。各ノードの `OnSecondPassEnter`/`OnSecondPassExit`（2パス目トラバーサル）による意味解析（スコープ管理・シンボル解決・型チェック）のデモ。
**EN:** Lightweight C sample. Variables, assignment, `print`, `if`/`while`, blocks, `bool` literals. Demo of semantic analysis (scope management, symbol resolution, type checking) via per-node `OnSecondPassEnter`/`OnSecondPassExit` (second-pass traversal).

## 実行 / Run

```
dotnet run --project samples/MiniC
```

## 文法 / Grammar

- `int x;` / `int x = expr;` — 変数宣言 (declaration)
- `x = expr;` — 代入 (assignment)
- `print(expr);` — 出力 (print)
- `if (expr) stmt` / `while (expr) stmt` — 制御文 (control flow)
- `{ stmt... }` — ブロック (block)
- 式: `NumExpr` (`[0-9]+`)、`BoolExpr` (`true`/`false`)、`VarExpr`、算術 (`+ - * /`)、単項 `-`、`( )`

## 意味解析 / Semantic analysis

各ノードの `OnSecondPassEnter`/`OnSecondPassExit`（Generator が Parse 後に `WalkSecondPass` で呼ぶ）から `SemanticAnalyzer` の各メソッドが呼ばれ、次を検出する:

- 未宣言参照・二重宣言・スコープ外参照（`ScopedSymbolTable` + `ResolveOrError`）
- `if`/`while` の条件は `bool` 必須、`int` 変数への `bool` 代入を検出（`TypeSymbol`/`TypeContext`）
- 解決したシンボルを `AstNode.SetAnnotation` で束縛

詳細は [docs/ja/semantic-analysis.md](../../docs/ja/semantic-analysis.md) / [docs/en/semantic-analysis.md](../../docs/en/semantic-analysis.md)。
