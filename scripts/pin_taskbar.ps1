$rootDir = Split-Path $PSScriptRoot
$target = Join-Path $rootDir "LaunchWorkspace.exe"
if (-not (Test-Path $target)) {
    $target = Join-Path $rootDir "WorkspaceHub.exe"
}

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  ⚡ タスクバーへのピン留め設定手順" -ForegroundColor Yellow
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "今からエクスプローラーを開き、"
Write-Host "対象の実行ファイル「LaunchWorkspace.exe」を選択状態にします。"
Write-Host ""
Write-Host "【ピン留め方法】" -ForegroundColor Cyan
Write-Host "1. 選択されている「LaunchWorkspace.exe」を右クリックします。"
Write-Host "2. Windows 11 の場合:「その他のオプションを表示」（または Shift + 右クリック）を押します。"
Write-Host "3.「タスクバーにピン留めする」をクリックします！"
Write-Host ""
Write-Host "これでタスクバーに稲妻アイコンが常駐し、" -ForegroundColor Green
Write-Host "いつでもワンクリックで全ツールが一発で整列配置されます！" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Cyan

Start-Process explorer.exe -ArgumentList "/select,`"$target`""
