$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dll = Join-Path $root 'dist\PilotBim.Analytics.ext2.dll'
$built = Join-Path $root 'src\PilotBim.Analytics\bin\Release\PilotBim.Analytics.ext2.dll'

if (-not (Test-Path $built)) {
    Write-Error "Build first: scripts\build.cmd"
}

$pilot = Get-Process -Name 'Ascon.Pilot.PilotBIM' -ErrorAction SilentlyContinue
if ($pilot) {
    Write-Host "WARNING: Pilot-BIM is running (PID $($pilot.Id)). Close it before deploy."
}

New-Item -ItemType Directory -Force -Path (Join-Path $root 'dist') | Out-Null
Copy-Item -Force $built $dll

$devRoot = Join-Path $env:LOCALAPPDATA 'ASCON\Pilot-BIM\Development\PilotBim.Analytics'
New-Item -ItemType Directory -Force -Path $devRoot | Out-Null
Copy-Item -Force $dll (Join-Path $devRoot 'PilotBim.Analytics.ext2.dll')

$ver = [Reflection.AssemblyName]::GetAssemblyName($dll).Version
$logPath = Join-Path $env:LOCALAPPDATA 'PilotBim.Analytics\Logs\analytics.log'
Write-Host "Deployed PilotBim.Analytics v$ver"
Write-Host $devRoot
Write-Host "Restart Pilot-BIM. Check toolbar/context menu in object tree view."
Write-Host "Log: $logPath"
