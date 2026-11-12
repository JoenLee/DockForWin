$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$env:DOTNET_CLI_HOME=Join-Path $root 'tools\.cli'
$env:NUGET_PACKAGES=Join-Path $root 'tools\.nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$output=Join-Path $root 'docs\widget-gallery-preview'
$build=Join-Path $output 'build'
$dotnet=Join-Path $root 'tools\dotnet\dotnet.exe'
New-Item -ItemType Directory -Path $output -Force | Out-Null
# Match Build.ps1's local SDK/shader preparation, while leaving the running production DLL untouched.
& (Join-Path $root 'tools\Compile-Shaders.ps1')
& $dotnet build (Join-Path $root 'tests\DockForWindows.Tests\DockForWindows.Tests.csproj') -c Release -o $build --nologo -p:NuGetAudit=false
if($LASTEXITCODE -ne 0){throw 'Widget preview build failed. Close an earlier preview before rebuilding.'}
Start-Process -FilePath $dotnet -ArgumentList @(('"'+(Join-Path $build 'DockForWindows.Tests.dll')+'"'),'--preview-widget-gallery',('"'+$output+'"')) -WorkingDirectory $root -WindowStyle Hidden
