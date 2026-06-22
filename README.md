# AstFirst

C# の**クラスと属性**で文法を書くと、Source Generator がコンパイル時に Lexer と LALR(1) Parser を生成するパーサジェネレータ。生成された Parser は、構文解析後に意味解析（スコープ付きシンボル表）を乗せられる AST を返す。

## 特徴

- **C# コードで文法定義**: クラスの継承ツリーで構文、コンストラクタ引数の `[Pattern]` で字句ルール。特別な構文や DSL ファイルは不要。
- **Source Generator (`IIncrementalGenerator`)**: コンパイル時に Lexer / Parser の C# コードを生成。実行時コード生成なし。
- **正規表現ベースのレクサ**: 文字クラス圧縮、最長一致 + 優先度駆動、`{m,n}` 量指定子、Unicode 補助面に対応。
- **LALR(1) 構文解析**: 優先度/結合性 (`[Precedence]`) で shift-reduce 衝突を解決（`*` > `+`、代入の右結合等）。
- **AST 構築**: reduce 時にユーザー定義クラスのコンストラクタを呼び、実値を格納。コンストラクタ本体でノードの意味アクションを書ける。
- **意味解析**: スコープ付きシンボル表 (`ScopedSymbolTable`) と診断 (`Diagnostic`) を提供。未宣言参照・二重宣言・スコープ外参照などを検出し、`ParseResult.Diagnostics` で取り出せる。
- **エラー回復**: panic mode で構文エラー後も解析を継続し、`ParseResult` で AST + エラーリストを返す。

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
        Value = int.Parse(num.Text);
        Span = num.Span;               // AST ノードにソース範囲を設定
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
// result.Ast      → MulExpr(AddExpr(NumExpr(1), +, NumExpr(2)), *, NumExpr(3))
//                  (* が + より優先度が高いので先に結合)
// result.Errors   → [] (構文エラーなし)
// result.HasErrors → false

var result2 = ExprParser.Parse("1+");
// result2.HasErrors → true (panic mode で回復)
```

## 意味解析

AstFirst は構文解析（AST 構築）に加え、意味解析のための**スコープ付きシンボル表** (`ScopedSymbolTable`) と**診断** (`Diagnostic`) を提供する。

### スコープ付きシンボル表

`ScopedSymbolTable` はレキシカルスコープのスタック。宣言位置 (`SourceSpan`) を記録し、内側スコープ優先で名前を解決する。

- `PushScope()` / `PopScope()` — スコープの開閉
- `Lookup(name)` — 現在のスコープから外側へ探して最初に見つかった宣言を返す（未宣言は `null`）
- `TryDeclare(name, span, value, out existing)` — 宣言。同一スコープの重複は拒否、外側スコープの同名（シャドウイング）は許可

### 1パス vs 2パス（重要）

LALR の reduce は**ボトムアップ**。親ノード（例: ブロック）のコンストラクタは子ノードの**後に**呼ばれるため、「ブロックに入る前にスコープを開く」をコンストラクタで実現できない。

- **1パス（コンストラクタ内）**: `SemanticContext` 派生型の引数で `ctx` を受け取り、`ctx.Symbols` / `ctx.Diagnostics` を使う。宣言順の可視性チェックや二重宣言検出には使えるが、**ブロックスコープの Push/Pop は正確でない**。
- **2パス（AST ウォーク） ★推奨**: `Parse` 後に AST をウォークし、`PushScope` / `PopScope` で正確なブロックスコープを管理する。

### 診断の取得

意味解析の診断（コンストラクタ内で `ctx.Diagnostics` に追加したもの、または2パスのウォークで集めたもの）は `ParseResult.Diagnostics` から取り出せる。

```csharp
var result = ProgramParser.Parse(code);
// result.Errors      → 構文エラー (ParseError)
// result.Diagnostics → 意味解析の診断 (Diagnostic)
// result.HasErrors   → 構文エラーまたは意味解析の Error が1つでもあれば true
```

### 独自コンテキストの注入

```csharp
var ctx = new MySemanticContext();         // SemanticContext 派生
var result = ProgramParser.Parse(code, ctx);
```

`Parse(string)` は `Parse(string, SemanticContext?)` に転送し、省略時は `BasicSemanticContext` を使う。独自のシンボル表や診断の集め方を差し替えられる。

### 例: MiniC の意味解析

`samples/MiniC/SemanticAnalyzer.cs` は2パスで AST をウォークし、未宣言参照・二重宣言・スコープ外参照を検出する。`dotnet run --project samples/MiniC` で実演。

```
--- 未宣言参照 ---
  意味解析の診断:
    Error: 変数 'x' は宣言されていません @ (0,0)-(0,0)

