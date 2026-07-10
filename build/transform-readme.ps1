<#
.SYNOPSIS
  README の相対リンクを GitHub 絶対 URL に変換し、NuGet 用 README を生成します。
  nuget.org 上ではパッケージ内の相対リンク (docs/en/*.md 等) がリンク切れになるため、
  CI の pack 前に本スクリプトで絶対 URL 化します。
.PARAMETER Source
  変換元 README (通常 README.md)。
.PARAMETER Output
  変換後 README の出力先。
.PARAMETER BaseUrl
  絶対 URL のベース (例: https://github.com/actbit/AstFirst/blob/v0.1.0)。
#>
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$Output,
    [Parameter(Mandatory = $true)][string]$BaseUrl
)

$content = Get-Content -Raw -Path $Source
# [text](path) のうち、path が http(s)://, #, mailto: で始まらないものを <BaseUrl>/<path> に変換。
$content = [regex]::Replace($content, '\]\((?!https?://|#|mailto:)([^)]+)\)', "]($BaseUrl/`$1)")
Set-Content -Path $Output -Value $content -NoNewline
