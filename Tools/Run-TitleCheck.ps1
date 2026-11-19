#requires -Version 7.0
[CmdletBinding()]
param([int]$Width=1920, [int]$Height=1080, [ValidateSet(100,125,150)][int]$Scale=100)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'RenderValidation.ps1')
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$exe=Join-Path $project 'Builds/Windows/RimworldTweak.exe'
$results=Join-Path $project 'TestResults'
[IO.Directory]::CreateDirectory($results) | Out-Null
$guard=[IO.File]::Open((Join-Path $results 'runner.lock'),'OpenOrCreate','ReadWrite','None')
try {
    $run=Join-Path $results ([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')+'-title-player')
    $storage=Join-Path $run 'storage'
    $settings=Join-Path $storage 'Settings'
    [IO.Directory]::CreateDirectory($settings) | Out-Null
    @{version=1;uiScalePercent=$Scale;recentSlotId=''} | ConvertTo-Json | Set-Content (Join-Path $settings 'user-settings.json') -Encoding utf8
    $info=[Diagnostics.ProcessStartInfo]::new($exe)
    $info.UseShellExecute=$false
    $info.CreateNoWindow=$true
    $info.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
    $info.WorkingDirectory=$project
    foreach($arg in @('-force-d3d11','-screen-fullscreen','0','-screen-width',"$Width",'-screen-height',"$Height",'-logFile',(Join-Path $run 'player.log'),'--storage-root',$storage,'--title-check',$run)){
        $info.ArgumentList.Add($arg)
    }
    $process=[Diagnostics.Process]::Start($info)
    Write-Output "Title Player PID $($process.Id) at $run"
    if(!$process.WaitForExit(180000)){throw "Player still live after observation deadline. Inspect PID $($process.Id) before another run."}
    $report=Get-Content -Raw -LiteralPath (Join-Path $run 'title-result.json') | ConvertFrom-Json
    if ($process.ExitCode -ne 0 -or !$report.passed) {throw "Player failed: $($report.error)"}
    if ($report.width -ne $Width -or $report.height -ne $Height -or $report.scale -ne $Scale) {throw 'Unexpected Player resolution or scale.'}
    foreach ($image in @('title','empty-list','saved-list','failed-list')) {
        $render=Get-RenderVerdict -Path (Join-Path $run ($image+'.png')) -ExpectedWidth $Width -ExpectedHeight $Height
        if (!$render.Passed) {throw "Title render failed for $image ($($render.SampledColors) colors)."}
    }
    Write-Output "Verified title Player at $run"
} finally { $guard.Dispose() }
