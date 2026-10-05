@echo off
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\play_electron.ps1"
if errorlevel 1 pause