--- シャドウイング (許容) ---
  意味解析: 診断なし (OK)
```

## 属性リファレンス

| 属性 | 対象 | 役割 |
|---|---|---|
| `[Grammar]` | クラス | 文法の開始記号（ルート非終端）。Generator の抽出開始点。`Mode` で複数方言を切り替え。 |
| `[Pattern(@"regex")]` | コンストラクタ引数 | 字句ルール（正規表現）。`Priority` でレクサ優先度（大きいほど高優先）。 |
| `[Precedence(n)]` | クラス（演算ノード） | 演算子優先度/結合性。`n` が大きいほど高優先。`IsRightAssociative`/`IsNonAssociative` で結合性。 |
| `[Skip(@"regex")]` | クラス（`[Grammar]` と同じ） | スキップパターン（空白・コメント等）。 |
| `[Expect(token)]` | コンストラクタ引数 | トークン種別の絞り込み。 |

### コンストラクタ引数の特別な型

- **`Token` 型** (`[Pattern]` 付き): 終端記号。`Token` 基底型または派生クラスを使う。字面 (`Text`) とソース範囲 (`Span`) を持つ。
- **`SemanticContext` 派生型**: 右辺の子でなく、パーサから意味解析コンテキストが注入される。`ctx.Symbols` / `ctx.Diagnostics` で意味解析を行う（属性ではなく**型**で判定される）。

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
- **`SemanticContext` 派生型の引数**: 右辺の子でなく、パーサから注入される。

## サンプル

### 電卓 (`src/AstFirst/Calc/`)
四則演算（`+` `*`、優先度付き）。`ExprParser.Parse("1+2")` → `AddExpr(NumExpr(1), NumExpr(2))`。

### MiniLang (`src/AstFirst/MiniLang/`)
変数宣言（`let`）、`print`、四則演算のサンプル言語。`StmtParser.Parse("let x = 1+2*3;")` → `LetStmt { Name="x", Value=... }`。

### JSON パーサ (`samples/JsonParser/`)
JSON の基本型（`null`/`true`/`false`/`number`/`string`）をパース。`[Skip(@"\s+")]`、キーワード優先度、正規表現数値をデモ。

### MiniC (`samples/MiniC/`) — 意味解析デモ
変数宣言・代入・`print`・`if`/`while`・ブロック文・四則演算。`SemanticAnalyzer` が2パスで AST をウォークし、**スコープ付きシンボル表で未宣言参照・二重宣言・スコープ外参照を検出**する。`dotnet run --project samples/MiniC` で実演。

### MiniBASIC (`samples/MiniBasic/`)
行番号付き BASIC（`PRINT`/`LET`/`IF-THEN-GOTO`/`GOTO`/`END`）。キーワード優先度、コンストラクタオーバーロード。

## アーキテクチャ

```
AstFirst.slnx
├── src/
│   ├── AstFirst.Core/        netstandard2.0  純粋ロジック (レクサ DFA / LALR)。Roslyn 非依存。
│   ├── AstFirst.Runtime/     net10.0         属性・基底クラス・意味解析 (ScopedSymbolTable / SemanticContext / AstNode / Token)
│   ├── AstFirst.Generator/   netstandard2.0  IIncrementalGenerator。Core のソースを取り込み単一アセンブリ化。
│   └── AstFirst/             net10.0         ユーザーコード (電卓・MiniLang サンプル)
├── samples/                   net10.0         JsonParser / MiniC / MiniBasic
└── tests/                     net10.0         AstFirst.Tests (Core/Runtime + EndToEnd) / AstFirst.Generator.Tests
```

- Generator は Roslyn で C# コードを読み、等価比較可能な POCO モデルに変換してから（キャッシュの生命線）、Core の純粋ロジックで DFA/LALR テーブルを構築し、C# コードを生成する。
- 生成コードは Runtime に依存。Lexer/Parser は DFA/LALR テーブルを `static readonly` 配列に埋め込み、shift/reduce を駆動。
- Generator は Core のソースを Compile Include して単一アセンブリ化（Analyzer 実行時の依存ロード問題を回避）。

## テスト

213 テスト（AstFirst.Tests 189 + Generator.Tests 24）。レクサ/DFA/LALR の各段階、エンドツーエンド（C# 文法定義 → 生成 → Parse → AST）、エラー回復、意味解析（スコープ付きシンボル表、ctx → ParseResult.Diagnostics の統合、型チェック）、位置情報（行・列）を検証。

## ライセンス

MIT (LICENSE.txt)
