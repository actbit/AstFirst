# samples/Perf/run-perf.ps1
# 大規模文法の生成パフォーマンス (クリーンビルド時間・生成コードサイズ・LALRテーブル次元) を集計し、
# PerfSummary.md に出力する。
#
# 実行 (リポジトリルートから):
#   pwsh samples/Perf/run-perf.ps1
# または
#   powershell -File samples/Perf/run-perf.ps1
#
# 注意: 各文法プロジェクトの obj を削除してクリーンビルドするため、数分かかる。
#       Perf.Bench のベンチマーク実行中と同時に走らせないこと (obj 競合)。

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path "$PSScriptRoot/../..").Path
Set-Location $repoRoot

$patterns = @('DeepPrec','WideRules','ManyTokens','DeepNest','MegaLang','CSharp')

Write-Host "=== AstFirst 大規模文法: 生成パフォーマンス計測 ==="
Write-Host "(各プロジェクトの obj を削除しクリーンビルド。生成 .g.cs のサイズとテーブル次元を集計。)"
Write-Host ""

# 依存プロジェクト (Core/Runtime/Generator) を先にビルドしてキャッシュウォーム。
Write-Host "依存プロジェクトをビルド中..."
dotnet build "src/AstFirst.Generator/AstFirst.Generator.csproj" -c Release 2>&1 | Out-Null
Write-Host ""

$results = @()
foreach ($p in $patterns) {
    $projDir = "samples/Perf/Perf.$p"
    $proj = "$projDir/Perf.$p.csproj"
    Write-Host "--- $p ---"

    # obj を削除してクリーンビルド (生成 .g.cs も再生成)。
    Remove-Item -Recurse -Force "$projDir/obj" -ErrorAction SilentlyContinue

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    & dotnet build $proj -c Release --no-incremental 2>&1 | Out-Null
    $sw.Stop()
    $buildMs = [math]::Round($sw.Elapsed.TotalMilliseconds, 0)

    # 生成 .g.cs のサイズ集計
    $genDir = "$projDir/obj/Release/net10.0/generated/AstFirst.Generator/AstFirst.Generator.ParserGenerator"
    $genFiles = @(Get-ChildItem -Path $genDir -Filter '*.g.cs' -ErrorAction SilentlyContinue)
    $genBytes = ($genFiles | Measure-Object -Property Length -Sum).Sum
    if (-not $genBytes) { $genBytes = 0 }
    $genLines = 0
    foreach ($f in $genFiles) { $genLines += (Get-Content $f.FullName | Measure-Object -Line).Lines }

    # Parser の .g.cs から StateCount / SymbolCount を抽出
    $parserFile = Join-Path $genDir "*Parser.g.cs"
    $parserFile = (Get-Item $parserFile -ErrorAction SilentlyContinue).FullName
    $states = "?"; $symbols = "?"
    if ($parserFile) {
        $content = Get-Content $parserFile -Raw
        $m1 = [regex]::Match($content, 'public const int StateCount = (\d+)')
        $m2 = [regex]::Match($content, 'public const int SymbolCount = (\d+)')
        if ($m1.Success) { $states = $m1.Groups[1].Value }
        if ($m2.Success) { $symbols = $m2.Groups[1].Value }
    }

    $results += [pscustomobject]@{
        Pattern  = $p
        States   = $states
        Symbols  = $symbols
        GenBytes = $genBytes
        GenLines = $genLines
        BuildMs  = $buildMs
    }
    Write-Host ("  LALR States={0}, Symbols={1}" -f $states, $symbols)
    Write-Host ("  生成コード: {0:N0} bytes ({1} lines)" -f $genBytes, $genLines)
    Write-Host ("  クリーンビルド時間: {0:N0} ms" -f $buildMs)
    Write-Host ""
}

# PerfSummary.md 出力
$md = "# 大規模文法 生成パフォーマンス`r`n`r`n"
$md += "各文法プロジェクトの obj 削除後のクリーンビルド (`dotnet build -c Release --no-incremental`) で計測。`r`n"
$md += "生成コード = Generator が生成した `*.g.cs` (Lexer/Parser/Listener) の合計。`r`n`r`n"
$md += "| パターン | LALR状態数 | シンボル数 | 生成コード(byte) | 生成コード(行) | ビルド時間(ms) |`r`n"
$md += "|---|---:|---:|---:|---:|---:|`r`n"
foreach ($r in $results) {
    $md += "| $($r.Pattern) | $($r.States) | $($r.Symbols) | $($r.GenBytes) | $($r.GenLines) | $($r.BuildMs) |`r`n"
}
$summaryPath = "$PSScriptRoot/PerfSummary.md"
$md | Set-Content -Path $summaryPath -Encoding UTF8
Write-Host "-> $summaryPath に集計結果を書き出しました。"
