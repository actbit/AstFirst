# アーキテクチャ

[English](../en/architecture.md) / 日本語

AstFirst は 3 層 + Generator で構成されるパーサジェネレータ。

## 層構造

- **AstFirst.Core** (`netstandard2.0`): 純粋ロジック。レクサの DFA 構築・最小化、LALR(1) テーブル構築。Roslyn 非依存。
- **AstFirst.Runtime** (`netstandard2.0`): ユーザーが触れる属性・基底クラス・意味解析ヘルパー。`[Pattern]` / `[Precedence]` / `[Enter]` / `[Exit]` / `[OnReduce]` / `AstNode` / `Token` / `SourceSpan` / `ScopedSymbolTable` / `TypeSymbol` / `FunctionTypeSymbol` / `ArrayTypeSymbol` / `ISymbol` / `SemanticContext` 等。
- **AstFirst.Generator** (`netstandard2.0`): `IIncrementalGenerator`。Core のソースを取り込み単一アセンブリ化。コンパイル時に Lexer / Parser / Walker / 各ノードの partial を生成。
- **AstFirst** (`net10.0`): ユーザーコード（電卓・MiniLang サンプル）。

## Generator の処理フロー

1. **抽出** (`ModelExtraction`): `[Grammar]` の `Discovery` に従い、同一名前空間・ルート型階層・`[GrammarPart]` から文法ノードを収集する。`[Rule]`/`[Token]` と意味規則を等価比較可能な POCO モデルへ変換する。
2. **DFA 構築** (`ModelToDfa`): 字句ルールの正規表現 → NFA (Thompson) → DFA (部分集合構成法) → 最小化 (Hopcroft)。
3. **LALR テーブル** (`ModelToTable`): LR(0) オートマトン → FIRST/NULLABLE → DeRemer-Pennello ルックアヘッド伝播 → ACTION/GOTO テーブル + 衝突検出。
4. **コード生成** (`CodeEmitter` / `ParserEmitter` / `WalkerEmitter`): Lexer（DFA 配列）、Parser（LALR テーブル + shift/reduce 駆動）、Walker（Enter/Exit/Walk + `[Enter]`/`[Exit]` dispatch）、各ノードの partial（`[OnReduce]` dispatch 含む）の C# コードを生成。

## 生成コードの構造

- **Lexer**: DFA の遷移表と受理ルールを `static readonly` 配列に埋め込み、`Tokenize()` で最長一致 + 優先度駆動。各トークンの行・列（1 ベース）も計算。
- **Parser**: ACTION/GOTO テーブル + Productions を配列に埋め込み、shift/reduce/accept を駆動。reduce 時に AST クラスのコンストラクタを呼び AST を構築（`[OnReduce]` 属性メソッドは partial `OnReduce` の直後に呼出）。Corchuelo ER1/ER2/ER3 エラー修復付き。
- **Walker**: 各具象ノードの `EnterXxx` / `ExitXxx`（virtual、空実装）+ `Walk`（反復スタックで Enter → 子 → Exit）。`IOnSecondPassEnter`/`Exit` と `[Enter]`/`[Exit]` 属性メソッドも呼出。子は各ノードの public プロパティから AstNode 派生を収集して辿る。意味解析フックが1つもない文法では生成を省略（ゼロコスト）。

## キャッシュ戦略

`IIncrementalGenerator` は入力が変わらない限り生成結果をキャッシュする。そのため `GrammarModel` とその構成要素（`NodeModel` / `RuleModel` / `ParamModel` / `TokenDefModel` / `ChildModel` / `AnalyzeRuleModel`）は `IEquatable` を実装（フィールド単位の等価比較）。Roslyn シンボルを文字列/整数の POCO に落とすことで、キャッシュの効率を保つ。

Generator は Core のソースを Compile Include して単一アセンブリ化する。Analyzer 実行時の依存アセンブリロード問題を回避するため。

## 関連

- [意味解析ガイド](semantic-analysis.md)
- [文法リファレンス](grammar-reference.md)
