# AstFirst

[![NuGet](https://img.shields.io/nuget/v/AstFirst.svg)](https://www.nuget.org/packages/AstFirst)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/actbit/AstFirst/blob/master/LICENSE)

日本語 / [English](README.md)

C# の**クラスと属性**で文法を書くと、Source Generator がコンパイル時に Lexer と LALR(1) / 軽量 GLR (LightGlr) Parser を生成するパーサジェネレータ。生成された Parser は意味解析（スコープ付きシンボル表・2パス目 Walker・型チェック・`Accept`/`Reject` による意味的曖昧性解決）を乗せられる AST を返す。

## 他のライブラリとの比較

AstFirst はパーサジェネレータ・パーサコンビネータと同じ領域を目指しつつ、異なるトレードオフをとります:

| | AstFirst | ANTLR | Superpower / Pidgin | Roslyn `SyntaxGenerator` |
|---|---|---|---|---|
| 文法の定義 | C# のクラス + 属性 | 外部 `.g4` DSL | C# パーサコンビネータ | 該当なし (C#/VB 構文のみ) |
| コード生成 | **コンパイル時** (Source Generator) | ビルド時コード生成ツール | **しない** (実行時に解釈) | 該当なし |
| 実行時方式 | 静的テーブル、パーサ構築不要 | 生成コード | 解釈実行 (パース毎にアロケーション/ディスパッチ) | 該当なし |
| AOT / Native AOT | ✓ 実行時コード生成なし | △ | ✓ | 該当なし |
| アルゴリズム | LALR(1) + 軽量 GLR (LightGlr) | LL(\*) / ALL(\*) | 再帰下降コンビネータ | 該当なし |
| エラー回復 | Corchuelo et al. ER1/ER2/ER3 (組み込み) | あり | 自作が必要 | 該当なし |
| 文法の表現力 | LALR(1) + GLR — `[Precedence]` / fork で衝突解決 | LL(\*) | **チューリング完全** (任意の C# で分岐) | 該当なし |

**コンビネータ (Superpower/Pidgin) に対する強み**: パーサを*コンパイル時*に静的テーブルとして生成するため、実行時の解釈・delegate dispatch・パーサ構築が不要で、AOT/Native AOT にも綺麗に通る。Corchuelo et al. の高品質エラー回復 (ER1 挿入 / ER2 削除 / ER3 Forward move) を組み込み。文法は宣言的な C# なので、IDE のナビゲーションやリファクタリングが効く。

**トレードオフ**: LALR(1) は `[Precedence]`/結合性で shift-reduce 衝突を解決する必要がある。ただし LightGlr モード (`[Grammar(ParseMode = ParseMode.LightGlr)]`) で本質的曖昧性 (cast/paren・generic 等) を並行 fork で扱える。C# 専用のツールチェイン。

**なぜ Source Generator か**: Lexer/Parser/Walker はコンパイラが見る普通の C# で、IDE で開ける (`.g.cs` がプロジェクト内にあり、Go to Definition が効く)。実行時にパーサを*構築*することは一度もなく、文法のミス（未解決コンフリクト・到達不能規則）は初回 `Parse` ではなく**コンパイル時の警告**として出る。

## 特徴

- **C# コードで文法定義**: クラスの継承ツリーで構文、`[Rule]` static メソッドの引数で右辺・字句ルール。特別な構文や DSL ファイルは不要。
- **Source Generator (`IIncrementalGenerator`)**: コンパイル時に Lexer / Parser / partial プロパティ の C# コードを生成。実行時コード生成なし。
- **正規表現ベースのレクサ**: 文字クラス圧縮、最長一致 + 優先度駆動、`{m,n}` 量指定子、Unicode 補助面に対応。トークンの**行・列**も計算。
- **LALR(1) + 軽量 GLR 構文解析**: デフォルトは LALR(1)。`[Grammar(ParseMode = ParseMode.LightGlr)]` で軽量 GLR に切り替え、本質的曖昧性 (cast/paren・generic) を並行 fork で解決。優先度/結合性 (`[Precedence]`) で shift-reduce 衝突を解決（`*` > `+`、代入の右結合等）。
- **高品質エラー回復 (Corchuelo et al.)**: ER1 挿入 / ER2 削除 / ER3 Forward move で構文エラー後も解析を継続。トークンを捨てない。LALR・GLR 両モードで共通使用。
- **AST 構築 + 子の自動保持 + Span 自動計算**: reduce 時に Generator 生成の partial コンストラクタが子・終端をプロパティへ自動セットし、子の `Span` をマージしてノードの `Span` を設定してから `OnReduce` を呼ぶ。子の手動代入も Span 設定も不要（`OnReduce` で上書き可）。
- **2パス目の意味解析**: `Parse` 後に各ノードの `OnSecondPassEnter`/`OnSecondPassExit`（トップダウン）を自動呼出。スコープの Push/Pop 等の正確な意味解析が書ける。
- **意味解析ヘルパー**: `[Enter]`/`[Exit]`/`[OnReduce]` 属性ルール、汎用 Walker (`{Root}Walker`)、スコープ付きシンボル表 (`ScopedSymbolTable`)、シンボル解決 (`ResolveOrError`)、型システム (`TypeSymbol` / `FunctionTypeSymbol` / `ArrayTypeSymbol` / `OverloadResolver`)、束縛解析 (`AstNode.SetAnnotation`)、診断 (`ParseResult.Diagnostics`)。
- **エラー回復**: Corchuelo et al. ER1/ER2/ER3 で構文エラー後も解析を継続し、`ParseResult` で AST + エラーリストを返す。

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
        Span = Num.Span;                       // 任意: Span は子から自動計算済み、ここで上書きも可
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
- 各ノードの `Span` は reduce 時に子の `Span` から自動計算されるため、上記 `OnReduce` での `Span = ...` は省略可能（明示的に上書きしたい場合のみ）。

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
// result2.HasErrors → true (Corchuelo ER1/ER2/ER3 で回復)
```

## 意味解析

AstFirst は構文解析（AST 構築）に加え、意味解析のための標準ヘルパーと 2パスの枠組みを提供する。詳細は [docs/ja/semantic-analysis.md](docs/ja/semantic-analysis.md)。

- **属性ベースのルール `[OnReduce]` / `[Enter]` / `[Exit]` (推奨)**: 意味ルールを `[Grammar]` ルートクラスの `static` メソッドで書く。Generator が `[OnReduce]` を Parser の reduce 処理から、`[Enter]`/`[Exit]` を Walker から dispatch し、ctx のキャストも自動で挿入（ノード毎のボイラープレート不要）。
- **1パス目 `OnReduce` (ボトムアップ)**: reduce 時に呼ばれる partial メソッド。`Accept()`/`Reject()` でこの構文を受け入れるか判定（既定 Accept）。`Reject` すると別候補へフォールバック。
- **2パス目 `[Enter]`/`[Exit]` / `OnSecondPassEnter`/`Exit` (トップダウン)**: 生成された汎用 Walker (`{Root}Walker`) が `Parse` 後に `Enter → 子 → Exit` を駆動。スコープ Push/Pop 等の正確な意味解析が書ける。意味フックのない文法では走査を省略（オーバーヘッドなし・ゼロコスト）。
- **型システム**: `TypeSymbol` は継承可能で `FunctionTypeSymbol`/`ArrayTypeSymbol`（共変・反変・構造等価）を組み込み、暗黙の型変換の分類と `OverloadResolver` も提供。`BasicSemanticContext` は `TypeContext` を標準で保持。
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

`SourceSpan` は行・列（1 ベース）を正確に持つ。Lexer がトークン化と同時に行・列を計算し、`Token.Span` から reduce 時に子の `Span` を自動マージして AST ノードの `Span` に設定する。診断メッセージにも正確な位置が出る。

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
| `[GrammarPart(typeof(Root))]` | クラス | 名前空間・ルート型階層外のノードを文法へ明示的に追加。`[Grammar].Discovery` で探索方法を選択可能。 |
| `[Rule]` | static メソッド | 生成規則（1クラス1つ）。メソッドの**引数**が右辺。 |
| `[Token(@"regex")]` / `[Pattern(@"regex")]` | `[Rule]` メソッドの `Token` 引数 | 字句ルール（正規表現）。`Priority` でレクサ優先度（大きいほど高優先）。 |
| `[Precedence(n)]` | クラス（演算ノード） | 演算子優先度/結合性。`n` が大きいほど高優先。`IsRightAssociative`/`IsNonAssociative` で結合性。 |
| `[Repeat]` / `[Repeat(Min=0)]` | `[Rule]` メソッドの `AstNode` 派生引数 | リスト（繰り返し）。`Min=1`（既定）は1回以上、`Min=0` は0回以上（空リスト可）。`IReadOnlyList<T>` に展開。 |
| `[Skip(@"regex")]` | `[Grammar]` クラス／アセンブリ | スキップパターン（空白・コメント等）。アセンブリ指定は全Grammar共通。 |

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
| Parse_CSharp（50 クラス入力 ≈ 7KB、LightGlr） | 11.4 ms（≈ 0.6 MB/s）※ |
| Parse_CSharp アロケーション | 1,931 KB |
| Build_CSharp（ModelToTable.Build 純粋時間） | 190 ms |
| クリーンビルド時間 | 12 s |
| LALR 状態数 / シンボル数 | 798 / 608 |
| 生成コードサイズ | 6.2 MB（15363 行） |

> ※ CSharpParser は LightGlr モードで運用 (cast/paren・generic の本質的曖昧性を fork で解決)。state 308 で識別子ごとに fork するため、LALR 単一スタック (0.68ms) より低速。LALR モードの文法 (DeepPrec 等) は単一スタックの fast path で LALR に近い性能を維持。詳細は [samples/Perf/PerfSummary.md](samples/Perf/PerfSummary.md) 参照。

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
│   ├── AstFirst.Runtime/     netstandard2.0  属性・基底クラス・意味解析 (ScopedSymbolTable / TypeSystem / SemanticContext / AstNode / Token)。Core に依存。
│   ├── AstFirst.Generator/   netstandard2.0  IIncrementalGenerator。Core のソースを取り込み単一アセンブリ化。
│   └── AstFirst/             net10.0         ユーザーコード (電卓・MiniLang サンプル)
├── samples/                   net10.0         JsonParser / MiniC / MiniBasic / CSharpParser / Perf
└── tests/                     net10.0         AstFirst.Tests (Core/Runtime + EndToEnd) / AstFirst.Generator.Tests
```

- Generator は Roslyn で C# コードを読み、等価比較可能な POCO モデルに変換してから（キャッシュの生命線）、Core の純粋ロジックで DFA/LALR テーブルを構築し、Lexer/Parser/partial の C# コードを生成する。
- 生成コードは Runtime に依存。Lexer/Parser は DFA/LALR テーブルを `static readonly` 配列に埋め込み shift/reduce を駆動。reduce 時に partial コンストラクタが子をセットして `OnReduce` を呼び、Reject ならフォールバック候補へ。`Parse` 後、生成された Walker（`{Root}Walker`）が `Enter → 子 → Exit` をトップダウンで駆動し、`IOnSecondPassEnter`/`Exit`・`[Enter]`/`[Exit]` 属性ルール・override 可能な `EnterXxx`/`ExitXxx` を呼ぶ（意味フックがない文法は走査自体を省略・ゼロコスト）。
- Generator は Core のソースを Compile Include して単一アセンブリ化（Analyzer 実行時の依存ロード問題を回避）。

## ドキュメント

- [アーキテクチャ](docs/ja/architecture.md)
- [意味解析ガイド](docs/ja/semantic-analysis.md)
- [文法リファレンス](docs/ja/grammar-reference.md)

英語版は `docs/en/` および [README.md](README.md)。

## テスト

374 テスト（AstFirst.Tests 308 + Generator.Tests 66）。レクサ/DFA/LALR の各段階、エンドツーエンド、エラー回復 (Corchuelo)、GLR fork/dedup、意味解析、文法ノード探索、Coreビルドのスナップショット安定性、`Accept`/`Reject` フォールバック、`OnAccepted`、位置情報を検証。

## ライセンス

MIT (LICENSE.txt)
