# 文法リファレンス

[English](../en/grammar-reference.md) / 日本語

AstFirst では C# のクラスと属性で文法を書く。Generator がコンパイル時に Lexer / Parser / Listener を生成する。

## 属性一覧

| 属性 | 対象 | 役割 |
|---|---|---|
| `[Grammar]` | クラス | 文法の開始記号（ルート非終端）。 |
| `[Pattern(@"regex")]` | コンストラクタ引数 | 字句ルール（正規表現）。 |
| `[Precedence(n)]` | クラス（演算ノード） | 演算子優先度/結合性。 |
| `[Skip(@"regex")]` | クラス（`[Grammar]` と同じ） | スキップパターン。 |
| `[Expect(token)]` | コンストラクタ引数 | トークン種別の絞り込み。 |

## `[Grammar]`

文法の開始記号（ルート非終端）の抽象クラスに付ける。Generator はこれを抽出開始点にする。

```csharp
[Grammar]
[Skip(@"\s+")]
public abstract class Expr : AstNode { }
```

`Mode` 名前付きプロパティで複数方言を切り替えられる（後述）。

## `[Pattern]`

コンストラクタ引数に付け、字句ルール（正規表現）を指定する。この引数が終端記号になる。

```csharp
public NumExpr([Pattern(@"[0-9]+")] Token num) { ... }
```

名前付きプロパティ:

- `Priority` — レクサ優先度（大きいほど高優先）。同じ入力で複数トークンが受理した際と shift-reduce 衝突解決に使う。
- `IsRightAssociative` — 右結合（代入 `=`、べき乗 `**` 等）。
- `IsNonAssociative` — 非結合（比較 `<` 等、`a<b<c` はエラー）。`IsRightAssociative` より優先。

```csharp
[Pattern(@"[A-Za-z_]\w*", Priority = 0)]    // 識別子（低優先）
[Pattern(@"if", Priority = 1)]               // キーワード if（高優先、識別子に勝つ）
[Pattern(@"=", IsRightAssociative = true)]    // 右結合
```

## `[Precedence]`

演算子ノード（二項/単項演算の AST クラス）に付け、優先度/結合性を指定する。shift-reduce 衝突を解決する。`n` が大きいほど高優先。

```csharp
[Precedence(1)]                              // 優先度1・左結合（既定）
public sealed class AddExpr : Expr { ... }

[Precedence(2)]                              // 優先度2（* は + より強い）
public sealed class MulExpr : Expr { ... }
```

名前付きプロパティ:

- `IsRightAssociative` — 右結合（`=`、`**`）。
- `IsNonAssociative` — 非結合（`<`、`>`）。`IsRightAssociative` より優先。

## `[Skip]`

スキップパターン（空白・コメント等）。`[Grammar]` を付けたクラスに併せて付ける。マッチした部分はトークン列から除外される。

```csharp
[Skip(@"(\s|//[^\n]*)+")]   // 空白と行コメント
```

## `[Expect]`

コンストラクタ引数のトークン種別を絞り込む。

## 規則の書き方

- **継承ツリー = 構文**: `[Grammar] public abstract class Expr` が非終端。`sealed class NumExpr : Expr` が生成規則 `Expr -> [0-9]+`。
- **コンストラクタ引数 = 右辺**: 引数の型と順序が右辺。`AddExpr(Expr left, [Pattern(@"\+")] Token op, Expr right)` は `Expr -> Expr + Expr`。
- **複数コンストラクタ = 複数規則**: 同じクラスに複数のコンストラクタを書くと、それぞれ独立の生成規則になる。
- **抽象クラス**: `abstract class` は非終端の基底。具象規則は `sealed class`（または具象クラス）。

```csharp
public sealed class DeclStmt : Stmt
{
    public DeclStmt([Pattern(@"int", Priority = 1)] Token kw, [Pattern(@"[A-Za-z_]\w*")] Token name, [Pattern(@";")] Token semi) { ... }
    public DeclStmt([Pattern(@"int", Priority = 1)] Token kw, [Pattern(@"[A-Za-z_]\w*")] Token name, [Pattern(@"=")] Token eq, Expr init, [Pattern(@";")] Token semi) { ... }
}
```

## Token と SemanticContext

コンストラクタ引数の特別な型:

- **`Token` 型**（`[Pattern]` 付き）: 終端記号。`Token` 基底型または派生クラス。字面（`Text`）とソース範囲（`Span`）を持つ。
- **`SemanticContext` 派生型**: 右辺の子でなく、パーサから意味解析コンテキストが注入される（属性ではなく**型**で判定）。`ctx.Symbols` / `ctx.Diagnostics` で意味解析を行う。

## 複数方言（Mode）

`[Grammar(Mode = "...")]` で、同じルートから複数の Parser/Listener を生成できる（フォーマット/方言の切り替え）。生成されるクラス名は `<Root>_<Mode>Parser` 等。

## 正規表現

`[Pattern]` の正規表現は Thompson 構成法 (NFA) → 部分集合構成法 (DFA) → Hopcroft 最小化で処理される。文字クラス、`{m,n}` 量指定子、Unicode 補助面に対応。

## 関連

- [アーキテクチャ](architecture.md)
- [意味解析ガイド](semantic-analysis.md)
