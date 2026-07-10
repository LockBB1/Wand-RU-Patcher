@echo off
REM Обёртка над build.ps1. Аргументы пробрасываются: build.bat -Test / -Publish
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
exit /b %ERRORLEVEL%
