# 大規模文法 パフォーマンス

## 生成パフォーマンス（クリーンビルド）

各文法プロジェクトの obj 削除後のクリーンビルド (`dotnet build -c Release --no-incremental`) で計測。
生成コード = Generator が生成した `*.g.cs` (Lexer/Parser/Listener) の合計。
（この表は `run-perf.ps1` が自動生成。gen-perf マーカー間のみ上書きし、それ以外のセクションは保持。）

<!-- BEGIN gen-perf -->
| パターン | LALR状態数 | シンボル数 | 生成コード(byte) | 生成コード(行) | ビルド時間(ms) |
|---|---:|---:|---:|---:|---:|
| DeepPrec | 44 | 45 | 81280 | 1339 | 5888 |
| WideRules | 205 | 207 | 613200 | 3545 | 5455 |
| ManyTokens | 205 | 206 | 615657 | 3546 | 5169 |
| DeepNest | 7 | 8 | 12352 | 260 | 3346 |
| MegaLang | 121 | 119 | 276789 | 2486 | 3443 |
| CSharp | 798 | 608 | 6183596 | 15363 | 11694 |
<!-- END gen-perf -->

## 実行パフォーマンス（BenchmarkDotNet、大規模テスト）

`Perf.Bench/RepresentativeBenchmarks`（Ryzen 9 3900, .NET 10.0.9, Release, SimpleJob）。
C# 完全文法（365 規則 / 798 状態）を最大規模のストレステストとして計測。

- **Build** = `ModelToTable.Build`（GrammarModel → LALR テーブル構築の純粋時間。Generator がコンパイル時に払う1回コスト）。
- **Parse** = 字句解析 + 構文解析 + AST 構築（実行時。入力 = Medium サイズ固定）。

| 文法 | 規則数 | Build | Parse | Parse Allocated |
|---|---:|---:|---:|---:|
| DeepPrec | 22 | 2.75 ms | 2.3 ms | 4.5 MB |
| WideRules | 103 | 2.71 ms | — | — |
| ManyTokens | 103 | 73.0 ms | — | — |
| DeepNest | 3 | 0.011 ms | 0.17 ms | 376 KB |
| MegaLang | 58 | 4.02 ms | 0.40 ms | 454 KB |
| **CSharp (LightGlr)** | **365** | **190 ms** | **11.4 ms** | **1,931 KB** |

> 上記は `dotnet run -c Release -- direct` (Stopwatch + GC 直接計測、warmup 5 回後 1 回) の値。WideRules / ManyTokens は未再計測 (—)。BenchmarkDotNet による精密計測は Windows Defender 適用除外設定後に再実行すること。

### 所見

- **Parse_CSharp 11.4 ms**（50 クラス ≈ 7KB 入力、LightGlr モード）。state 308 で識別子ごとに fork するため LALR 単一スタック (旧 0.68 ms) より低速。fast path (単一スタック時 List/Queue/HashSet バイパス) で 84.8 ms → 11.4 ms に最適化済み。
- **LALR モードの文法** (DeepPrec / MegaLang / DeepNest) は単一スタック fast path で動作。旧値の 2〜3 倍 (COW リスト + NotifyAccepted のオーバーヘッド)。
- **Build_CSharp 190 ms** はテーブル構築時間 (LALR 共通)。LightGlr でも同じ LALR テーブルを使用するため不変。

### 計測環境

- CPU: AMD Ryzen 9 3900（12 物理コア）
- .NET 10.0.9、Windows 11 (10.0.26200)
- BenchmarkDotNet 0.14.0、SimpleJob(launchCount:1, warmupCount:2, iterationCount:3)

## スタック配列化（新モデル・段階8）

`[Rule] static` 新モデルで ParserEmitter の実行時スタックを `Stack<int>`/`Stack<object?>` →
配列 + top 指数へ置き換え、reduce ごとの `PeekN`（`new object?[n]`）を廃止してインデックス参照化した。
（配列は容量超過時のみ 2 倍拡張。ボックス化は残るが再確保と reduce 时アロケーションを削減。）

Windows Defender が BenchmarkDotNet のベンチ子プロセスを遮断する環境のため、
`dotnet run -c Release --project samples/Perf/Perf.Bench -- direct`（Stopwatch + GC 直接計測、warmup 5 回後 1 回）で確認:

| 文法 | アロケーション (新モデル配列化) | 備考 |
|---|---:|---|
| CSharp | **627 KB** | 旧 PeekN 版 779 KB から約 20% 削減 (PeekN 廃止の効果) |
| MegaLang | 436 KB | |
| DeepNest | 368 KB | |
| DeepPrec | 4385 KB | |

※ 時間は 1 回計測のため JIT/GC ノイズが大きく、BenchmarkDotNet（上記「実行パフォーマンス」表）ほど正確ではない。
   正確な回帰値は Windows Defender 適用除外設定後、BenchmarkDotNet で再計測すること。