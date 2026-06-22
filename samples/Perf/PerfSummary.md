# 大規模文法 生成パフォーマンス

各文法プロジェクトの obj 削除後のクリーンビルド (dotnet build -c Release --no-incremental) で計測。
生成コード = Generator が生成した *.g.cs (Lexer/Parser/Listener) の合計。

| パターン | LALR状態数 | シンボル数 | 生成コード(byte) | 生成コード(行) | ビルド時間(ms) |
|---|---:|---:|---:|---:|---:|
| DeepPrec | 44 | 45 | 58623 | 646 | 5759 |
| WideRules | 205 | 207 | 601011 | 2343 | 5503 |
| ManyTokens | 205 | 206 | 599293 | 2344 | 5544 |
| DeepNest | 7 | 8 | 12036 | 252 | 3319 |
| MegaLang | 121 | 119 | 253005 | 1428 | 3781 |

