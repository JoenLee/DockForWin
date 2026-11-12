param([switch]$Settings)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
& (Join-Path $PSScriptRoot 'Build.ps1') -Configuration Release
$env:DOCKFORWINDOWS_DATA = Join-Path $projectRoot 'data'
$dllPath = Join-Path $projectRoot 'src\DockForWindows\bin\Release\net10.0-windows\DockForWindows.dll'
$runArguments = @('"' + $dllPath + '"', '--data-directory', '"' + (Join-Path $projectRoot 'data') + '"')
if ($Settings) { $runArguments += '--settings' }
Start-Process -FilePath (Join-Path $projectRoot 'tools\dotnet\dotnet.exe') -ArgumentList $runArguments -WorkingDirectory $projectRoot -WindowStyle Hidden

