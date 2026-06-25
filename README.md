# AstFirst

日本語 / [English](README.en.md)

C# の**クラスと属性**で文法を書くと、Source Generator がコンパイル時に Lexer と LALR(1) Parser を生成するパーサジェネレータ。生成された Parser は意味解析（スコープ付きシンボル表・Listener・型チェック）を乗せられる AST を返す。

## 特徴

- **C# コードで文法定義**: クラスの継承ツリーで構文、コンストラクタ引数の `[Pattern]` で字句ルール。特別な構文や DSL ファイルは不要。
- **Source Generator (`IIncrementalGenerator`)**: コンパイル時に Lexer / Parser / **Listener** の C# コードを生成。実行時コード生成なし。
- **正規表現ベースのレクサ**: 文字クラス圧縮、最長一致 + 優先度駆動、`{m,n}` 量指定子、Unicode 補助面に対応。トークンの**行・列**も計算。
- **LALR(1) 構文解析**: 優先度/結合性 (`[Precedence]`) で shift-reduce 衝突を解決（`*` > `+`、代入の右結合等）。
- **AST 構築**: reduce 時にユーザー定義クラスのコンストラクタを呼び、実値を格納。コンストラクタ本体でノードの意味アクションを書ける。
- **意味解析**: スコープ付きシンボル表 (`ScopedSymbolTable`)、Generator 生成の **Listener**、シンボル解決 (`ResolveOrError`)、型チェック (`TypeSymbol`/`TypeContext`)、束縛解析 (`AstNode.SetAnnotation`)、診断 (`ParseResult.Diagnostics`) を提供。
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

### 2. Generator が Lexer / Parser / Listener を生成

コンパイル時に `ExprLexer` / `ExprParser` / `ExprListener` が自動生成される。

### 3. 呼ぶだけ

```csharp
var result = ExprParser.Parse("1+2*3");
// result.Ast      → AddExpr(NumExpr(1), +, MulExpr(NumExpr(2), *, NumExpr(3)))
//                  (* が + より優先度が高いので先に結合)
// result.Errors   → [] (構文エラーなし)
// result.HasErrors → false

var result2 = ExprParser.Parse("1+");
// result2.HasErrors → true (panic mode で回復)
```

## 意味解析

AstFirst は構文解析（AST 構築）に加え、意味解析のための標準ヘルパーを提供する。詳細は [docs/ja/semantic-analysis.md](docs/ja/semantic-analysis.md)。

- **スコープ付きシンボル表** (`ScopedSymbolTable`) — レキシカルスコープの管理
- **Listener** (`XxxListener`) — Generator が生成する型安全な AST ウォーカー
- **シンボル解決** (`ResolveOrError`) — 未宣言参照の検出
- **型チェック** (`TypeSymbol` / `TypeContext`) — 型の表現と検査
- **束縛解析** (`AstNode.SetAnnotation`) — ノードにシンボル/型を紐付け
- **診断** (`ParseResult.Diagnostics`) — 意味解析の診断を取り出す

### Listener（Generator 生成）

各 `[Grammar]` につき `XxxListener` 抽象クラスが自動生成される。具象ノード毎の `EnterXxx`/`ExitXxx` と、`Walk`（Enter → 子再帰 → Exit）を持つ。継承して override し、`Walk(ルート)` を呼ぶと意味解析が回る。

```csharp
// MiniC の意味解析: ProgramListener を継承
public sealed class SemanticAnalyzer : ProgramListener
{
    private readonly ScopedSymbolTable _symbols = new();
    private readonly DiagnosticBag _diagnostics = new();
    public override void EnterBlockStmt(BlockStmt node) => _symbols.PushScope();
    public override void ExitBlockStmt(BlockStmt node) => _symbols.PopScope();
    public override void EnterDeclStmt(DeclStmt node) { /* 宣言 */ }
    public override void EnterVarExpr(VarExpr node) { /* 参照解決 */ }
    public IReadOnlyList<Diagnostic> Analyze(Program p) { Walk(p); return _diagnostics.Items; }
}
```

### スコープ付きシンボル表

