@echo off
setlocal
cd /d "%~dp0.."
dotnet build "src\PilotBim.Analytics\PilotBim.Analytics.csproj" -c Release
if errorlevel 1 exit /b 1
if not exist "dist" mkdir dist
copy /Y "src\PilotBim.Analytics\bin\Release\PilotBim.Analytics.ext2.dll" "dist\" >nul
echo Built: dist\PilotBim.Analytics.ext2.dll
