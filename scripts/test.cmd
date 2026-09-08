@echo off
setlocal
cd /d "%~dp0.."
dotnet test "tests\PilotBim.Analytics.Tests\PilotBim.Analytics.Tests.csproj" -c Release --verbosity minimal
if errorlevel 1 exit /b 1
