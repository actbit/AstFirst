# AstFirst

日本語 / [English](README.en.md)

C# の**クラスと属性**で文法を書くと、Source Generator がコンパイル時に Lexer と LALR(1) Parser を生成するパーサジェネレータ。生成された Parser は意味解析（スコープ付きシンボル表・2パス目ウォーク・型チェック・`Accept`/`Reject` による意味的曖昧性解決）を乗せられる AST を返す。

## 特徴

- **C# コードで文法定義**: クラスの継承ツリーで構文、`[Rule]` static メソッドの引数で右辺・字句ルール。特別な構文や DSL ファイルは不要。
- **Source Generator (`IIncrementalGenerator`)**: コンパイル時に Lexer / Parser / partial プロパティ の C# コードを生成。実行時コード生成なし。
- **正規表現ベースのレクサ**: 文字クラス圧縮、最長一致 + 優先度駆動、`{m,n}` 量指定子、Unicode 補助面に対応。トークンの**行・列**も計算。
- **LALR(1) 構文解析**: 優先度/結合性 (`[Precedence]`) で shift-reduce 衝突を解決（`*` > `+`、代入の右結合等）。
- **意味的曖昧性の解決 (Accept/Reject)**: reduce 時の `OnReduce` で `Reject()` すると、優先度順の別候補（別規則/shift）へフォールバック。cast vs 括弧式のような意味依存の曖昧性を構文解析で解決できる。
- **AST 構築 + 子の自動保持**: reduce 時に Generator 生成の partial コンストラクタが子・終端をプロパティへ自動セットし、`OnReduce` を呼ぶ。子の手動代入は不要。
- **2パス目の意味解析**: `Parse` 後に各ノードの `OnSecondPassEnter`/`OnSecondPassExit`（トップダウン）を自動呼出。スコープの Push/Pop 等の正確な意味解析が書ける。
- **意味解析ヘルパー**: スコープ付きシンボル表 (`ScopedSymbolTable`)、シンボル解決 (`ResolveOrError`)、型チェック (`TypeSymbol`/`TypeContext`)、束縛解析 (`AstNode.SetAnnotation`)、診断 (`ParseResult.Diagnostics`)。
- **エラー回復**: panic mode で構文エラー後も解析を継続し、`ParseResult` で AST + エラーリストを返す。

## クイックスタート

### 1. 文法を書く

```csharp
using AstFirst;

[Grammar]                              // 開始記号
[Skip(@"\s+")]                         // 空白をスキップ
public abstract partial class Expr : AstNode { }

public sealed partial class NumExpr : Expr     // 規則: Expr -> [0-9]+
{
    public int Value { get; private set; }
    [Rule]
    public static void NumToken([Token(@"[0-9]+")] Token num) { }   // 右辺 = 引数
    partial void OnReduce()                    // reduce 時・ボトムアップ
    {
        Value = int.Parse(Num.Text);
        Span = Num.Span;                       // AST ノードにソース範囲を設定
    }
}

[Precedence(1)]                        // 優先度1・左結合(既定)
public sealed partial class AddExpr : Expr     // 規則: Expr -> Expr + Expr
{
    [Rule]
    public static void Add(Expr left, [Token(@"\+")] Token op, Expr right) { }
    partial void OnReduce() => Span = SourceSpan.Merge(Left.Span, Right.Span);
}

[Precedence(2)]                        // 優先度2(高い)・左結合
public sealed partial class MulExpr : Expr     // 規則: Expr -> Expr * Expr
{
    [Rule]
    public static void Mul(Expr left, [Token(@"\*")] Token op, Expr right) { }
    partial void OnReduce() => Span = SourceSpan.Merge(Left.Span, Right.Span);
}
```

- `[Rule]` static メソッド（1クラス1つ・void・空本体）の**引数**が右辺。`Token` + `[Token]`/`[Pattern]` は終端、`AstNode` 派生は子、`SemanticContext` 派生は ctx（パーサが注入）。
- `partial` 宣言必須。Generator が子・終端のプロパティ（引数名の PascalCase、例: `Num`/`Left`/`Right`）と partial コンストラクタを生成し、`OnReduce` を呼ぶ。

