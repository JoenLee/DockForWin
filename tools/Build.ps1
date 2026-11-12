param([ValidateSet('Debug','Release')][string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $projectRoot 'tools\.cli'
$env:NUGET_PACKAGES = Join-Path $projectRoot 'tools\.nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$dotnetExe = Join-Path $projectRoot 'tools\dotnet\dotnet.exe'
if (!(Test-Path -LiteralPath $dotnetExe)) { throw 'Local SDK missing. See README.md.' }
Push-Location $projectRoot
try {
    & (Join-Path $PSScriptRoot 'Compile-Shaders.ps1')
    & $dotnetExe build 'src\DockForWindows\DockForWindows.csproj' -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
} finally { Pop-Location }
