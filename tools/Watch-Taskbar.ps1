param([int]$DockPid,[long]$DockStarted,[string]$WindhawkPath)
$ErrorActionPreference='SilentlyContinue'
$dockProcess=Get-Process -Id $DockPid
if($dockProcess -and $dockProcess.StartTime.ToUniversalTime().Ticks -eq $DockStarted){$dockProcess.WaitForExit()}
# Only the dedicated portable adapter may be stopped here.
$owned=Get-Process -Name windhawk | Where-Object {$_.Path -eq $WindhawkPath}
if($owned){Start-Process -FilePath $WindhawkPath -ArgumentList '-exit' -WindowStyle Hidden -Wait}
