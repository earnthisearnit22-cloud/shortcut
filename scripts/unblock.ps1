$rootDir = Split-Path $PSScriptRoot
Write-Host ""
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  [ブロック解除] ダウンロードファイルのセキュリティ解除" -ForegroundColor Yellow
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "インターネット（zip等）で受け取ったファイルにWindowsが付与する"
Write-Host "実行制限（Zone.Identifier / Mark-of-the-Web）を一括解除します..."
Write-Host ""

try {
    Get-ChildItem -LiteralPath $rootDir -Recurse -Force -ErrorAction SilentlyContinue | ForEach-Object {
        try {
            Unblock-File -LiteralPath $_.FullName -ErrorAction SilentlyContinue
        } catch {}
    }
    Write-Host "[完了] すべてのファイルのセキュリティブロックを解除しました！" -ForegroundColor Green
    Write-Host "これで WorkspaceHub.exe や LaunchWorkspace.exe が" -ForegroundColor White
    Write-Host "SmartScreenの警告なしでスムーズに起動できるようになります。" -ForegroundColor White
} catch {
    Write-Host "[注意] 解除処理中に通知がありました: $_" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "続けて「01_ホームアプリを起動.bat」を実行してください。" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""
