@echo off
rem Mo Clean VaiLon (se hoi quyen quan tri).
start "" /min powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0CleanVaiLon.ps1"
