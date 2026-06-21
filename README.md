# AstFirst

C# の**普通のクラスと属性**で文法を書くと、Source Generator がコンパイル時に Lexer と LALR(1) Parser を生成する、自作パーサジェネレータ。

正規表現 → NFA → DFA、LR(0) → LALR(1) まで**すべて自前実装**（既成ライブラリ不使用）。

## 特徴

- **C# コードで文法定義**: クラスの継承ツリーで構文、コンストラクタ引数の `[Pattern]` で字句ルール、コンストラクタ本体で意味解析。特別な構文やファイルは不要。
- **Source Generator (`IIncrementalGenerator`)**: コンパイル時に Lexer/Parser の C# コードを生成。
- **自前レクサ**: 正規表現パーサ → Thompson 構成法 (NFA) → 部分集合構成法 (DFA) → Hopcroft 最小化、文字クラス圧縮、最長一致 + 優先度駆動。
- **自前 LALR(1)**: LR(0) オートマトン → FIRST/NULLABLE → DeRemer-Pennello (1982) ルックアヘッド伝播 → ACTION/GOTO テーブル、衝突検出。
- **優先度/結合性**: `[Precedence]` で演算子の優先度と左/右/非結合を指定（`*` > `+`、代入の右結合等）。yacc 互換の衝突解決。
- **AST 構築**: reduce 時にユーザー定義クラスのコンストラクタを呼び、実値を格納。
- **意味解析**: コンストラクタ = ノード確定時のメソッド。`[Context]` で `SemanticContext`（シンボル表 + 診断）を注入。
- **エラー回復**: panic mode で構文エラー後も解析を継続。`ParseResult` で AST + エラーリストを返す。
- **`{m,n}` 量指定子**: `\d{2,4}` 等の正規表現量指定子をサポート。

## クイックスタート

### 1. 文法を書く

```csharp
using AstFirst;

[Grammar]                              // 開始記号
[Skip(@"\s+")]                         // 空白をスキップ
public abstract class Expr : AstNode { }

public sealed class NumExpr : Expr     // 規則: Expr -> [0-9]+
{
    public int Value { get; }
    public NumExpr([Pattern(@"[0-9]+")] Token num)
    {
        Value = int.Parse(num.Text);   // コンストラクタ本体 = 意味解析
        Span = num.Span;
    }
}

[Precedence(1)]                        // 優先度1・左結合(既定)
public sealed class AddExpr : Expr     // 規則: Expr -> Expr + Expr
{
    public Expr Left { get; }
    public Expr Right { get; }
    public AddExpr(Expr left, [Pattern(@"\+")] Token op, Expr right)
    {
        Left = left;
        Right = right;
        Span = SourceSpan.Merge(left.Span, right.Span);
    }
}

[Precedence(2)]                        // 優先度2(高い)・左結合
public sealed class MulExpr : Expr     // 規則: Expr -> Expr * Expr
{
    public Expr Left { get; }
    public Expr Right { get; }
    public MulExpr(Expr left, [Pattern(@"\*")] Token op, Expr right)
    {
        Left = left;
        Right = right;
        Span = SourceSpan.Merge(left.Span, right.Span);
    }
}
```

### 2. Generator が Lexer / Parser を生成

コンパイル時に `ExprLexer` / `ExprParser` が自動生成される。

### 3. 呼ぶだけ

```csharp
var result = ExprParser.Parse("1+2*3");
// result.Ast    → MulExpr(AddExpr(NumExpr(1), +, NumExpr(2)), *, NumExpr(3))
//                (* が + より優先度が高いので先に結合)
// result.Errors → [] (エラーなし)
// result.HasErrors → false

var result2 = ExprParser.Parse("1+");
// result2.HasErrors → true (panic mode で回復)
```

## 属性リファレンス

| 属性 | 対象 | 役割 |
|---|---|---|
| `[Grammar]` | クラス | 文法の開始記号（ルート非終端）。Generator の抽出開始点。 |
| `[Pattern(@"regex")]` | コンストラクタ引数 | 字句ルール（正規表現）。`Priority` でレクサ優先度（大きいほど高優先）。 |
| `[Precedence(n)]` | クラス（演算ノード） | 演算子優先度/結合性。`n` が大きいほど高優先。`IsRightAssociative`/`IsNonAssociative` で結合性。 |
| `[Context]` | コンストラクタ引数 | `SemanticContext`（シンボル表 + 診断）を注入。意味解析で使用。 |
| `[Skip(@"regex")]` | クラス（`[Grammar]` と同じ） | スキップパターン（空白・コメント等）。 |
| `[Expect(token)]` | コンストラクタ引数 | トークン種別の絞り込み。 |

### `[Pattern]` の named プロパティ