`ScopedSymbolTable` はレキシカルスコープのスタック。宣言位置 (`SourceSpan`) を記録し、内側スコープ優先で名前を解決する。

- `PushScope()` / `PopScope()` — スコープの開閉
- `Lookup(name)` — 現在のスコープから外側へ（未宣言は `null`）
- `TryDeclare(name, span, value, out existing)` — 宣言。同一スコープ重複は拒否、外側同名（シャドウイング）は許可
- `ResolveOrError(name, span, bag)` — 解決し、未宣言なら `bag` に Error を追加して `null`

### 型チェック

`TypeSymbol`（型の表現、継承・`IsAssignableFrom`）と `TypeContext`（ノード → 型）。言語非依存の枠組みで、具象型はユーザーが定義する。

```csharp
var Int = new TypeSymbol("int");
var Bool = new TypeSymbol("bool");
// Listener の Exit で式の型を伝播
_types.SetType(node, Int);
// 条件の型チェック
if (_types.TypeOf(cond) is { } t && !Bool.IsAssignableFrom(t))
    diag.Error("if の条件は bool が必要です", cond.Span);
```

### 束縛解析

`AstNode.SetAnnotation/GetAnnotation<T>` で、解決したシンボルや型をノードに紐付ける。

```csharp
node.SetAnnotation("symbol", resolvedSymbol);
var sym = node.GetAnnotation<SymbolEntry>("symbol");
```

### 1パス vs 2パス（重要）

LALR の reduce は**ボトムアップ**。親ノード（例: ブロック）のコンストラクタは子ノードの**後に**呼ばれるため、「ブロックに入る前にスコープを開く」をコンストラクタで実現できない。

- **1パス（コンストラクタ内）**: `SemanticContext` 派生型の引数で `ctx` を受け取り、`ctx.Symbols` / `ctx.Diagnostics` を使う。宣言順の可視性チェックや二重宣言検出には使えるが、**ブロックスコープの Push/Pop は正確でない**。
- **2パス（AST ウォーク） ★推奨**: `Parse` 後に Listener で AST をウォークし、`PushScope` / `PopScope` で正確なブロックスコープを管理する。

### 診断の取得

意味解析の診断（コンストラクタ内で `ctx.Diagnostics` に追加したもの、または Listener のウォークで集めたもの）は `ParseResult.Diagnostics` から取り出せる。

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

### 位置情報（行・列）

`SourceSpan` は行・列（1 ベース）を正確に持つ。Lexer がトークン化と同時に行・列を計算し、`Token.Span` → AST ノードの `Span` に伝播する。診断メッセージにも正確な位置が出る。

### 例: MiniC の意味解析

`samples/MiniC/SemanticAnalyzer.cs` は `ProgramListener` を継承し、スコープ管理・シンボル解決・型チェック（int/bool）を行う。`dotnet run --project samples/MiniC` で実演。

```
--- 未宣言参照 ---
  意味解析の診断:
    Error: 'x' は宣言されていません @ (1,7)-(1,8)

--- 型エラー: if の条件が int ---
  意味解析の診断:
    Error: if の条件は bool が必要です (実際: int) @ (1,5)-(1,6)

--- シャドウイング (許容) ---
  意味解析: 診断なし (OK)
```

## 属性リファレンス

詳細は [docs/ja/grammar-reference.md](docs/ja/grammar-reference.md)。

| 属性 | 対象 | 役割 |
|---|---|---|
| `[Grammar]` | クラス | 文法の開始記号（ルート非終端）。Generator の抽出開始点。`Mode` で複数方言を切り替え。 |
| `[Pattern(@"regex")]` | コンストラクタ引数 | 字句ルール（正規表現）。`Priority` でレクサ優先度（大きいほど高優先）。 |
| `[Precedence(n)]` | クラス（演算ノード） | 演算子優先度/結合性。`n` が大きいほど高優先。`IsRightAssociative`/`IsNonAssociative` で結合性。 |
| `[Skip(@"regex")]` | クラス（`[Grammar]` と同じ） | スキップパターン（空白・コメント等）。 |

