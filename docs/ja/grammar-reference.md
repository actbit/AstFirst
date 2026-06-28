# 文法リファレンス

[English](../en/grammar-reference.md) / 日本語

AstFirst では C# のクラスと属性で文法を書く。Generator がコンパイル時に Lexer / Parser / 各ノードの partial を生成する。本ドキュメントは現行の `[Rule]` static モデルを説明する（`OnReduce` / `OnSecondPass` / `Accept`/`Reject` / partial 子保持）。全体の概要は [README](../../README.md) を参照。

## 属性一覧

| 属性 | 対象 | 役割 |
|---|---|---|
| `[Grammar]` | クラス | 文法の開始記号（ルート非終端）。Generator の抽出開始点。`Mode` で複数方言を切り替え。 |
| `[Rule]` | static メソッド | 生成規則。メソッドの**引数**が右辺。1クラスに複数置ける（後述）。 |
| `[Token(@"regex")]` / `[Pattern(@"regex")]` | `[Rule]` メソッドの `Token` 引数 | 字句ルール（正規表現）。`Priority` でレクサ優先度。 |
| `[Precedence(n)]` | クラス（演算ノード） | 演算子優先度/結合性。`n` が大きいほど高優先。 |
| `[Repeat]` / `[Repeat(Min=0)]` | `[Rule]` メソッドの `AstNode` 派生引数 | リスト（繰り返し）。`Min=1`（既定）= 1回以上、`Min=0` = 0回以上。`IReadOnlyList<T>` に展開。 |
| `[Skip(@"regex")]` | クラス（`[Grammar]` と同じ） | スキップパターン（空白・コメント等）。 |

## `[Grammar]`

文法の開始記号（ルート非終端）の抽象クラスに付ける。Generator はこれを抽出開始点にする。

```csharp
[Grammar]
[Skip(@"\s+")]
public abstract partial class Expr : AstNode { }
```

`Mode` 名前付きプロパティで複数方言を切り替えられる（後述）。

## `[Rule]`

生成規則を定義する static メソッドに付ける。1クラスに複数置ける。本体は空（意味アクションは `OnReduce` に書く）。

```csharp
public sealed partial class NumExpr : Expr
{
    [Rule]
    public static void NumToken([Token(@"[0-9]+")] Token num) { }
    partial void OnReduce() { Value = int.Parse(Num.Text); }
}
```

- **引数 = 右辺**: 引数の型と順序が右辺。`Token` + `[Token]`/`[Pattern]` は終端、`AstNode` 派生は子、`SemanticContext` 派生は ctx（パーサが注入）。
- **`partial` 宣言必須**: Generator が子・終端のプロパティ（引数名の PascalCase、例: `Num`/`Left`/`Right`）と partial コンストラクタを生成し、`OnReduce` を呼ぶ。
- **複数 `[Rule]`**: 同じクラスに複数の `[Rule]` を書くと、それぞれ独立の生成規則になる。どの規則で reduce されたかは `RuleName` プロパティ（メソッド名）で判定し、`OnReduce` で `switch` する。

```csharp
public sealed partial class BinaryExpr : Expr
{
    [Rule] public static void Add(Expr left, [Token(@"\+")] Token op, Expr right) { }
    [Rule] public static void Sub(Expr left, [Token(@"-")]  Token op, Expr right) { }
    partial void OnReduce() { /* this.RuleName で "Add"/"Sub" を判定 */ }
}
```

## `[Token]` / `[Pattern]`

`[Rule]` メソッドの `Token` 引数に付け、字句ルール（正規表現）を指定する。`[Token]` と `[Pattern]` は同じ属性の別名（`[Token]` の方が用途が明示的）。

```csharp
[Rule] public static void Num([Token(@"[0-9]+")] Token num) { }
```

名前付きプロパティ:

- `Priority` — レクサ優先度（大きいほど高優先）。同じ入力で複数トークンが受理した際の解決に使う。

```csharp
[Token(@"[A-Za-z_]\w*", Priority = 0)]    // 識別子（低優先）
[Token(@"if", Priority = 1)]               // キーワード if（高優先、識別子に勝つ）
```

## `[Precedence]`

演算子ノード（二項/単項演算の AST クラス）に付け、優先度/結合性を指定する。shift-reduce 衝突を解決する。`n` が大きいほど高優先。

```csharp
[Precedence(1)]                              // 優先度1・左結合（既定）
public sealed partial class AddExpr : Expr { ... }

[Precedence(2)]                              // 優先度2（* は + より強い）
public sealed partial class MulExpr : Expr { ... }
```

名前付きプロパティ:

- `IsRightAssociative` — 右結合（`=`、`**`）。
- `IsNonAssociative` — 非結合（`<`、`>`）。`IsRightAssociative` より優先。