```csharp
[Pattern(@"[A-Za-z_]\w*", Priority = 0)]    // 識別子（低優先）
[Pattern(@"if", Priority = 1)]               // キーワード if（高優先、識別子より勝つ）
[Pattern(@"=", IsRightAssociative = true)]    // 右結合（代入）
[Pattern(@"<", IsNonAssociative = true)]      // 非結合（比較）
```

### `[Precedence]` の named プロパティ

```csharp
[Precedence(1)]                              // 優先度1・左結合（既定）
[Precedence(2)]                              // 優先度2（高い）
[Precedence(1, IsRightAssociative = true)]    // 右結合（代入 =、べき乗 **）
[Precedence(1, IsNonAssociative = true)]      // 非結合（比較 < >）
```

### 文法の書き方

- **継承ツリー = 構文**: `[Grammar] public abstract class Expr` が非終端。`sealed class NumExpr : Expr` が「`Expr -> [0-9]+`」の生成規則。
- **コンストラクタ引数 = 右辺**: 引数の型と順序が生成規則の右辺を表す。`AddExpr(Expr left, [Pattern(@"\+")] Token op, Expr right)` は `Expr -> Expr + Expr`。
- **複数コンストラクタ = 複数規則**: 同じクラスに複数のコンストラクタを書くと、それぞれが独立の生成規則になる。
- **`[Context]` 引数**: 右辺の子でなく、パーサから `SemanticContext` が注入される。意味解析で `ctx.Symbols` / `ctx.Diagnostics` を使う。
- **`Token` 型**: `[Pattern]` 付きの引数は終端。`Token` 基底型または派生クラスを使う。

## サンプル

### 電卓 (本体 `src/AstFirst/Calc/`)
四則演算（`+` `*`、優先度付き）。`ExprParser.Parse("1+2")` → `AddExpr(NumExpr(1), NumExpr(2))`。

### MiniLang (本体 `src/AstFirst/MiniLang/`)
変数宣言（`let`）、`print`、四則演算のサンプル言語。`StmtParser.Parse("let x = 1+2*3;")` → `LetStmt { Name="x", Value=... }`。

### JSON パーサ (`samples/JsonParser/`)
JSON の基本型（`null`/`true`/`false`/`number`/`string`）をパース。`[Skip(@"\s+")]`、キーワード優先度、正規表現数値をデモ。

## アーキテクチャ

```
AstFirst.slnx
├── src/
│   ├── AstFirst.Core/        netstandard2.0  純粋ロジック (レクサ DFA / LALR)。Roslyn 非依存。
│   ├── AstFirst.Runtime/     net10.0         属性・基底クラス ([Pattern]/[Precedence]/[Context]/AstNode/Token/SemanticContext)
│   ├── AstFirst.Generator/   netstandard2.0  IIncrementalGenerator。Core のソースを取り込み単一アセンブリ化。
│   └── AstFirst/             net10.0         ユーザーコード (電卓・MiniLang サンプル)
├── samples/
│   └── JsonParser/           net10.0         JSON パーサのサンプル
└── tests/
    ├── AstFirst.Tests/             net10.0   Core/Runtime + EndToEnd
    └── AstFirst.Generator.Tests/   net10.0   Generator の抽出・コード生成
```

**設計のポイント**:
- Generator は Roslyn で C# コードを読み、等価比較可能な POCO モデルに変換してから（キャッシュの生命線）、Core の純粋ロジックで DFA/LALR テーブルを構築し、C# コードを生成する。
- 生成コードは Core（ランタイム）に依存。Lexer/Parser は DFA/LALR テーブルを `static readonly` 配列に埋め込み、shift/reduce を駆動。
- Generator は Core のソースを Compile Include して単一アセンブリ化（Analyzer 実行時の依存ロード問題を回避）。

## 進捗

- [x] フェーズ1: 自前レクサ（正規表現 → NFA → DFA → 最小化 → 最長一致駆動）
- [x] フェーズ2: 自前 LALR（LR(0) → FIRST → DeRemer-Pennello → ACTION/GOTO + 衝突検出）
- [x] フェーズ3: Source Generator（C# コードから抽出 → Lexer/Parser 生成）
- [x] フェーズ4: reduce 時のコンストラクタ呼び出し + AST 構築 + `[Context]` 注入
- [x] フェーズ5: エラー回復（panic mode + ParseResult / 診断リスト）
- [x] フェーズ6 (一部): 優先度/結合性 (`[Precedence]`)、`{m,n}` 量指定子、`[Skip]`、レクサ優先度
- [ ] フェーズ6 (残): テーブル圧縮、Unicode 補助面
- [ ] フェーズ7: 複数フォーマット/方言対応 (`[Grammar(Mode=...)]`)

## テスト

129 テスト（AstFirst.Tests 108 + Generator.Tests 21）。レクサ/DFA/LALR の各段階と、エンドツーエンド（C# 文法定義 → 生成 → Parse → AST）を検証。

## ライセンス

MIT (LICENSE.txt)