### 2. Generator が Lexer / Parser / partial を生成

コンパイル時に `ExprLexer` / `ExprParser` と各ノードの partial プロパティ・コンストラクタが自動生成される。

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

AstFirst は構文解析（AST 構築）に加え、意味解析のための標準ヘルパーと 2パスの枠組みを提供する。詳細は [docs/ja/semantic-analysis.md](docs/ja/semantic-analysis.md)。

- **1パス目 `OnReduce` (ボトムアップ)**: reduce 時に呼ばれる。`Accept()`/`Reject()` でこの構文を受け入れるか判定（既定 Accept）。`Reject` すると別候補へフォールバック。
- **2パス目 `OnSecondPassEnter`/`Exit` (トップダウン)**: `IOnSecondPassEnter`/`IOnSecondPassExit` インターフェースを実装したノードで、`Parse` 後に AST ルートから順に自動呼出（Enter → 子再帰 → Exit）。スコープ Push/Pop 等の正確な意味解析が書ける。未実装文法では走査を省略しオーバーヘッドなし。
- **スコープ付きシンボル表** (`ScopedSymbolTable`) — レキシカルスコープの管理
- **シンボル解決** (`ResolveOrError`) — 未宣言参照の検出
- **型チェック** (`TypeSymbol` / `TypeContext`) — 型の表現と検査
- **束縛解析** (`AstNode.SetAnnotation`) — ノードにシンボル/型を紐付け
- **診断** (`ParseResult.Diagnostics`) — 意味解析の診断を取り出す

### Accept/Reject とフォールバック

reduce 時の `OnReduce` で `Reject()` を呼ぶと、その解釈を破棄して**優先度順の別候補**（別規則/shift）へフォールバックする。これにより、cast `(Type)e` vs 括弧式 `(e)` のような**意味依存の曖昧性**を構文解析で解決できる。

```csharp
public sealed partial class CastExpr : Expr
{
    [Rule] public static void Cast(Type t, [Token(@"\)")] Token rp, Expr e, SemanticContext ctx) { }
    partial void OnReduce(SemanticContext ctx)
    {
        // Type が既知の型でなければ Reject → 括弧式の規則へフォールバック
        if (!IsKnownType(T.Name)) Reject();
    }
}
```

### 2パス目（OnSecondPass）

`IOnSecondPassEnter` / `IOnSecondPassExit` インターフェースを実装したノードで、`Parse` 後にトップダウン（子の前/後）で自動呼出される。ブロックスコープの Push/Pop に適する。**1つも実装しない文法では AST 走査ごと省略**され、Parse のオーバーヘッドが発生しない。

```csharp
// MiniC: BlockStmt でスコープを開閉
public sealed partial class BlockStmt : Stmt, IOnSecondPassEnter, IOnSecondPassExit
{
    [Rule] public static void Block([Token(@"\{")] Token lb, Program body, [Token(@"\}")] Token rb, MiniCContext ctx) { }
    public void OnSecondPassEnter(SemanticContext ctx) => ((MiniCContext)ctx).Symbols.PushScope();
    public void OnSecondPassExit(SemanticContext ctx) => ((MiniCContext)ctx).Symbols.PopScope();
}
```

### スコープ付きシンボル表

`ScopedSymbolTable` はレキシカルスコープのスタック。宣言位置 (`SourceSpan`) を記録し、内側スコープ優先で名前を解決する。

- `PushScope(key, kind)` / `PopScope(key)` — スコープの開閉（キー付き）。引数なし版も後方互換で残存。
- `Lookup(name)` — 現在のスコープから外側へ（未宣言は `null`）
- `TryDeclare(name, span, value, out existing)` — 宣言。同一スコープ重複は拒否、外側同名（シャドウイング）は許可
- `ResolveOrError(name, span, bag)` — 解決し、未宣言なら `bag` に Error を追加して `null`

