#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Capture','Restart','WorkFeedback','MapPerformance','SettlementArt','RaidIntegration','SettlementCommands')][string] $Action='Capture',
    [int] $Width=1920,
    [int] $Height=1080,
    [ValidateSet(100,125,150)][int] $Scale=100
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'RenderValidation.ps1')
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$exe=Join-Path $project 'Builds/Windows/RimworldTweak.exe'
if(!(Test-Path -LiteralPath $exe)){throw 'Build the current Windows Player first.'}
$results=Join-Path $project 'TestResults'
[IO.Directory]::CreateDirectory($results)|Out-Null
$guard=[IO.File]::Open((Join-Path $results 'runner.lock'),'OpenOrCreate','ReadWrite','None')
try {
    $run=Join-Path $results ([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')+'-ux-'+$Action.ToLowerInvariant())
    [IO.Directory]::CreateDirectory($run)|Out-Null
    $storage=Join-Path $run 'storage'
    $modes=switch($Action){
        'RaidIntegration' {'raid-integration'}
        'SettlementCommands' {'settlement-commands'}
        'Restart' {@('save','load')}
        'WorkFeedback' {'work-feedback'}
        'SettlementArt' {'settlement-art'}
        'MapPerformance' {'map-performance'}
        default {'capture'}
    }
    $records=@()
    $renderFailures=@()
    $layoutFailures=@()
    foreach($mode in $modes){
        $output=Join-Path $run $mode
        [IO.Directory]::CreateDirectory($output)|Out-Null
        $info=[Diagnostics.ProcessStartInfo]::new($exe)
        $info.UseShellExecute=$false
        $info.CreateNoWindow=$true
        $info.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
        $info.WorkingDirectory=$project
        foreach($arg in @('-force-d3d11','-screen-fullscreen','0','-screen-width',"$Width",'-screen-height',"$Height",'-logFile',(Join-Path $output 'player.log'),'--ux-output',$output,'--ux-storage',$storage,'--ux-mode',$mode,'--ux-scale',"$Scale")){$info.ArgumentList.Add($arg)}
        $process=[Diagnostics.Process]::Start($info)
        Write-Output "Player $mode PID $($process.Id): $output"
        if(!$process.WaitForExit(120000)){throw "Player still running after observation deadline: PID $($process.Id). Do not start another run until inspected."}
        $resultPath=Join-Path $output 'ux-result.json'
        if(!(Test-Path $resultPath)){throw "Missing result: $resultPath (exit $($process.ExitCode))"}
        $result=Get-Content $resultPath -Raw|ConvertFrom-Json
        if($process.ExitCode -ne 0 -or !$result.passed){throw "Player failed: $($result.error)"}
        if($result.width -ne $Width -or $result.height -ne $Height -or $result.scale -ne $Scale){throw 'Actual resolution/scale differs from requested configuration.'}
        if($mode -eq 'map-performance'){
            foreach($size in @(32,250)){
                $measurements=Get-Content -Raw (Join-Path $output "performance-$size.json") | ConvertFrom-Json
                if($measurements.size -ne $size -or $measurements.slotBytes -le 0){throw "Missing performance evidence for $size"}
            }
            $records+=[pscustomobject]@{mode=$mode;processId=$process.Id;exitCode=$process.ExitCode;result=$result}
            $process.Dispose()
            continue
        }
        $diagnostic=$mode -in @('work-feedback','settlement-art','raid-integration','settlement-commands')
        $imageFilter=if($diagnostic){'*-offscreen.png'}else{'*.png'}
        $images=@(Get-ChildItem -LiteralPath $output -Filter $imageFilter)
        if($images.Count -eq 0){throw 'No screenshot evidence.'}
        foreach($png in $images){
            $render=Get-RenderVerdict -Path $png.FullName -ExpectedWidth $Width -ExpectedHeight $Height
            if(!$render.Passed){$renderFailures+="Blank or wrong-sized rendered image: $($png.FullName) ($($render.SampledColors) colors)"}
        }
        $layouts=@(Get-ChildItem -LiteralPath $output -Filter '*-layout.json')
        $requiredScreens=switch($mode){
            'capture' {@('default','sample-five','stove','work','building','save','confirmation','error')}
            'work-feedback' {@('cooking-start','cooking-half','cooking-done','neighbor-work','distant-selected-work','construction-route')}
            'settlement-art' {@('settlement-default','settlement-details','settlement-distant','settlement-cooking','settlement-harvest')}
            'raid-integration' {@('raid-start','raid-result')}
            'settlement-commands' {@('weapon-equipped','medicine-selected','priority-work','rescue-carry','rescue-bed')}
            'save' {'saved'}
            'load' {'restored'}
        }
        foreach($screen in $requiredScreens){
            if(!(Test-Path (Join-Path $output ($screen+'-layout.json')))){$layoutFailures+="Missing layout report: $screen"}
            if($diagnostic -and !(Test-Path (Join-Path $output ($screen+'-offscreen.png')))){$renderFailures+="Missing diagnostic render: $screen"}
        }
        foreach($layoutFile in $layouts){
            $layout=Get-Content $layoutFile.FullName -Raw|ConvertFrom-Json
            if($layout.width -ne $Width -or $layout.height -ne $Height -or $layout.scale -ne $Scale){$layoutFailures+="Layout configuration mismatch: $($layoutFile.Name)"}
            foreach($issue in $layout.issues){$layoutFailures+="$($layout.screen): $issue"}
        }
        $records+=[pscustomobject]@{mode=$mode;processId=$process.Id;exitCode=$process.ExitCode;result=$result}
        $process.Dispose()
    }
    ConvertTo-Json -InputObject $records -Depth 6|Set-Content (Join-Path $run 'summary.json') -Encoding utf8
    ConvertTo-Json -InputObject @($renderFailures) | Set-Content (Join-Path $run 'render-failures.json') -Encoding utf8
    ConvertTo-Json -InputObject @($layoutFailures) | Set-Content (Join-Path $run 'layout-failures.json') -Encoding utf8
    if($renderFailures.Count -gt 0 -or $layoutFailures.Count -gt 0){throw ((@($renderFailures)+@($layoutFailures)) -join [Environment]::NewLine)}
    Write-Output "UX check complete: $run. Render contents and human input remain separate checks."
} finally {$guard.Dispose()}
