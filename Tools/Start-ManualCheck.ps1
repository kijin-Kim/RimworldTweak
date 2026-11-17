#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet(1280,1920,2560)][int] $Width=1280,
    [ValidateSet(720,800,1080,1440)][int] $Height=720,
    [switch] $PrintOnly
)
$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$exe=Join-Path $project 'Builds/Windows/RimworldTweak.exe'
$storage=Join-Path $project 'TestResults/ManualAcceptance'
if(!(Test-Path -LiteralPath $exe)){throw 'Build the Windows Player before the manual check.'}
$log=Join-Path $storage ('player-'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')+'.log')
$playerArgs=@('-force-d3d11','-screen-fullscreen','0','-screen-width',"$Width",'-screen-height',"$Height",'--storage-root',$storage,'-logFile',$log)
[pscustomobject]@{Executable=$exe;StorageRoot=$storage;PlayerLog=$log;Arguments=$playerArgs;WillLaunch=(!$PrintOnly)} | ConvertTo-Json
if($PrintOnly){return}
[IO.Directory]::CreateDirectory($storage)|Out-Null
# Invoked by the user for interactive checks; no automated clicks or scripted gameplay.
& $exe @playerArgs
