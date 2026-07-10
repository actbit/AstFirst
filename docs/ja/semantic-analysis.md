# 意味解析ガイド

[English](../en/semantic-analysis.md) / 日本語

AstFirst は構文解析（AST 構築）後に意味解析を乗せられる。標準ヘルパー: スコープ付きシンボル表、汎用 Walker、属性ベース意味規則、型システム、シンボル解決、型チェック、束縛、診断。

## 意味解析の3つの記述方法

AstFirst では意味解析ロジックを3つの方法で書ける（併用可）。

| 方法 | タイミング | 記述場所 | 用途 |
|---|---|---|---|
| `partial void OnReduce(ctx)` | 1パス目・reduce 時（ボトムアップ） | 各ノードクラス内 | ノード局所の構文処理（`Name = Tok.Text`・`Span` 計算） |
| `[OnReduce]` / `[Enter]` / `[Exit]` 属性 | 1パス（OnReduce）・2パス（Enter/Exit） | `[Grammar]` ルートクラス内 ★推奨 | 文法全体の意味処理（宣言登録・参照解決・型チェック）。ctx キャストが自動注入される |
| `IOnSecondPassEnter` / `IOnSecondPassExit` | 2パス目（トップダウン） | 各ノードクラス内（インターフェース実装） | 後方互換。属性方式推奨 |

属性方式が最も簡潔。ボイラープレート（ctx キャスト・転送呼び出し）を Generator が自動生成する。

```csharp
[Grammar]
[Skip(@"\s+")]
public abstract partial class MyLang : AstNode
{
    [OnReduce]  // reduce 時・ボトムアップ
    public static void Declare(Decl d, MyCtx ctx)
    {
        if (!ctx.Symbols.TryDeclare(d.Name, d.Span, null, out _))
            ctx.Diagnostics.Error($"'{d.Name}' は既に宣言されています", d.Span);
    }

    [Enter]     // 2パス目・トップダウン Enter
    public static void EnterBlock(Block b, MyCtx ctx) => ctx.Symbols.PushScope();

    [Exit]      // 2パス目・Exit
    public static void ExitBlock(Block b, MyCtx ctx) => ctx.Symbols.PopScope();
}

public sealed partial class Decl : MyLang
{
    public string Name { get; private set; } = "";
    [Rule] public static void DeclRule([Token("[A-Za-z]+")] Token name, MyCtx ctx) { }
    partial void OnReduce(MyCtx ctx) { Name = NameTok.Text; Span = NameTok.Span; }  // 構文処理は partial OnReduce
}
```

`[OnReduce]` と partial `OnReduce` は**共存**する（partial `OnReduce` → `[OnReduce]` 属性の順）。

## SemanticContext の注入

`[Rule]` の引数に `SemanticContext` 派生型を宣言すると、Generator がパーサから `ctx` を注入する（属性は不要、**型**で判定）。`ctx.Symbols`（`ScopedSymbolTable`）と `ctx.Diagnostics`（`DiagnosticBag`）が使える。`BasicSemanticContext` はさらに `Types`（`TypeContext`）を標準装備。

```csharp
public sealed class MyCtx : BasicSemanticContext { /* 独自状態を追加可 */ }
```

## スコープ付きシンボル表

`ScopedSymbolTable` はレキシカルスコープのスタック。

- `PushScope(key, kind)` / `PopScope(key)` — スコープの開閉（キーで対応付けを検証）
- `Lookup(name)` — 現在のスコープから外側へ（未宣言は `null`）
- `TryDeclare(name, span, value, out existing)` — 同一スコープ重複は拒否、外側同名（シャドウイング）は許可
- `ResolveOrError(name, span, bag)` — 解決し、未宣言なら `bag` に Error を追加して `null`

シンボルの `Value`（`object?`）に型付きシンボルを格納できる:

```csharp
var varSym = new VariableSymbol("x", span, depth, Int);
ctx.Symbols.TryDeclare("x", span, varSym, out _);
var entry = ctx.Symbols.Lookup("x");
var sym = entry?.AsVariable();  // VariableSymbol? を取得
```

## 型システム

`TypeSymbol`（型の表現）。`sealed` ではなく継承可能で、`FunctionTypeSymbol`・`ArrayTypeSymbol` を組み込みで提供する。`IsAssignableFrom` は `virtual`（派生で構造的ルールを override）。