### コンストラクタ引数の特別な型

- **`Token` 型** (`[Pattern]` 付き): 終端記号。字面 (`Text`) とソース範囲 (`Span`) を持つ。
- **`SemanticContext` 派生型**: 右辺の子でなく、パーサから意味解析コンテキストが注入される（属性ではなく**型**で判定される）。

### `[Pattern]` / `[Precedence]` の named プロパティ

```csharp
[Pattern(@"[A-Za-z_]\w*", Priority = 0)]    // 識別子（低優先）
[Pattern(@"if", Priority = 1)]               // キーワード if（高優先）

[Precedence(1)]                              // 優先度1・左結合（例: + -）
[Precedence(2)]                              // 優先度2（+ より強い、例: * /）
[Precedence(3, IsRightAssociative = true)]    // 優先度3・右結合（* より強い、例: べき乗 **）
[Precedence(1, IsNonAssociative = true)]      // 優先度1・非結合（例: 比較 < >、a<b<c はエラー）
```

### 文法の書き方

- **継承ツリー = 構文**: `[Grammar] public abstract class Expr` が非終端。`sealed class NumExpr : Expr` が「`Expr -> [0-9]+`」の生成規則。
- **コンストラクタ引数 = 右辺**: 引数の型と順序が右辺。`AddExpr(Expr left, [Pattern(@"\+")] Token op, Expr right)` は `Expr -> Expr + Expr`。
- **複数コンストラクタ = 複数規則**: 同じクラスに複数のコンストラクタを書くと、それぞれが独立の生成規則になる。

## サンプル

- **電卓** (`src/AstFirst/Calc/`) — 四則演算（優先度付き）。
- **MiniLang** (`src/AstFirst/MiniLang/`) — `let`/`print`/四則演算。
- **JSON パーサ** (`samples/JsonParser/`) — JSON 基本型。
- **MiniC** (`samples/MiniC/`) — 変数・代入・`if`/`while`・ブロック・bool。**意味解析（Listener + 型チェック）デモ**。
- **MiniBASIC** (`samples/MiniBasic/`) — 行番号付き BASIC。
- **C# パーサ** (`samples/CSharpParser/`) — C# 完全文法（ECMA-334 Annex A 相当）。構文解析 + AST 構築のみ（意味解析なし）。`samples/Perf/Perf.Grammars/CSharpFactory.cs` で文法を定義。

各サンプルの README を参照。

## C# 完全文法ベンチマーク (samples/Perf)

AstFirst で C# の完全文法（365 規則）を LALR(1) で実装し、パーサ生成時間 (Build) と実行時間 (Parse) を計測する。意味解析は行わない（構文解析 + AST 構築のみ）。

- `samples/Perf/Perf.Grammars/CSharpFactory.cs` — 文法定義（型/式/パターン/文/宣言/メンバ/属性/プリプロセス）。
- `samples/Perf/Perf.Gen` — `GrammarSpec → GrammarModel → LALR テーブル` を構築し、コンフリクト/状態数を検証。`GeneratedGrammar.cs` を各文法プロジェクトへ書き出す。
- 集計結果: [samples/Perf/PerfSummary.md](samples/Perf/PerfSummary.md)

### smoke test（Perf.CSharp）

24/24 OK（宣言/継承/generic クラス/enum/struct/interface/プロパティ/全文/全式/switch/try/using 等）。

### 計測結果（Ryzen 9 3900, .NET 10, Release）

| 指標 | 値 |
|---|---|
| Parse_CSharp（50 クラス入力） | 0.33 ms |
| Build_CSharp（ModelToTable.Build 純粋時間） | 231 ms |
| クリーンビルド時間 | 11 s |
| LALR 状態数 / シンボル数 | 798 / 608 |
| 生成コードサイズ | 6.0 MB（8011 行） |

> Build_CSharp 231 ms / 生成コード 6 MB は、365 規則の C# 完全文法に対する LALR テーブル構築（LalrLookahead の不動点反復）とテーブル埋め込みコードの規模限界。WideRules（103 規則）は Build 2.7 ms なので、規模に対して非線形に増大する。

