$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_HOME=Join-Path $root 'tools\.cli'
$env:NUGET_PACKAGES=Join-Path $root 'tools\.nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$dotnet=Join-Path $root 'tools\dotnet\dotnet.exe'
& (Join-Path $root 'tools\Build.ps1') -Configuration Release
if($LASTEXITCODE -ne 0){throw 'Application build failed'}
& $dotnet build (Join-Path $root 'tests\DockForWindows.Tests\DockForWindows.Tests.csproj') -c Release --nologo -p:NuGetAudit=false
if($LASTEXITCODE -ne 0){throw 'Preview host build failed'}
$assembly=Join-Path $root 'tests\DockForWindows.Tests\bin\Release\net10.0-windows\DockForWindows.Tests.dll'
$output=Join-Path $root 'docs\settings-full-preview'
Start-Process -FilePath $dotnet -ArgumentList ('"'+$assembly+'" --preview-full-settings "'+$output+'"') -WorkingDirectory $root -WindowStyle Hidden