```csharp
var Int = new TypeSymbol("int");
var Bool = new TypeSymbol("bool");
var fnType = new FunctionTypeSymbol(Int, new[] { Int });   // (int) => int
var arrType = new ArrayTypeSymbol(Int);                    // int[]

Int.IsAssignableFrom(Int);                  // true
fnType.IsAssignableFrom(new FunctionTypeSymbol(Int, new[] { Int }));  // true (構造等価)
```

- `TypeContext` — ノード→型 の対応（`SetType` / `TypeOf` / `HasType`）。`BasicSemanticContext.Types` で標準利用。
- `ClassifyConversion(to)` / `IsImplicitlyConvertible(to)` — 暗黙の型変換（派生→基底・widening）。
- `OverloadResolver.Resolve(candidates, argTypes, bag, span, name)` — 関数オーバーロード解決（完全一致→暗黙変換→不在/曖昧）。

シンボル階層として `ISymbol` / `VariableSymbol` / `FunctionSymbol` / `FunctionParam` を提供（`SymbolEntry.Value` に格納して運用）。

## 汎用 Walker（Generator 生成）

各 `[Grammar]` に `{Root}Walker`（`public abstract class`）が生成される。反復スタックで `Enter → 子 → Exit` を駆動し、各具象ノードの `EnterXxx` / `ExitXxx`（virtual・override 可）と `IOnSecondPassEnter` / `Exit`・`[Enter]` / `[Exit]` 属性メソッドを呼ぶ。

```csharp
// 独自の AST 走査（意味解析以外にもコード生成等で再利用可）
public sealed class CountWalker : ProgramWalker
{
    public int Nodes;
    protected override void EnterEach(AstNode node, SemanticContext ctx) { Nodes++; base.EnterEach(node, ctx); }
}
```

`Parser.Parse()` の末尾で既定インスタンス（`_Default`）が自動駆動する。意味解析フックが1つもない文法では Walker ごと生成を省略（ゼロコスト）。

## 束縛解析

`AstNode.SetAnnotation` / `GetAnnotation<T>` で、解決結果（シンボル・型）をノードに紐付ける。後のフェーズ（コード生成など）で参照できる。

```csharp
node.SetAnnotation("symbol", resolved);
var sym = node.GetAnnotation<SymbolEntry>("symbol");
```

## 1パス vs 2パス

LALR の reduce は**ボトムアップ**。親ノードのコンストラクタ（`OnReduce`）は子の**後に**呼ばれるため、ブロックスコープの Push/Pop を reduce 時に正確にできない。

- **1パス（`OnReduce` / `[OnReduce]`）**: 宣言順の可視性チェック・二重宣言検出には使えるが、**ブロックスコープは不正確**。
- **2パス（`[Enter]` / `[Exit]`・Walker）★推奨**: `Parse` 後に Walker が `Enter → 子 → Exit` で回り、`PushScope` / `PopScope` で正確なブロックスコープを管理する。

## 診断の統合

意味解析の診断は `ParseResult.Diagnostics` から取り出せる。`ctx.Diagnostics` に追加したものが `ParseResult` に渡る。

```csharp
var result = MyParser.Parse(code);
foreach (var d in result.Diagnostics) Console.WriteLine(d);
result.HasErrors  // 構文エラー OR 意味解析の Severity.Error
```

## 実例: MiniC

`samples/MiniC/` は `[Enter]` / `[Exit]` 属性方式のリファレンス実装。ルート `Program` クラスに意味規則を集約:

- `[Enter] EnterBlock` / `[Exit] ExitBlock` — スコープの Push/Pop
- `[Enter] EnterDecl` / `EnterDeclInit` — `TryDeclare`（二重宣言検出）
- `[Enter] EnterVar` / `EnterAssign` — `ResolveOrError`（未宣言検出）+ annotations に束縛
- `[Exit] ExitNum` / `ExitAdd` 等 — 式の型伝播
- `[Exit] ExitIf` / `ExitWhile` — 条件の型チェック（bool 必須）

```
dotnet run --project samples/MiniC
```

## 関連

- [アーキテクチャ](architecture.md)
- [文法リファレンス](grammar-reference.md)