### コンフリクト解決技術

C# の曖昧性を分類し、設計で解消（99 → 5）:

| 曖昧性源 | 分類 | 解決 |
|---|---|---|
| 二項/三項/postfix/unary の shift-reduce | 見かけ上 | `[Precedence]` + 全終端伝播（規則の全終端に優先度を設定） |
| リスト（文/引数/初期化子）の shift-reduce | 見かけ上 | bison 互換の shift 優先（LalrTable: 優先度未設定 SR は shift） |
| LINQ キーワード / `get`/`set` 等の文脈キーワード | 文脈キーワード | `priority:1` + 文法位置で弁別 |
| cast `(Type)e` vs 括弧式 `(e)` | 真に意味依存 | shift 優先で式文を優先（cast は制限） |
| identifier/generic が「式か型か」 | 真に意味依存 | 残り 5 コンフリクト（RR、許容） |

残り 5 コンフリクト（state 361）は `Foo` が式（`IdentifierExpr`）か型（`NamedType`）かの衝突。LALR(1) では解決不可（C# 仕様も意味解析で解決）。reduce-reduce を許容し式を優先。

### 設計上の制限（LALR(1) の限界）

指針「generic は Member の型のみ（ローカルは `var`）」「cast/paren は意味依存で許容」に沿う:

- **generic メソッド呼び出し** `Foo<T>(x)`: 不可（式位置の `<` は比較のみとし、generic の `<` と衝突させない）。
- **default 演算子** `default(T)` / target-typed `default`: 不可（switch 文の `default:` ラベルとの衝突を避けるため）。
- **ユーザー定義型の** `Foo[]` / `Foo?` / `Foo*` / cast `(Foo)e`: 式優先のため制限。

対照的に、定義済み型 `int[]` / `int?`、generic 型フィールド `List<int> x`、cast `(int)e` は動作する。

## アーキテクチャ

詳細は [docs/ja/architecture.md](docs/ja/architecture.md)。

```
AstFirst.slnx
├── src/
│   ├── AstFirst.Core/        netstandard2.0  純粋ロジック (レクサ DFA / LALR)。Roslyn 非依存。
│   ├── AstFirst.Runtime/     net10.0         属性・基底クラス・意味解析 (ScopedSymbolTable / TypeSystem / SemanticContext / AstNode / Token)
│   ├── AstFirst.Generator/   netstandard2.0  IIncrementalGenerator。Core のソースを取り込み単一アセンブリ化。
│   └── AstFirst/             net10.0         ユーザーコード (電卓・MiniLang サンプル)
├── samples/                   net10.0         JsonParser / MiniC / MiniBasic
└── tests/                     net10.0         AstFirst.Tests (Core/Runtime + EndToEnd) / AstFirst.Generator.Tests
```

- Generator は Roslyn で C# コードを読み、等価比較可能な POCO モデルに変換してから（キャッシュの生命線）、Core の純粋ロジックで DFA/LALR テーブルを構築し、Lexer/Parser/Listener の C# コードを生成する。
- 生成コードは Runtime に依存。Lexer/Parser は DFA/LALR テーブルを `static readonly` 配列に埋め込み shift/reduce を駆動。Listener は `Enter`/`Exit`/`Walk` を持つ抽象クラス。
- Generator は Core のソースを Compile Include して単一アセンブリ化（Analyzer 実行時の依存ロード問題を回避）。

## ドキュメント

- [アーキテクチャ](docs/ja/architecture.md)
- [意味解析ガイド](docs/ja/semantic-analysis.md)
- [文法リファレンス](docs/ja/grammar-reference.md)

英語版は `docs/en/` および [README.en.md](README.en.md)。

## テスト

277 テスト（AstFirst.Tests 234 + Generator.Tests 43）。レクサ/DFA/LALR の各段階、エンドツーエンド、エラー回復、意味解析（スコープ・Listener・型チェック・ctx → ParseResult.Diagnostics の統合）、位置情報（行・列）を検証。

## ライセンス

MIT (LICENSE.txt)
