Add-Type -AssemblyName System.IO.Compression.FileSystem

$shortcutDir = (Get-Item (Split-Path $PSScriptRoot)).FullName
$parentDir = Split-Path $shortcutDir
$targetZip = Join-Path $parentDir "shortcut.zip"

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  📦 Workspace Hub 配布用 ZIP パッケージ作成" -ForegroundColor Yellow
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "対象フォルダ: $shortcutDir" -ForegroundColor Gray
Write-Host "圧縮処理中..."

if (Test-Path $targetZip) {
    Remove-Item $targetZip -Force -ErrorAction SilentlyContinue
}

[System.IO.Compression.ZipFile]::CreateFromDirectory($shortcutDir, $targetZip)

Write-Host ""
Write-Host "[成功] 配布用ZIPを作成しました！" -ForegroundColor Green
Write-Host "保存場所: $targetZip" -ForegroundColor Yellow
Write-Host "サイズ  : $((Get-Item $targetZip).Length) bytes" -ForegroundColor Gray
Write-Host ""
Write-Host "この zip ファイルをそのまま相手に送信すれば、" -ForegroundColor White
Write-Host "どんなWindows環境でも解凍するだけですぐに使えます！" -ForegroundColor White
Write-Host "========================================================" -ForegroundColor Cyan