### 型チェック

`TypeSymbol`（型の表現、継承・`IsAssignableFrom`）と `TypeContext`（ノード → 型）。言語非依存の枠組みで、具象型はユーザーが定義する。

```csharp
var Int = new TypeSymbol("int");
var Bool = new TypeSymbol("bool");
// OnSecondPassExit で式の型を伝播
ctx.Types.SetType(node, Int);
// 条件の型チェック
if (ctx.Types.TypeOf(cond) is { } t && !Bool.IsAssignableFrom(t))
    ctx.Diagnostics.Error("if の条件は bool が必要です", cond.Span);
```

### 束縛解析

`AstNode.SetAnnotation/GetAnnotation<T>` で、解決したシンボルや型をノードに紐付ける。

```csharp
node.SetAnnotation("symbol", resolvedSymbol);
var sym = node.GetAnnotation<SymbolEntry>("symbol");
```

### 診断の取得

意味解析の診断（`OnReduce`/`OnSecondPass` で `ctx.Diagnostics` に追加したもの）は `ParseResult.Diagnostics` から取り出せる。

```csharp
var result = ProgramParser.Parse(code, new MiniCContext());
// result.Errors      → 構文エラー (ParseError)
// result.Diagnostics → 意味解析の診断 (Diagnostic)
// result.HasErrors   → 構文エラーまたは意味解析の Error が1つでもあれば true
```

### 独自コンテキストの注入

```csharp
public sealed class MiniCContext : BasicSemanticContext
{
    public TypeContext Types { get; } = new();
}
var result = ProgramParser.Parse(code, new MiniCContext());
```

`Parse(string)` は `Parse(string, SemanticContext?)` に転送し、省略時は `BasicSemanticContext` を使う。`BasicSemanticContext` から派生して独自の状態（型コンテキスト等）を追加できる。

### 位置情報（行・列）

`SourceSpan` は行・列（1 ベース）を正確に持つ。Lexer がトークン化と同時に行・列を計算し、`Token.Span` → AST ノードの `Span` に伝播する。診断メッセージにも正確な位置が出る。

### 例: MiniC の意味解析

`samples/MiniC/SemanticAnalyzer.cs`（静的ヘルパ）と各ノードの `OnSecondPass` で、スコープ管理・シンボル解決・型チェック（int/bool）を行う。`dotnet run --project samples/MiniC` で実演。

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
| `[Rule]` | static メソッド | 生成規則（1クラス1つ）。メソッドの**引数**が右辺。 |
| `[Token(@"regex")]` / `[Pattern(@"regex")]` | `[Rule]` メソッドの `Token` 引数 | 字句ルール（正規表現）。`Priority` でレクサ優先度（大きいほど高優先）。 |
| `[Precedence(n)]` | クラス（演算ノード） | 演算子優先度/結合性。`n` が大きいほど高優先。`IsRightAssociative`/`IsNonAssociative` で結合性。 |
| `[Repeat]` / `[Repeat(Min=0)]` | `[Rule]` メソッドの `AstNode` 派生引数 | リスト（繰り返し）。`Min=1`（既定）は1回以上、`Min=0` は0回以上（空リスト可）。`IReadOnlyList<T>` に展開。 |
| `[Skip(@"regex")]` | クラス（`[Grammar]` と同じ） | スキップパターン（空白・コメント等）。 |

### `[Rule]` メソッドの引数（型ベース分類）

- **`Token` 型** (`[Token]`/`[Pattern]` 付き): 終端記号。字面 (`Text`) とソース範囲 (`Span`) を持つ。
- **`AstNode` 派生型**: 右辺の子。Generator が partial プロパティ（引数名の PascalCase）を生成。
- **`SemanticContext` 派生型**: 右辺の子でなく、パーサから意味解析コンテキストが注入される（属性ではなく**型**で判定される）。

### `[Token]` / `[Precedence]` の named プロパティ

