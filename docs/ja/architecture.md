# アーキテクチャ

[English](../en/architecture.md) / 日本語

> **注: 本ドキュメントは旧モデル（コンストラクタベース）の記述を含みます。** 現行の `[Rule]` static モデル（`OnReduce` / `OnSecondPass` / `Accept`/`Reject` / partial 子保持）については [README](../../README.md) を参照してください。本ドキュメントの全面更新は後続 PR で行います。

AstFirst は 3 層 + Generator で構成されるパーサジェネレータ。

## 層構造

- **AstFirst.Core** (`netstandard2.0`): 純粋ロジック。レクサの DFA 構築・最小化、LALR(1) テーブル構築。Roslyn 非依存。
- **AstFirst.Runtime** (`net10.0`): ユーザーが触れる属性・基底クラス・意味解析ヘルパー。`[Pattern]` / `[Precedence]` / `AstNode` / `Token` / `SourceSpan` / `ScopedSymbolTable` / `TypeSymbol` / `SemanticContext` 等。
- **AstFirst.Generator** (`netstandard2.0`): `IIncrementalGenerator`。Core のソースを取り込み単一アセンブリ化。コンパイル時に Lexer / Parser / Listener を生成。
- **AstFirst** (`net10.0`): ユーザーコード（電卓・MiniLang サンプル）。

## Generator の処理フロー

1. **抽出** (`ModelExtraction`): `[Grammar]` ルートから AstNode 派生・Token 派生・`[Pattern]` を走査し、等価比較可能な POCO モデル (`GrammarModel`) に変換。
2. **DFA 構築** (`ModelToDfa`): 字句ルールの正規表現 → NFA (Thompson) → DFA (部分集合構成法) → 最小化 (Hopcroft)。
3. **LALR テーブル** (`ModelToTable`): LR(0) オートマトン → FIRST/NULLABLE → DeRemer-Pennello ルックアヘッド伝播 → ACTION/GOTO テーブル + 衝突検出。
4. **コード生成** (`CodeEmitter` / `ParserEmitter` / `ListenerEmitter`): Lexer（DFA 配列）、Parser（LALR テーブル + shift/reduce 駆動）、Listener（Enter/Exit/Walk）の C# コードを生成。

## 生成コードの構造

- **Lexer**: DFA の遷移表と受理ルールを `static readonly` 配列に埋め込み、`Tokenize()` で最長一致 + 優先度駆動。各トークンの行・列（1 ベース）も計算。
- **Parser**: ACTION/GOTO テーブル + Productions を配列に埋め込み、shift/reduce/accept を駆動。reduce 時に AST クラスのコンストラクタを呼び AST を構築。panic mode のエラー回復付き。
- **Listener**: 各具象ノードの `EnterXxx` / `ExitXxx`（virtual、空実装）+ `Walk`（Enter → 子再帰 → Exit）。子は各ノードの public プロパティから AstNode 派生を収集して辿る。

## キャッシュ戦略

`IIncrementalGenerator` は入力が変わらない限り生成結果をキャッシュする。そのため `GrammarModel` とその構成要素（`NodeModel` / `CtorModel` / `ParamModel` / `TokenDefModel` / `ChildModel`）は `IEquatable` を実装（フィールド単位の等価比較）。Roslyn シンボルを文字列/整数の POCO に落とすことで、キャッシュの効率を保つ。

Generator は Core のソースを Compile Include して単一アセンブリ化する。Analyzer 実行時の依存アセンブリロード問題を回避するため。

## 関連

- [意味解析ガイド](semantic-analysis.md)
- [文法リファレンス](grammar-reference.md)
