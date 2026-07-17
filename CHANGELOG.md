# Changelog

## 0.4.1 — 文法ノード探索の拡張と品質改善

- **別名前空間の文法ノード**: 既定探索で `[Grammar]` ルートの派生型をアセンブリ全体から収集。
- **探索モード**: `GrammarDiscovery.TypeHierarchy` で名前空間走査を無効化。`Namespace` で従来境界も選択可能。
- **明示参加**: `[GrammarPart(typeof(Root))]` で名前空間・型階層外の `AstNode` を文法へ追加。
- **アセンブリ共通Skip**: 宣言済みだが未収集だった `[assembly: Skip(...)]` をGeneratorへ反映。
- **安定した Core モデル**: `Grammar` が入力コレクションのスナップショットを保持し、`Production` が右辺入力を防御コピー。`GrammarBuilder.Build` を反復可能に。
- **品質ゲート**: CI と公開フローで全テストを実行。Generator の Core 型競合警告を解消。

---

## 0.4.0 — 軽量 GLR (LightGlr) モード + 読み取り専用 OnReduce

### ⚠ 破壊的変更 (後方互換性なし)

0.3.0 から以下の API が変更されました。既存コードの修正が必要です。

- **OnReduce の ctx が読み取り専用**: `OnReduce(MyCtx ctx)` → `OnReduce(SemanticContext ctx)`。宣言 (TryDeclare) や診断追加 (Error) は不可。
- **SemanticContext.Symbols が読み取り専用**: `IReadOnlySymbolTable` を返す (Lookup のみ)。
- **SemanticContext.Diagnostics 削除**: Diagnostics は `BasicSemanticContext` に移動。
- **書き込みは [Enter]/[Exit] で**: `ctx.WritableSymbols.TryDeclare(...)` / `ctx.Diagnostics.Error(...)`。
- **LALR のパニックモード削除**: スタック pop の代わりに Corchuelo et al. ER1/ER2/ER3 を使用。
- **List が COW に**: 破壊的 `List.Add` → copy-on-write (ErrorRepair の probe 安全性)。

### 新機能

- **軽量 GLR (LightGlr) モード**: `[Grammar(ParseMode = ParseMode.LightGlr)]` で本質的曖昧性 (cast/paren, generic) を並行 fork で解決。
- **ErrorRepair (Corchuelo et al.)**: ER1 挿入 / ER2 削除 / ER3 Forward move。LALR・LightGlr 両モードで共通使用。トークンを捨てない高品質エラー回復。
- **OnAccepted(ctx)**: GLR の fork が収束 (ルート確定) した時に呼ばれるコールバック。ctx 書き込み可能。
- **AmbiguousCandidates**: `ParseResult.AmbiguousCandidates` で複数解釈を観察可能。
- **NotifyAccepted**: ルート確定を AST に通知する virtual メソッド。
- **IReadOnlySymbolTable**: 読み取り専用シンボル表インターフェース。
- **GrammarSpec の [Rule] モデル対応**: Perf.Gen が正しく [Rule] static メソッドを生成。

### パフォーマンス

- LightGlrDriver に fast path (単一スタック時 LALR に近い性能) を追加。
- 単一スタック時は List/Queue/HashSet をバイパス、アロケーションほぼゼロ。
- fork が必要なコンフリクト経路のみ Clone。

### 内部変更

- `AstFirst.Glr.ErrorRepair` クラス追加 (Corchuelo ER1/ER2/BR3 共通ロジック)。
- `AstFirst.Glr.GlrTables` クラス追加 (LALR テーブル参照ラッパ)。
- `AstFirst.Glr.LightGlrDriver` クラス追加 (Tomita-lite GLR ドライバ)。
- `GlrParserEmitter` クラス追加 (LightGlr 専用エミッタ)。
- `ParserEmitter` の panic モードを Corchuelo 修復に置換。

---

## 0.3.0 — 意味解析機能の拡充

- [Enter]/[Exit]/[OnReduce] 属性・汎用 Walker・型システム拡張
- AST ノードの Span を子から自動計算 (OnReduce で上書き可)

## 0.2.x — 初期リリース

- LALR(1) パーサ生成・Lexer・AST・[Rule] static モデル
