$rootDir = Split-Path $PSScriptRoot
$desktop = $null

# 1. レジストリから取得（OneDrive同期デスクトップ等も正確に検出）
try {
    $regDesktop = (Get-ItemProperty "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders" -ErrorAction SilentlyContinue).Desktop
    if ($regDesktop) {
        $desktop = [Environment]::ExpandEnvironmentVariables($regDesktop)
    }
} catch {}

# 2. .NET 特殊フォルダから取得
if ([string]::IsNullOrWhiteSpace($desktop)) {
    try { $desktop = [Environment]::GetFolderPath("Desktop") } catch {}
}
if ([string]::IsNullOrWhiteSpace($desktop)) {
    try { $desktop = [Environment]::GetFolderPath("DesktopDirectory") } catch {}
}

# 3. USERPROFILE から取得
if ([string]::IsNullOrWhiteSpace($desktop) -and $env:USERPROFILE) {
    $desktop = Join-Path $env:USERPROFILE "Desktop"
}

if ([string]::IsNullOrWhiteSpace($desktop)) {
    Write-Host "[エラー] デスクトップフォルダを検出できませんでした。" -ForegroundColor Red
    return
}

$targetExe = Join-Path $rootDir "WorkspaceHub.exe"
$launchExe = Join-Path $rootDir "LaunchWorkspace.exe"
$ico = Join-Path $rootDir "workspace.ico"

if (-not (Test-Path $targetExe)) {
    Write-Host "[エラー] WorkspaceHub.exe が見つかりません。先に build.bat を実行してください。" -ForegroundColor Red
    return
}

$exeToUse = if (Test-Path $launchExe) { $launchExe } else { $targetExe }

# 1. Windows標準 .lnk ショートカット（稲妻アイコン付き）
try {
    $wsh = New-Object -ComObject WScript.Shell
    $lnkPath = Join-Path $desktop "⚡ ワークスペース一括起動.lnk"
    $sc = $wsh.CreateShortcut($lnkPath)
    $sc.TargetPath = $exeToUse
    if ($exeToUse -eq $targetExe) { $sc.Arguments = "--launch-all" }
    $sc.WorkingDirectory = $rootDir
    if (Test-Path $ico) { $sc.IconLocation = ($ico + ",0") }
    $sc.Description = "ワークスペースの全ツールを一括起動・整列配置します"
    $sc.Save()
} catch {
    Write-Host "[通知] .lnk ショートカット作成: $_" -ForegroundColor Gray
}

# 2. バックグラウンド実行用 VBScript ショートカット
try {
    $vbsPath = Join-Path $desktop "⚡ ワークスペース一括起動.vbs"
    $escapedExe = $targetExe.Replace("\", "\\")
    $vbsCode = "Set WshShell = CreateObject(`"WScript.Shell`")" + [Environment]::NewLine + "WshShell.Run `"`"`"`" + $escapedExe + `"`"`" --launch-all`", 0, False"
    [System.IO.File]::WriteAllText($vbsPath, $vbsCode, [System.Text.Encoding]::Default)
} catch {
    Write-Host "[通知] .vbs ショートカット作成: $_" -ForegroundColor Gray
}

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  [完了] デスクトップにショートカットを作成しました！" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "作成先: $desktop" -ForegroundColor Gray
Write-Host "  ・⚡ ワークスペース一括起動.lnk （稲妻アイコン付き）" -ForegroundColor Cyan
Write-Host "  ・⚡ ワークスペース一括起動.vbs （バックグラウンド実行用）" -ForegroundColor Cyan
Write-Host ""
Write-Host "ダブルクリックするだけで、全ツールを一括起動＆整列配置されます！" -ForegroundColor Yellow
Write-Host "========================================================" -ForegroundColor Cyan
