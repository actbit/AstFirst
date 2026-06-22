# 大規模文法ベンチマーク (samples/Perf)

特性の異なる大規模な文法を複数作り、AstFirst の**生成パフォーマンス**（コンパイル時：LALR テーブル構築 + コード生成）と**実行パフォーマンス**（実行時：字句解析 + 構文解析 + AST 構築）を計測する。

実測には **BenchmarkDotNet** を使用し、規則数・トークン数・優先度階層・ネスト深さが各指標にどう効くかを見る。

## プロジェクト構成

```
samples/Perf/
├── Perf.Grammars/   共有: 各パターンの文法ファクトリ (csSource + GrammarModel を生成)
├── Perf.Gen/        コンソール: ファクトリから文法 GeneratedGrammar.cs を各プロジェクトに書き出し
├── Perf.DeepPrec/   文法: 深い優先度階層 (20段階の二項演算子)
├── Perf.WideRules/  文法: 幅広の規則数 (100種の文)
├── Perf.ManyTokens/ 文法: 多数のトークン (100キーワード + 識別子)
├── Perf.DeepNest/   文法: 深いネスト (括弧式)
├── Perf.MegaLang/   文法: 総合大規模 (上記を統合)
├── Perf.Bench/      BenchmarkDotNet: Parse / Tokenize / ModelToTable 計測
└── run-perf.ps1     生成パフォーマンス集計 (クリーンビルド時間・生成コードサイズ・テーブル次元)
```

各文法は別プロジェクト（別アセンブリ）。AstFirst は1アセンブリ内の全 AstNode 派生を1文法に集約する（`ModelExtraction.GetAllTypes`）ため、パターンごとに分離が必要。

## 5 つの文法パターン

| パターン | 内容 | 規則 | 状態 | 主に効く指標 |
|---|---|---:|---:|---|
| **DeepPrec** | 20段階の二項演算子（`[Precedence(1..20)]`） | 22 | 44 | shift-reduce 解決 → 生成コードサイズ |
| **WideRules** | 100種の文（Stmt0..Stmt99）、Stmt 抽象の代替 | 103 | 205 | 代替規則数 → LALR 状態数 |
| **ManyTokens** | 100キーワード + 識別子、Program 直接再帰 | 103 | 205 | 終端数 + 多数代替 → LALR lookahead 爆発 |
| **DeepNest** | 括蜀式 `(((...)))` | 3 | 7 | ネスト深さ → 実行時スタック |
| **MegaLang** | 宣言30種 + 式(17段階演算子+代入+ネスト) + 制御文 | 58 | 121 | 総合的な生成・実行コスト |

## 生成物の構造（Perf.Grammars）

各パターンは `Perf.Grammars` のファクトリが、1つの仕様から2つを整合的に導出する:

- **csSource** — コンパイルされる C# 文法（`GeneratedGrammar.cs`）。Generator が読んで Parser を生成。
- **GrammarModel** — Generator が抽出するのと等価な POCO。Perf.Bench がテーブル構築計測に使う。

これにより「文法プロジェクトがビルドする文法」と「ベンチが計測する GrammarModel」が常に一致する。

## 実行方法

### 1. 文法を（再）生成

```sh
dotnet run --project samples/Perf/Perf.Gen
```

`Perf.Grammars` の各ファクトリから `Perf.*/GeneratedGrammar.cs` を書き出す。規模を変えるときはファクトリの定数（`Levels`/`RuleCount`/`KeywordCount` 等）を編集して再実行。生成済みの `.cs` はリポジトリにコミット済み。

### 2. smoke test（各文法がパース可能か）

```sh
dotnet run --project samples/Perf/Perf.DeepNest
# (Perf.DeepPrec / WideRules / ManyTokens / MegaLang も同様)
```

### 3. 実行パフォーマンス（BenchmarkDotNet）

```sh
dotnet run -c Release --project samples/Perf/Perf.Bench
# 引数で絞り込み可:
#   -- --filter '*TableBuild*'   生成（テーブル構築）のみ、速い
#   -- --filter '*Parse*'        Parse のみ
#   -- -f '*' -j short           全ベンチを短縫計測
```

Release ビルドが必須。結果は `BenchmarkDotNet.Artifacts/`（.gitignore 対象）に Markdown/CSV で出力。

### 4. 生成パフォーマンス（ビルド時間・生成コードサイズ）

```sh
pwsh samples/Perf/run-perf.ps1
```

各文法プロジェクトの `obj` 削除後のクリーンビルド時間、生成 `*.g.cs` のサイズ、LALR 状態数/シンボル数を集計し `PerfSummary.md` を出力。**Perf.Bench 実行中と同時に走らせないこと**（obj 競合）。

## 計測対象

### 実行 — Parse / Tokenize（Perf.Bench）

- **Parse** = 字句解析 + 構文解析 + AST 構築（`XxxParser.Parse`）。
- **Tokenize** = 字句解析のみ（`XxxLexer.Tokenize`、Parser が内部で呼ぶものを独立計測）。
- `[Params(Small, Medium, Large)]` で入力サイズを切り替え、規模に対するスケール（線形か）を見る。
- `MemoryDiagnoser` でアロケーション・GC も計測。
- 各 `GlobalSetup` で `result.Ast != null && !result.HasErrors` を assert（壊れた入力で計測しないよう担保）。

### 生成 — ModelToTable（Perf.Bench）

- **ModelToTable.Build** = GrammarModel → LALR テーブル構築の純粋な時間。Generator プロセス外で直接計測（`Perf.Grammars` が Generator の `ModelToTable.cs` を Compile Include）。
- ビルド全体のノイズを除いた、テーブル構築アルゴリズム自体の速度。

