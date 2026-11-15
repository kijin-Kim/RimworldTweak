#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Prepare', 'Build', 'Smoke')][string] $Action = 'Smoke',
    [string] $EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe',
    [ValidateRange(1,3600)][int] $TimeoutSeconds = 600
)
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'RenderValidation.ps1')
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$resultsRoot = Join-Path $project 'TestResults'
[IO.Directory]::CreateDirectory($resultsRoot) | Out-Null
$guard = [IO.File]::Open((Join-Path $resultsRoot 'runner.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
$runDir = $null
$process = $null
$exitCode = 1
try {

    foreach ($running in @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'")) {
        if (-not $running.CommandLine -or $running.CommandLine.Replace('/','\').Contains($project, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Unity project is running or cannot be inspected.'
        }
    }
    $unityLock = Join-Path $project 'Temp/UnityLockfile'
    if (Test-Path $unityLock) {
        # A crashed compiler can leave a file without a live owner. Do not delete it.
        # After process inspection, require exclusive access before Unity handles recovery.
        $probe = [IO.File]::Open($unityLock, 'Open', 'ReadWrite', 'None')
        $probe.Dispose()
    }
    $runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + $Action.ToLowerInvariant()
    $runDir = Join-Path $resultsRoot $runId
    [IO.Directory]::CreateDirectory($runDir) | Out-Null
    function Get-Inputs {
        $hashes = @{}
        foreach ($folder in @('Assets','Packages','ProjectSettings','Tools')) {
            Get-ChildItem (Join-Path $project $folder) -Recurse -File | ForEach-Object {
                $hashes[[IO.Path]::GetRelativePath($project, $_.FullName).Replace('\','/')] = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
            }
        }
        return $hashes
    }
    $before = Get-Inputs
    $before | ConvertTo-Json | Set-Content (Join-Path $runDir 'inputs-before.json')
    $buildRoot = Join-Path $project 'Builds/Windows'
    $provenancePath = Join-Path $buildRoot 'build-provenance.json'
    if ($Action -eq 'Smoke') {
        $provenance = Get-Content $provenancePath -Raw | ConvertFrom-Json -AsHashtable
        $sourceDiff = @(@($before.Keys) + @($provenance.Inputs.Keys) | Sort-Object -Unique | Where-Object { $before[$_] -ne $provenance.Inputs[$_] })
        if ($sourceDiff.Count -gt 0) { throw ('Build is stale; rebuild before smoke: ' + ($sourceDiff -join ', ')) }
        foreach ($relative in $provenance.Outputs.Keys) {
            $artifact = Join-Path $buildRoot $relative
            if (-not (Test-Path $artifact) -or (Get-FileHash $artifact -Algorithm SHA256).Hash -ne $provenance.Outputs[$relative]) {
                throw ('Build artifact differs from verified build: ' + $relative)
            }
        }
        $executable = Join-Path $project 'Builds/Windows/RimworldTweak.exe'
        $arguments = @('-screen-fullscreen','0','-screen-width','1280','-screen-height','800','-logFile',(Join-Path $runDir 'player.log'),'--smoke-output',$runDir)
    } else {
        $executable = $EditorPath
        $version = [regex]::Match((Get-Content (Join-Path $project 'ProjectSettings/ProjectVersion.txt') -Raw), '(?m)^m_EditorVersion: (\S+)').Groups[1].Value
        if ((Get-Item $executable).VersionInfo.ProductVersion.Split('_')[0] -ne $version) { throw 'Editor version mismatch.' }
        $method = if ($Action -eq 'Prepare') { 'PrepareScene' } else { 'BuildWindows' }
        $arguments = @('-batchmode','-nographics','-quit','-projectPath',$project,'-executeMethod',("RimworldTweak.Editor.DemoBuild." + $method),'-logFile',(Join-Path $runDir 'editor.log'))
    }
    $info = [Diagnostics.ProcessStartInfo]::new($executable)
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.WindowStyle = if ($Action -eq 'Smoke') { [Diagnostics.ProcessWindowStyle]::Normal } else { [Diagnostics.ProcessWindowStyle]::Hidden }
    $info.WorkingDirectory = $project
    foreach ($argument in $arguments) { $info.ArgumentList.Add($argument) }
    [ordered]@{ Action=$Action; Executable=$executable; Arguments=$arguments; StartedUtc=[DateTime]::UtcNow.ToString('o') } |
        ConvertTo-Json -Depth 5 | Set-Content (Join-Path $runDir 'run.json')
    Write-Host "Run directory: $runDir"
    $process = [Diagnostics.Process]::Start($info)
    $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
    if ($timedOut) { $process.Kill($true); $process.WaitForExit() }
    $after = Get-Inputs
    $after | ConvertTo-Json | Set-Content (Join-Path $runDir 'inputs-after.json')
    $changed = @(@($before.Keys) + @($after.Keys) | Sort-Object -Unique | Where-Object { $before[$_] -ne $after[$_] })
    $passed = -not $timedOut -and $process.ExitCode -eq 0
    if ($Action -ne 'Prepare' -and $changed.Count -gt 0) { $passed = $false }
    if ($Action -eq 'Smoke') {
        $smoke = Get-Content (Join-Path $runDir 'smoke.json') -Raw | ConvertFrom-Json
        if (-not $smoke.passed) { throw ('Player smoke failed: ' + $smoke.error) }
        $render = Get-RenderVerdict -Path (Join-Path $runDir 'demo.png')
        $render | ConvertTo-Json | Set-Content (Join-Path $runDir 'render.json')
        $passed = $passed -and $smoke.passed -and $render.Passed
    } elseif ($Action -eq 'Build') {
        $build = Get-Content (Join-Path $project 'Builds/Windows/build-summary.json') -Raw | ConvertFrom-Json
        $passed = $passed -and $build.result -eq 'Succeeded' -and (Test-Path (Join-Path $project 'Builds/Windows/RimworldTweak.exe'))
    } else {
        $passed = $passed -and (Select-String -Path (Join-Path $runDir 'editor.log') -Pattern 'DEMO_SCENE_PREPARED' -Quiet)
    }
    $summary = [ordered]@{ Action=$Action; Passed=$passed; ProcessExitCode=$process.ExitCode; TimedOut=$timedOut; ChangedInputs=$changed; FinishedUtc=[DateTime]::UtcNow.ToString('o') }
    $summary | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $runDir 'summary.json')
    $summary | ConvertTo-Json -Depth 5 | Write-Output
    if ($passed -and $Action -eq 'Build') {
        $outputs = @{}
        Get-ChildItem $buildRoot -Recurse -File | Where-Object { $_.FullName -ne $provenancePath } | ForEach-Object {
            $outputs[[IO.Path]::GetRelativePath($buildRoot, $_.FullName)] = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
        }
        [ordered]@{ RunId=$runId; Inputs=$before; Outputs=$outputs } | ConvertTo-Json -Depth 5 | Set-Content $provenancePath
    }
    if ($passed) { $exitCode = 0 }
} catch {
    if ($runDir) {
        [ordered]@{ Action=$Action; Passed=$false; Error=$_.Exception.Message } |
            ConvertTo-Json | Set-Content (Join-Path $runDir 'summary.json')
    }
    Write-Error $_ -ErrorAction Continue
} finally {
    if ($process) {
        if (-not $process.HasExited) { $process.Kill($true); $process.WaitForExit() }
        $process.Dispose()
    }
    $guard.Dispose()
}
exit $exitCode
