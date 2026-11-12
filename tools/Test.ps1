$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $projectRoot 'tools\.cli'
$env:NUGET_PACKAGES = Join-Path $projectRoot 'tools\.nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOCK_TEST_ARTIFACTS = Join-Path $projectRoot 'tests\artifacts'
New-Item -ItemType Directory -Path $env:DOCK_TEST_ARTIFACTS -Force | Out-Null
$dotnetExe = Join-Path $projectRoot 'tools\dotnet\dotnet.exe'
Push-Location $projectRoot
try {
    & (Join-Path $PSScriptRoot 'Compile-Shaders.ps1')
    & $dotnetExe run --project 'tests\DockForWindows.Tests\DockForWindows.Tests.csproj' --no-launch-profile
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
} finally { Pop-Location }
