@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File %~dp0scripts\export_zip.ps1
pause