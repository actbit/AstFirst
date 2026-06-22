# 意味解析ガイド

[English](../en/semantic-analysis.md) / 日本語

AstFirst は構文解析（AST 構築）後に意味解析を乗せられる。標準ヘルパー: スコープ付きシンボル表、Listener、シンボル解決、型チェック、束縛、診断。

## SemanticContext の注入

コンストラクタ引数に `SemanticContext` 派生型を宣言すると、Generator がパーサから `ctx` を注入する（属性は不要、**型**で判定）。`ctx.Symbols`（`ScopedSymbolTable`）と `ctx.Diagnostics`（`DiagnosticBag`）が使える。

```csharp
public sealed class DeclStmt : Stmt
{
    public DeclStmt(... Token name, SemanticContext ctx, ...)
    {
        if (!ctx.Symbols.TryDeclare(name.Text, name.Span, null, out _))
            ctx.Diagnostics.Error($"'{name.Text}' は重複", name.Span);
    }
}
```

## スコープ付きシンボル表

`ScopedSymbolTable` はレキシカルスコープのスタック。

- `PushScope()` / `PopScope()` — スコープの開閉
- `Lookup(name)` — 現在のスコープから外側へ（未宣言は `null`）
- `TryDeclare(name, span, value, out existing)` — 同一スコープ重複は拒否、外側同名（シャドウイング）は許可
- `ResolveOrError(name, span, bag)` — 解決し、未宣言なら `bag` に Error を追加して `null`

## Listener（Generator 生成）

各 `[Grammar]` に `XxxListener` が生成される。具象ノード毎の `EnterXxx` / `ExitXxx` と `Walk`（Enter → 子再帰 → Exit）を持つ。継承して override する。

```csharp
public sealed class SemanticAnalyzer : ProgramListener
{
    public override void EnterBlockStmt(BlockStmt node) => _symbols.PushScope();
    public override void ExitBlockStmt(BlockStmt node) => _symbols.PopScope();
    public override void ExitNumExpr(NumExpr node) => _types.SetType(node, Int);
    public IReadOnlyList<Diagnostic> Analyze(Program p) { Walk(p); return _bag.Items; }
}
```

`Walk` は Enter → 子再帰 → Exit の順に回る。式の型伝播（Exit）→ 条件の型チェック（Exit）が正しく順序付く。

## 型チェック

`TypeSymbol`（型の表現、`IsAssignableFrom` で代入可能性）と `TypeContext`（ノード → 型）。言語非依存の枠組みで、具象型はユーザーが定義する。

```csharp
var Int = new TypeSymbol("int");
var Bool = new TypeSymbol("bool");
// Exit で式の型を伝播
_types.SetType(node, Int);
// 条件の型チェック
if (_types.TypeOf(cond) is { } t && !Bool.IsAssignableFrom(t))
    _bag.Error("if の条件は bool が必要です", cond.Span);
```

## 束縛解析

`AstNode.SetAnnotation` / `GetAnnotation<T>` で、解決結果（シンボル・型）をノードに紐付ける。後のフェーズ（コード生成など）で参照できる。

```csharp
node.SetAnnotation("symbol", resolved);
var sym = node.GetAnnotation<SymbolEntry>("symbol");
```

## 1パス vs 2パス

LALR の reduce は**ボトムアップ**。親ノードのコンストラクタは子の**後に**呼ばれるため、ブロックスコープの Push/Pop をコンストラクタで正確にできない。

- **1パス（コンストラクタ内）**: 宣言順の可視性チェック・二重宣言検出には使えるが、**ブロックスコープは不正確**。
- **2パス（Listener ウォーク） ★推奨**: `Parse` 後に `Walk` し、`PushScope` / `PopScope` で正確なブロックスコープを管理する。

## 診断の統合

意味解析の診断は `ParseResult.Diagnostics` から取り出せる。コンストラクタ内で `ctx.Diagnostics` に追加したものも、Listener ウォークで集めたものも、`ctx.Diagnostics.Items` が `ParseResult` に渡る。

```csharp
var result = ProgramParser.Parse(code);
foreach (var d in result.Diagnostics) Console.WriteLine(d);
```

## 実例: MiniC

`samples/MiniC/SemanticAnalyzer.cs` は `ProgramListener` を継承し、次を行う:

- `EnterBlockStmt` / `ExitBlockStmt` — スコープの Push/Pop
- `EnterDeclStmt` — `TryDeclare`（二重宣言検出）
- `EnterVarExpr` / `EnterAssignStmt` — `ResolveOrError`（未宣言検出）+ annotations に束縛
- `ExitXxx`（リテラル・算術）— 式の型伝播
- `ExitIfStmt` / `ExitWhileStmt` — 条件の型チェック（bool 必須）

```
dotnet run --project samples/MiniC
```

## 関連

- [アーキテクチャ](architecture.md)
- [文法リファレンス](grammar-reference.md)