### 生成 — ビルド全体（run-perf.ps1）

- クリーンビルド時間（Generator 実行 + C# コンパイル + リンク）と生成コードサイズ。実プロジェクトでかかる実コストの目安。

## 結果

環境: AMD Ryzen 9 3900 (12物理/24論理), .NET 10.0.9, Windows 11。BenchmarkDotNet 0.14.0 / ShortRun (IterationCount=3, WarmupCount=3)。誤差は大きめだが傾向は明確。

### 実行 — Parse / Tokenize（大入力）

入力サイズ（Large）: DeepPrec=589KB, WideRules=490KB, ManyTokens=689KB, DeepNest=10KB(深さ5000), MegaLang=49KB。

| 操作 | DeepPrec | WideRules | ManyTokens | DeepNest | MegaLang |
|---|---:|---:|---:|---:|---:|
| **Parse** | 40.3 ms | 38.9 ms | 23.6 ms | 0.99 ms | 3.4 ms |
| **Tokenize** | 14.5 ms | 14.8 ms | 10.5 ms | 0.53 ms | 1.64 ms |
| Parse Alloc | 65 MB | 62 MB | 30 MB | 2.4 MB | 5.3 MB |

### 実行 — 規模に対するスケール（Parse, DeepPrec）

入力サイズを Small(0.5KB) → Medium(59KB) → Large(589KB) と10倍ずつ増やしたとき:

| Size | 入力 | Mean | Allocated |
|---|---:|---:|---:|
| Small | 487 B | 28.5 μs | 65 KB |
| Medium | 58.9 KB | 4,184 μs | 6.6 MB |
| Large | 589 KB | 40,304 μs | 65 MB |

→ **入力サイズにほぼ線形**（10倍ごとに約10倍）。Parser は O(n)。

### 生成 — テーブル構築（ModelToTable.Build、純粋）

| パターン | Mean | Allocated | LALR状態 | シンボル |
|---|---:|---:|---:|---:|
| Build_DeepPrec | 2.94 ms | 3.6 MB | 44 | 45 |
| Build_WideRules | 2.74 ms | 4.5 MB | 205 | 207 |
| **Build_ManyTokens** | **71.7 ms** | **156 MB** | 205 | 206 |
| Build_DeepNest | 11.0 μs | 24 KB | 7 | 8 |
| Build_MegaLang | 4.31 ms | 5.2 MB | 121 | 119 |

### 生成 — ビルド時間・生成コードサイズ（run-perf.ps1）

各プロジェクト `obj` 削除後のクリーンビルド（`dotnet build -c Release --no-incremental`）。依存プロジェクトは事前にビルド済み。

| パターン | LALR状態 | シンボル | 生成コード(byte) | 生成コード(行) | ビルド時間(ms) |
|---|---:|---:|---:|---:|---:|
| DeepPrec | 44 | 45 | 58,623 | 646 | 5,759 |
| WideRules | 205 | 207 | 601,011 | 2,343 | 5,503 |
| ManyTokens | 205 | 206 | 599,293 | 2,344 | 5,544 |
| DeepNest | 7 | 8 | 12,036 | 252 | 3,319 |
| MegaLang | 121 | 119 | 253,005 | 1,428 | 3,781 |

生成コードサイズは **状態数×シンボル数** の `ActionKind/ActionValue/Goto` 配列3つに支配。WideRules/ManyTokens（状態205）は約600KB・2300行。ビルド時間は固定費（Generator ロード＋最小コンパイル）約3秒＋生成コードのコンパイルで、3-6秒に収まる（DeepPrec のやや長めは初回オーバーヘッドのブレ）。Generator のテーブル構築自体（ManyTokens 72ms 含む）はビルド全体から見れば小さい。

## 考察

1. **実行は入力に線形、字句解析が過半**。Parser は O(n)。Parse のうち Tokenize（DFA 駆動）が 30-40% を占め、残りが shift/reduce + AST 構築。Lexer は純粋な配列参照ループで速い。

2. **ManyTokens の二面性（最重要）**。実行時 Parse は**速い**（Large 23.6ms、演算子優先の DeepPrec の 40ms より速い）。しかしテーブル構築は **72ms / 156MB** と他の **24倍**。原因は「`Program → Kw0..Kw99, IdentNode` の直接再帰 + 102代替」が `LalrLookahead` の伝播不動点反復を爆発させること（WideRules は代替を `Stmt` 抽象に分散させるので 2.7ms に収まる）。**生成は1回・実行は毎回**なので、実用上の影響は限定的だが、多数代替を1非終端に直接ぶら下げる文法はコンパイル時間が伸びる指標になる。

3. **DeepPrec（深い優先度階層）は状態数を増やさない**。20段階でも LALR 状態44。優先度/結合性は shift-reduce 衝突の**解決**（状態増ではなくテーブル埋め）で処理されるため。生成コードサイズも小さい。

4. **WideRules（幅広規則）は状態数を増やす**。100代替で状態205。生成コードサイズ（`S×M` 配列3つ）とビルド時間が膨らむ主因。

5. **ネスト深さは実行時に線形**。DeepNest は深さ5000でも 1ms。スタックベースの LR で再帰しないため、深いネストでもスタックオーバーフローしない。

6. **アロケーションは入力に比例**。Parse はトークン毎に `BasicToken`+`SourceSpan`+`Position`、reduce 毎に AST ノードを生成（`Stack<object?>` へのボックス化含む）。DeepPrec Large で 65MB / 589KB入力 ≈ 110倍。GC 圧力が Parse 時間の大部分を占める可能性。
