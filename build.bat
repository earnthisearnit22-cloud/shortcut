@echo off
setlocal
set SCRIPT_DIR=%~dp0

set CSC=%windir%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set WPF_DIR=%windir%\Microsoft.NET\Framework64\v4.0.30319\WPF

if exist %CSC% goto FoundCSC
set CSC=%windir%\Microsoft.NET\Framework\v4.0.30319\csc.exe
set WPF_DIR=%windir%\Microsoft.NET\Framework\v4.0.30319\WPF

:FoundCSC
if exist %CSC% goto DoBuild
echo [ERROR] .NET Framework 4.x csc.exe not found.
pause
exit /b 1

:DoBuild
echo [Workspace Hub] Building applications...

%CSC% /nologo /codepage:65001 /utf8output /target:winexe /optimize+ /win32icon:%SCRIPT_DIR%workspace.ico /r:%WPF_DIR%\PresentationFramework.dll /r:%WPF_DIR%\PresentationCore.dll /r:%WPF_DIR%\WindowsBase.dll /r:System.Xaml.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.dll /out:%SCRIPT_DIR%WorkspaceHub.exe %SCRIPT_DIR%WorkspaceHub.cs

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] WorkspaceHub.exe build failed.
    pause
    exit /b %ERRORLEVEL%
)

%CSC% /nologo /codepage:65001 /utf8output /target:winexe /optimize+ /win32icon:%SCRIPT_DIR%workspace.ico /out:%SCRIPT_DIR%LaunchWorkspace.exe %SCRIPT_DIR%LaunchWorkspace.cs

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] LaunchWorkspace.exe build failed.
    pause
    exit /b %ERRORLEVEL%
)

copy /y %SCRIPT_DIR%LaunchWorkspace.exe %SCRIPT_DIR%LaunchAll.exe >nul

echo ========================================================
echo   [SUCCESS] Build completed!
echo   - WorkspaceHub.exe   (Main App)
echo   - LaunchWorkspace.exe (Silent Launcher)
echo   - LaunchAll.exe       (Compatibility Alias)
echo ========================================================
if %1 neq --nopause pause