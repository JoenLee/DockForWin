param([string]$OutputRoot='')
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
if(!$OutputRoot){$OutputRoot=Join-Path $projectRoot 'dist'}
$OutputRoot=[System.IO.Path]::GetFullPath($OutputRoot)
$env:DOTNET_CLI_HOME=Join-Path $projectRoot 'tools\.cli'
$env:NUGET_PACKAGES=Join-Path $projectRoot 'tools\.nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$dotnet=Join-Path $projectRoot 'tools\dotnet\dotnet.exe'
$project=Join-Path $projectRoot 'src\DockForWindows\DockForWindows.csproj'
$version=([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version | Where-Object {$_} | Select-Object -First 1
if($version -notmatch '^\d+\.\d+\.\d+$'){throw 'The project must have a numeric three-part release version.'}
$compiler=Join-Path $projectRoot 'tools\installer\inno\ISCC.exe'
if(!(Test-Path -LiteralPath $compiler)){$command=Get-Command ISCC.exe -ErrorAction SilentlyContinue;if($command){$compiler=$command.Source}}
if(!(Test-Path -LiteralPath $dotnet)){throw 'Install the SDK using the instructions in README.md first.'}
if(!(Test-Path -LiteralPath $compiler)){throw 'Install Inno Setup and add ISCC.exe to PATH, or use tools\installer\inno.'}
$payload=Join-Path $OutputRoot 'payload'
New-Item -ItemType Directory -Path $OutputRoot,$payload -Force | Out-Null
if(@(Get-ChildItem -LiteralPath $payload -Force).Count){throw 'Choose an empty output directory to avoid packaging stale files.'}
& (Join-Path $projectRoot 'tools\Compile-Shaders.ps1')
# Leave the inner bundle uncompressed: LZMA2 can find matches across the whole runtime and application.
# WPF and Windows Forms do not support safe trimming; keep reflection, COM, fonts and shader resources intact.
& $dotnet publish $project -c Release -r win-x64 --self-contained true -p:UseAppHost=true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=false -p:PublishTrimmed=false -p:PublishReadyToRun=false -p:DebugType=None -p:DebugSymbols=false -o $payload --nologo
if($LASTEXITCODE -ne 0){throw 'Application publish failed'}
$allowed=Get-ChildItem -LiteralPath $payload -Recurse -File | Where-Object { $_.Extension -in @('.json','.pdb','.log','.lnk') -or $_.FullName -match '[\\/]data[\\/]' }
if($allowed){throw ('Unexpected personal/debug/config files in publish output: '+($allowed.Name -join ', '))}
$licenses=Join-Path $payload 'Licenses'
New-Item -ItemType Directory -Path $licenses -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\DockForWindows\Assets\Fonts\VarelaRound-OFL.txt') -Destination (Join-Path $licenses 'VarelaRound-OFL.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\DockForWindows\Assets\FigmaWidgets\fonts\OFL.txt') -Destination (Join-Path $licenses 'Inter-OFL.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'src\DockForWindows\Assets\Ionicons\LICENSE') -Destination (Join-Path $licenses 'Ionicons-LICENSE.txt')
foreach($family in @('microsoft.netcore.app.runtime.win-x64','microsoft.windowsdesktop.app.runtime.win-x64')){
    $packageRoot=Join-Path $env:NUGET_PACKAGES $family
    $package=Get-ChildItem -LiteralPath $packageRoot -Directory | Where-Object Name -match '^\d+\.\d+\.\d+$' | Sort-Object {[version]$_.Name} -Descending | Select-Object -First 1
    if(!$package){throw ('Runtime license package missing: '+$family)}
    foreach($notice in @('LICENSE.TXT','THIRD-PARTY-NOTICES.TXT')){
        $inputFile=Join-Path $package.FullName $notice
        if(Test-Path -LiteralPath $inputFile){Copy-Item -LiteralPath $inputFile -Destination (Join-Path $licenses ($family+'-'+$notice))}
    }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install-Readme.txt') -Destination (Join-Path $payload 'Install-Readme.txt')
& $compiler "/DPayloadDir=$payload" "/DDestinationDir=$OutputRoot" "/DAppVersion=$version" (Join-Path $PSScriptRoot 'DockForWin.iss')
if($LASTEXITCODE -ne 0){throw 'Installer compilation failed'}
$installer=Join-Path $OutputRoot ('DockForWin-Setup-'+$version+'-x64.exe')
$hash=(Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText((Join-Path $OutputRoot 'SHA256SUMS.txt'),$hash+'  '+[System.IO.Path]::GetFileName($installer)+[Environment]::NewLine,[System.Text.UTF8Encoding]::new($false))
[PSCustomObject]@{Installer=$installer;Bytes=(Get-Item -LiteralPath $installer).Length;SHA256=$hash;IncludesPersonalData=$false;SelfContained=$true}
