# AstFirst

C# の**普通のクラスと属性**で文法を書くと、Source Generator がコンパイル時に Lexer と LALR(1) Parser を生成する、自作パーサジェネレータ。

正規表現 → NFA → DFA、LR(0) → LALR(1) まで**すべて自前実装**（既成ライブラリ不使用）。

## 特徴

- **C# コードで文法定義**: クラスの継承ツリーで構文、コンストラクタ引数の `[Pattern]` で字句ルール、コンストラクタ本体で意味解析。特別な構文やファイルは不要。
- **Source Generator (`IIncrementalGenerator`)**: コンパイル時に Lexer/Parser の C# コードを生成。
- **自前レクサ**: 正規表現パーサ → Thompson 構成法 (NFA) → 部分集合構成法 (DFA) → Hopcroft 最小化、文字クラス圧縮、最長一致 + 優先度駆動。
- **自前 LALR(1)**: LR(0) オートマトン → FIRST/NULLABLE → DeRemer-Pennello (1982) ルックアヘッド伝播 → ACTION/GOTO テーブル、衝突検出。
- **優先度/結合性**: `[Precedence]` 属性で演算子の優先度と左/右/非結合を指定（`*` > `+`、代入の右結合等）。yacc 互換の衝突解決。
- **AST 構築**: reduce 時にユーザー定義クラスのコンストラクタを呼び、実値を格納。
- **意味解析**: コンストラクタ = ノード確定時のメソッド。`[Context]` で `SemanticContext`（シンボル表 + 診断）を注入。
- **エラー回復**: panic mode で構文エラー後も解析を継続。`ParseResult` で AST + エラーリストを返す。

## 使い方

ユーザーは C# のクラスと属性で文法を書くだけ:

```csharp
using AstFirst;

[Grammar]
public abstract class Expr : AstNode { }

public sealed class NumExpr : Expr
{
    public int Value { get; }
    public NumExpr([Pattern(@"[0-9]+")] Token num)
    {
        Value = int.Parse(num.Text);
        Span = num.Span;
    }
}

[Precedence(1)]  // + は優先度1・左結合(既定)
public sealed class AddExpr : Expr
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

[Precedence(2)]  // * は優先度2(高い)・左結合
public sealed class MulExpr : Expr
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

Generator が `ExprLexer` / `ExprParser` を生成するので、呼ぶだけ:

```csharp
var result = ExprParser.Parse("1+2*3");
// result.Ast    → MulExpr(AddExpr(NumExpr(1), +, NumExpr(2)), *, NumExpr(3))  (* が優先)
// result.Errors → [] (エラーなし)
```

### 属性

| 属性 | 対象 | 役割 |
|---|---|---|
| `[Grammar]` | クラス | 文法の開始記号 (ルート非終端)。Generator の抽出開始点。 |
| `[Pattern(@"regex")]` | コンストラクタ引数 | 字句ルール (正規表現)。`Priority` でレクサ優先度、`IsRightAssociative`/`IsNonAssociative` で結合性。 |
| `[Precedence(n)]` | クラス (演算ノード) | 演算子優先度/結合性。`n` が大きいほど高優先。shift-reduce 衝突を解決。 |
| `[Context]` | コンストラクタ引数 | `SemanticContext`（シンボル表 + 診断）を注入。意味解析で使用。 |
| `[Skip(@"regex")]` | クラス/アセンブリ | スキップパターン (空白・コメント等)。 |

## アーキテクチャ

```
AstFirst.slnx
├── src/
│   ├── AstFirst.Core/        netstandard2.0  純粋ロジック (レクサ DFA / LALR)。Roslyn 非依存。
│   ├── AstFirst.Runtime/     net10.0         属性・基底クラス ([Pattern]/[Precedence]/[Context]/AstNode/Token/SemanticContext)
│   ├── AstFirst.Generator/   netstandard2.0  IIncrementalGenerator。Core のソースを取り込み単一アセンブリ化。
│   └── AstFirst/             net10.0         ユーザーコード (文法定義)
└── tests/
    ├── AstFirst.Tests/             net10.0   Core/Runtime + EndToEnd
    └── AstFirst.Generator.Tests/   net10.0   Generator の抽出・コード生成
```

**設計のポイント**: Generator は Roslyn で C# コードを読み、等価比較可能な POCO モデルに変換してから（キャッシュの生命線）、Core の純粋ロジックで DFA/LALR テーブルを構築し、C# コードを生成する。生成コードは Core（ランタイム）に依存。

## 進捗

- [x] フェーズ1: 自前レクサ（正規表現 → NFA → DFA → 最小化 → 最長一致駆動）
- [x] フェーズ2: 自前 LALR（LR(0) → FIRST → DeRemer-Pennello → ACTION/GOTO + 衝突検出）
- [x] フェーズ3: Source Generator（C# コードから抽出 → Lexer/Parser 生成）
- [x] フェーズ4: reduce 時のコンストラクタ呼び出し + AST 構築
- [x] フェーズ4 補完: `[Context]` で意味解析コンテキストを注入
- [x] フェーズ5: エラー回復（panic mode + ParseResult / 診断リスト）
- [x] フェーズ6 (一部): 優先度/結合性 (`[Precedence]`)、`{m,n}` 量指定子
- [ ] フェーズ6 (残): テーブル圧縮、Unicode 補助面
- [ ] フェーズ7: 複数フォーマット/方言対応 (`[Grammar(Mode=...)]`)

## テスト

126 テスト（AstFirst.Tests 105 + Generator.Tests 21）。レクサ/DFA/LALR の各段階と、エンドツーエンド（C# 文法定義 → 生成 → Parse → AST）を検証。

## ライセンス

MIT (LICENSE.txt)
