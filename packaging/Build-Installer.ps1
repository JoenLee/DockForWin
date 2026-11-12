param([string]$OutputRoot='D:\DockForWin')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_HOME=Join-Path $projectRoot 'tools\.cli';$env:NUGET_PACKAGES=Join-Path $projectRoot 'tools\.nuget';$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$dotnet=Join-Path $projectRoot 'tools\dotnet\dotnet.exe'
$project=Join-Path $projectRoot 'src\DockForWindows\DockForWindows.csproj'
$portable=Join-Path $OutputRoot 'Portable'
New-Item -ItemType Directory -Path $OutputRoot,$portable -Force|Out-Null
& (Join-Path $projectRoot 'tools\Compile-Shaders.ps1')
& (Join-Path $PSScriptRoot 'Create-Icon.ps1') -OutputFile (Join-Path $projectRoot 'src\DockForWindows\Assets\DockForWindows.ico')
& $dotnet restore $project -r win-x64 -p:SelfContained=true -p:UseAppHost=true -p:PublishSingleFile=true -p:NuGetAudit=false --source 'https://api.nuget.org/v3/index.json' --nologo
if($LASTEXITCODE-ne0){throw 'Publish restore failed'}
& $dotnet publish $project -c Release -r win-x64 --self-contained true --no-restore -p:UseAppHost=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o $portable --nologo
if($LASTEXITCODE-ne0){throw 'Application publish failed'}
Copy-Item -LiteralPath(Join-Path $PSScriptRoot 'Install-Readme.txt') -Destination(Join-Path $portable 'Install-Readme.txt') -Force
$compiler=Join-Path $projectRoot 'tools\installer\inno\ISCC.exe'
& $compiler "/DPayloadDir=$portable" "/DDestinationDir=$OutputRoot" (Join-Path $PSScriptRoot 'DockForWin.iss')
if($LASTEXITCODE-ne0){throw 'Installer compilation failed'}
Get-FileHash -LiteralPath(Join-Path $OutputRoot 'DockForWin-Setup-0.6.0-x64.exe') -Algorithm SHA256