```csharp
[Token(@"[A-Za-z_]\w*", Priority = 0)]    // 識別子（低優先）
[Token(@"if", Priority = 1)]               // キーワード if（高優先）

[Precedence(1)]                              // 優先度1・左結合（例: + -）
[Precedence(2)]                              // 優先度2（+ より強い、例: * /）
[Precedence(3, IsRightAssociative = true)]    // 優先度3・右結合（* より強い、例: べき乗 **）
[Precedence(1, IsNonAssociative = true)]      // 優先度1・非結合（例: 比較 < >、a<b<c はエラー）
```

### 文法の書き方

- **継承ツリー = 構文**: `[Grammar] public abstract partial class Expr` が非終端。`sealed partial class NumExpr : Expr` が「`Expr -> [0-9]+`」の生成規則。
- **`[Rule]` メソッドの引数 = 右辺**: 引数の型と順序が右辺。`[Rule] static void Add(Expr left, [Token(@"\+")] Token op, Expr right)` は `Expr -> Expr + Expr`。
- **1クラスに複数 `[Rule]` 可**: 同じクラスに複数の `[Rule]` static メソッドを書くと、それぞれ独立の生成規則になる。どの規則で reduce されたかは `RuleName` プロパティ（メソッド名）で判定し、`OnReduce` 内で `switch` する。
- **中間抽象クラス**: 抽象クラスを挟んだ継承階層（`Root → Mid → Leaf`）が使える。抽象基底に `[Rule]` で共通プロパティを宣言すると、具象サブクラスが `: base(...)` で初期化し readonly を維持したままプロパティを継承できる。
- **リスト（`[Repeat]`）**: `[Repeat]` を付けた引数は `IReadOnlyList<T>` に展開される。`Min=1`（既定）は1回以上、`Min=0` は0回以上（空リスト可）。

```csharp
// 1クラス複数 [Rule]: RuleName で分岐
public sealed partial class BinaryExpr : Expr
{
    [Rule] public static void Add(Expr left, [Token(@"\+")] Token op, Expr right) { }
    [Rule] public static void Sub(Expr left, [Token(@"-")]  Token op, Expr right) { }
    partial void OnReduce() { /* this.RuleName で "Add"/"Sub" を判定 */ }
}

// リスト: [Repeat] で IReadOnlyList<T>
public sealed partial class ProgramBody : Program
{
    [Rule] public static void Body([Repeat(Min = 0)] Stmt statements) { }
    // → Program → Stmt* (Statements は IReadOnlyList<Stmt>)
}
```

## サンプル

- **電卓** (`src/AstFirst/Calc/`) — 四則演算（優先度付き）。
- **MiniLang** (`src/AstFirst/MiniLang/`) — `let`/`print`/四則演算。
- **JSON パーサ** (`samples/JsonParser/`) — JSON 基本型。
- **MiniC** (`samples/MiniC/`) — 変数・代入・`if`/`while`・ブロック・bool。**意味解析（2パス目 + 型チェック）デモ**。
- **MiniBASIC** (`samples/MiniBasic/`) — 行番号付き BASIC。
- **C# パーサ** (`samples/CSharpParser/`) — C# 完全文法（ECMA-334 Annex A 相当）。構文解析 + AST 構築のみ（意味解析なし）。`samples/Perf/Perf.Grammars/CSharpFactory.cs` で文法を定義。

各サンプルの README を参照。

## C# 完全文法ベンチマーク (samples/Perf)

AstFirst で C# の完全文法（365 規則）を LALR(1) で実装し、パーサ生成時間 (Build) と実行時間 (Parse) を計測する。意味解析は行わない（構文解析 + AST 構築のみ）。

- `samples/Perf/Perf.Grammars/CSharpFactory.cs` — 文法定義（型/式/パターン/文/宣言/メンバ/属性/プリプロセス）。
- `samples/Perf/Perf.Gen` — `GrammarSpec → GrammarModel → LALR テーブル` を構築し、コンフリクト/状態数を検証。`GeneratedGrammar.cs` を各文法プロジェクトへ書き出す。
- 集計結果: [samples/Perf/PerfSummary.md](samples/Perf/PerfSummary.md)

