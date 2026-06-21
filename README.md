# AstFirst

C# の**普通のクラスと属性**で文法を書くと、Source Generator がコンパイル時に Lexer と LALR(1) Parser を生成する、自作パーサジェネレータ。

正規表現 → NFA → DFA、LR(0) → LALR(1) まで**すべて自前実装**（既成ライブラリ不使用）。

## 特徴

- **C# コードで文法定義**: クラスの継承ツリーで構文、コンストラクタ引数の `[Pattern]` で字句ルール、コンストラクタ本体で意味解析。特別な構文やファイルは不要。
- **Source Generator (`IIncrementalGenerator`)**: コンパイル時に Lexer/Parser の C# コードを生成。
- **自前レクサ**: 正規表現パーサ → Thompson 構成法 (NFA) → 部分集合構成法 (DFA) → Hopcroft 最小化、文字クラス圧縮、最長一致 + 優先度駆動。
- **自前 LALR(1)**: LR(0) オートマトン → FIRST/NULLABLE → DeRemer-Pennello (1982) ルックアヘッド伝播 → ACTION/GOTO テーブル、衝突検出。
- **AST 構築**: reduce 時にユーザー定義クラスのコンストラクタを呼び、実値を格納。
- **意味解析**: コンストラクタ = ノード確定時のメソッド。`[Context]` で `SemanticContext`（シンボル表 + 診断）を注入。

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
```

Generator が `ExprLexer` / `ExprParser` を生成するので、呼ぶだけ:

```csharp
var tokens = ExprLexer.Tokenize("1+2*3");   // トークン列
var ast = ExprParser.Parse("1+2");          // → AddExpr(NumExpr(1), +, NumExpr(2))
```

字句ルールはコンストラクタ引数の `[Pattern]` で、構文はクラスの継承ツリーとコンストラクタ引数の型/順序で表す。`Token` 派生クラスを作ればトークン種別を型で識別できる。

## アーキテクチャ

```
AstFirst.slnx
├── src/
│   ├── AstFirst.Core/        netstandard2.0  純粋ロジック (レクサ DFA / LALR)。Roslyn 非依存。
│   ├── AstFirst.Runtime/     net10.0         属性・基底クラス ([Pattern]/[Context]/AstNode/Token/SemanticContext)
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
- [ ] フェーズ4 補完: `[Context]` で意味解析コンテキストを注入
- [ ] フェーズ5: エラー回復（panic mode / error トークン、期待トークン提示）
- [ ] フェーズ6: テーブル圧縮 (compact)、Unicode 補助面、`{m,n}`、優先度/結合性の完全化
- [ ] フェーズ7: 複数フォーマット/方言対応 (`[Grammar(Mode=...)]`)

## テスト

126 テスト（AstFirst.Tests 105 + Generator.Tests 21）。レクサ/DFA/LALR の各段階と、エンドツーエンド（C# 文法定義 → 生成 → Parse → AST）を検証。

## ライセンス

MIT (LICENSE.txt)