## `[Repeat]`

`[Rule]` メソッドの `AstNode` 派生引数に付け、リスト（繰り返し）を表す。Generator が LALR 規則に展開し（`List_T → item | List_T item | ε`）、partial プロパティは `IReadOnlyList<T>` になる。

名前付きプロパティ:

- `Min` — 最小繰り返し回数。`1`（既定）= 1回以上（Plus）、`0` = 0回以上（Star、空リスト可）。

```csharp
public sealed partial class ProgramBody : Program
{
    [Rule] public static void Body([Repeat(Min = 0)] Stmt statements) { }
    // → Program → Stmt* (Statements は IReadOnlyList<Stmt>、空も可)
}

public sealed partial class NonEmpty : Program
{
    [Rule] public static void Body([Repeat] Stmt statements) { }   // Min=1 (既定) → Stmt+
}
```

## `[Skip]`

スキップパターン（空白・コメント等）。`[Grammar]` を付けたクラスに併せて付ける。マッチした部分はトークン列から除外される。

```csharp
[Skip(@"(\s|//[^\n]*)+")]   // 空白と行コメント
```

## 規則の書き方

- **継承ツリー = 構文**: `[Grammar] public abstract partial class Expr` が非終端。`sealed partial class NumExpr : Expr` が生成規則 `Expr -> [0-9]+`。
- **`[Rule]` メソッドの引数 = 右辺**: `AddExpr` の `[Rule] static void Add(Expr left, [Token(@"\+")] Token op, Expr right)` は `Expr -> Expr + Expr`。
- **抽象クラス**: `abstract class` は非終端の基底。具象規則は `sealed class`（または具象クラス）。

### 中間抽象クラス

抽象クラスを挟んだ継承階層（`Root → Mid → Leaf`）が使える。抽象クラスも非終端として機能し、Generator が単位規則（値をそのまま渡す）で開始記号から到達可能にする。

抽象基底に `[Rule]` で共通プロパティを宣言すると、具象サブクラスが `: base(...)` で初期化し、readonly を維持したままプロパティを継承できる。

```csharp
public abstract partial class ABinary : ANode
{
    [Rule] public static void Base(ANode left, ANode right) { }   // 共通プロパティ Left/Right を宣言
}

public sealed partial class AAdd : ABinary
{
    [Rule] public static void Add(ANode left, [Token(@"\+")] Token op, ANode right) { }
    // → internal AAdd(...) : base(ruleName, left, right) { Op = op; }
    // Left/Right は基底 ABinary の readonly プロパティ（継承・再定義しない）
}
```

## Token と SemanticContext

`[Rule]` メソッドの引数の特別な型:

- **`Token` 型**（`[Token]`/`[Pattern]` 付き）: 終端記号。`Token` 基底型または派生クラス。字面（`Text`）とソース範囲（`Span`）を持つ。
- **`AstNode` 派生型**: 右辺の子。Generator が partial プロパティ（引数名の PascalCase）を生成。
- **`SemanticContext` 派生型**: 右辺の子でなく、パーサから意味解析コンテキストが注入される（属性ではなく**型**で判定）。

## `OnReduce` / Accept/Reject / OnSecondPass

- **`OnReduce(ctx)`**: 規則が reduce されたとき（ボトムアップ）に呼ばれる partial メソッド。子プロパティが既に設定済み。`this.RuleName` で規則を判定、`Span` の設定等を行う。
- **Accept/Reject**: `IsAccepted` をオーバーライドして `false` を返すと、その reduce を拒否（Reject）しフォールバック候補を試す。詳細は [README](../../README.md) の「Accept/Reject とフォールバック」。
- **`OnSecondPass`**: 2パス目のトラバーサル（トップダウン）。`IOnSecondPassEnter`/`IOnSecondPassExit` を実装したノードに対し、`OnSecondPassEnter`（子の前）→ 子再帰 → `OnSecondPassExit`（子の後）を自動呼出。詳細は [意味解析ガイド](semantic-analysis.md)。

## 複数方言（Mode）

`[Grammar(Mode = "...")]` で、同じルートから複数の Parser/Listener を生成できる（フォーマット/方言の切り替え）。生成されるクラス名は `<Root>_<Mode>Parser` 等。

## 正規表現

`[Token]`/`[Pattern]` の正規表現は Thompson 構成法 (NFA) → 部分集合構成法 (DFA) → Hopcroft 最小化で処理される。文字クラス、`{m,n}` 量指定子、Unicode 補助面に対応。

## 関連

- [アーキテクチャ](architecture.md)
- [意味解析ガイド](semantic-analysis.md)
- [README](../../README.md)