### smoke test（Perf.CSharp）

AST 構築成功（宣言/継承/generic クラス/enum/struct/interface/プロパティ/全文/全式/switch/try/using 等）。

### 計測結果（Ryzen 9 3900, .NET 10, Release）

| 指標 | 値 |
|---|---|
| Parse_CSharp（50 クラス入力 ≈ 7KB） | 0.68 ms（≈ 10 MB/s）※ |
| Parse_CSharp アロケーション | 627 KB |
| Build_CSharp（ModelToTable.Build 純粋時間） | 190 ms |
| クリーンビルド時間 | 11 s |
| LALR 状態数 / シンボル数 | 798 / 608 |
| 生成コードサイズ | 6.0 MB（8011 行） |

> ※ Parse 時間は [Rule] モデル移行後（Reject/TryFallback + Span スタック）の代表値。Build_CSharp 190 ms は Worklist アルゴリズム最適化後。詳細は [samples/Perf/PerfSummary.md](samples/Perf/PerfSummary.md) 参照。Parse のアロケーションは AST 構築（reduce ごとに `new`）が主。

### コンフリクト解決技術

C# の曖昧性を分類し、設計で解消（99 → 5）:

| 曖昧性源 | 分類 | 解決 |
|---|---|---|
| 二項/三項/postfix/unary の shift-reduce | 見かけ上 | `[Precedence]` + 全終端伝播（規則の全終端に優先度を設定） |
| リスト（文/引数/初期化子）の shift-reduce | 見かけ上 | bison 互換の shift 優先（LalrTable: 優先度未設定 SR は shift） |
| LINQ キーワード / `get`/`set` 等の文脈キーワード | 文脈キーワード | `priority:1` + 文法位置で弁別 |
| cast `(Type)e` vs 括弧式 `(e)` | 真に意味依存 | `Accept`/`Reject` で意味的に解決可能（既定は shift 優先、必要なら Reject で切替） |
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
├── samples/                   net10.0         JsonParser / MiniC / MiniBasic / CSharpParser / Perf
└── tests/                     net10.0         AstFirst.Tests (Core/Runtime + EndToEnd) / AstFirst.Generator.Tests
```

- Generator は Roslyn で C# コードを読み、等価比較可能な POCO モデルに変換してから（キャッシュの生命線）、Core の純粋ロジックで DFA/LALR テーブルを構築し、Lexer/Parser/partial の C# コードを生成する。
- 生成コードは Runtime に依存。Lexer/Parser は DFA/LALR テーブルを `static readonly` 配列に埋め込み shift/reduce を駆動。reduce 時に partial コンストラクタが子をセットして `OnReduce` を呼び、Reject ならフォールバック候補へ。`Parse` 後、`IOnSecondPassEnter`/`IOnSecondPassExit` 実装ノードがあれば `WalkSecondPass`（反復的スタック実装）でトップダウン呼出（未実装なら走査自体を省略）。
- Generator は Core のソースを Compile Include して単一アセンブリ化（Analyzer 実行時の依存ロード問題を回避）。

## ドキュメント

- [アーキテクチャ](docs/ja/architecture.md)
- [意味解析ガイド](docs/ja/semantic-analysis.md)
- [文法リファレンス](docs/ja/grammar-reference.md)

英語版は `docs/en/` および [README.en.md](README.en.md)。

## テスト

279 テスト（AstFirst.Tests 236 + Generator.Tests 43）。レクサ/DFA/LALR の各段階、エンドツーエンド、エラー回復、意味解析（スコープ・2パス目・型チェック・ctx → `ParseResult.Diagnostics` の統合）、`Accept`/`Reject` フォールバック、位置情報（行・列）を検証。

## ライセンス

MIT (LICENSE.txt)
