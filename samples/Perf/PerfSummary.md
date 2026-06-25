# 大規模文法 パフォーマンス

## 生成パフォーマンス（クリーンビルド）

各文法プロジェクトの obj 削除後のクリーンビルド (`dotnet build -c Release --no-incremental`) で計測。
生成コード = Generator が生成した `*.g.cs` (Lexer/Parser/Listener) の合計。
（この表は `run-perf.ps1` が自動生成。gen-perf マーカー間のみ上書きし、それ以外のセクションは保持。）

<!-- BEGIN gen-perf -->
| パターン | LALR状態数 | シンボル数 | 生成コード(byte) | 生成コード(行) | ビルド時間(ms) |
|---|---:|---:|---:|---:|---:|
| DeepPrec | 44 | 45 | 58214 | 624 | 3007 |
| WideRules | 205 | 207 | 601219 | 2321 | 3447 |
| ManyTokens | 205 | 206 | 599511 | 2322 | 3660 |
| DeepNest | 7 | 8 | 11454 | 230 | 3101 |
| MegaLang | 121 | 119 | 252995 | 1406 | 3315 |
| CSharp | 798 | 608 | 6023011 | 8011 | 10969 |
<!-- END gen-perf -->

## 実行パフォーマンス（BenchmarkDotNet、大規模テスト）

`Perf.Bench/RepresentativeBenchmarks`（Ryzen 9 3900, .NET 10.0.9, Release, SimpleJob）。
C# 完全文法（365 規則 / 798 状態）を最大規模のストレステストとして計測。

- **Build** = `ModelToTable.Build`（GrammarModel → LALR テーブル構築の純粋時間。Generator がコンパイル時に払う1回コスト）。
- **Parse** = 字句解析 + 構文解析 + AST 構築（実行時。入力 = Medium サイズ固定）。

| 文法 | 規則数 | Build | Parse | Parse Allocated |
|---|---:|---:|---:|---:|
| DeepPrec | 22 | 2.75 ms | 1.94 ms | 4.7 MB |
| WideRules | 103 | 2.71 ms | 2.14 ms | 4.7 MB |
| ManyTokens | 103 | 73.0 ms | 1.14 ms | 2.5 MB |
| DeepNest | 3 | 0.011 ms | 0.104 ms | 338 KB |
| MegaLang | 58 | 4.02 ms | 0.193 ms | 401 KB |
| **CSharp** | **365** | **231 ms** | **0.33 ms** | **779 KB** |

### 所見

- **Parse_CSharp 0.33 ms**（50 クラス ≈ 7KB 入力、≈ 21 MB/s）。テーブル駆動 LALR として高速。実行時は配列インデックスアクセス（O(1)）で、テーブルが 798 状態と大きくても速度に依存しない。
- **Build_CSharp 231 ms** は `LalrLookahead` の不動点反復（798状態×365規則、O(n²)〜O(n³) 寄り）の規模限界。WideRules（103 規則）2.7 ms → CSharp（365 規則）231 ms と非線形に増大。ただし Generator 実行時のみの1回コストで、実行時ではない。
- **Parse のアロケーション 779 KB**（入力の ≈111 倍）は AST 構築（reduce ごとにノードを `new`）。仕組み上（AST は結果として返るためオブジェクト寿命 = AST 寿命）抑制が困難。詳細は README「C# 完全文法ベンチマーク」章。

### 計測環境

- CPU: AMD Ryzen 9 3900（12 物理コア）
- .NET 10.0.9、Windows 11 (10.0.26200)
- BenchmarkDotNet 0.14.0、SimpleJob(launchCount:1, warmupCount:2, iterationCount:3)