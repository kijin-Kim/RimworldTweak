#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet("EditMode", "PlayMode")][string] $Platform = "EditMode",
    [string] $EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.28f1\Editor\Unity.exe',
    [ValidateRange(1, 3600)][int] $TimeoutSeconds = 600
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'TestResultValidation.ps1')
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$versionFile = Join-Path $project 'ProjectSettings/ProjectVersion.txt'
$versionMatch = [regex]::Match((Get-Content -LiteralPath $versionFile -Raw), '(?m)^m_EditorVersion: (\S+)')
if (-not $versionMatch.Success) { throw 'ProjectVersion.txt has no Editor version.' }
$version = $versionMatch.Groups[1].Value
$editor = Get-Item -LiteralPath $EditorPath
if ($editor.VersionInfo.ProductVersion.Split('_')[0] -ne $version) { throw 'Installed Editor version differs from ProjectVersion.txt.' }
$expected = @(Get-Content -LiteralPath (Join-Path $PSScriptRoot ("expected-" + $Platform.ToLowerInvariant() + "-tests.json")) -Raw | ConvertFrom-Json)
if ($expected.Count -eq 0) { throw 'Expected test list is empty.' }
$resultsRoot = Join-Path $project 'TestResults'
[IO.Directory]::CreateDirectory($resultsRoot) | Out-Null
$guardPath = Join-Path $resultsRoot 'runner.lock'
$guard = $null
$process = $null
$runDir = $null
$exit = 1
try {
    # OpenOrCreate + FileShare.None supplies an exclusive OS lock without deleting a stale file.
    $guard = [IO.File]::Open($guardPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)

    $unityProcesses = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'")
    foreach ($running in $unityProcesses) {
        if ([string]::IsNullOrWhiteSpace($running.CommandLine)) { throw 'Cannot inspect an existing Unity process; refusing to run concurrently.' }
        $normalized = $running.CommandLine.Replace('/', '\')
        if ($normalized.IndexOf($project.Replace('/', '\'), [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "Unity already uses this project (PID $($running.ProcessId)). Close it normally."
        }
    }
    $unityLock = Join-Path $project 'Temp/UnityLockfile'
    if (Test-Path $unityLock) {
        $probe = [IO.File]::Open($unityLock, 'Open', 'ReadWrite', 'None')
        $probe.Dispose()
    }
    $runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8)
    $runDir = Join-Path $resultsRoot $runId
    [IO.Directory]::CreateDirectory($runDir) | Out-Null
    $resultPath = Join-Path $runDir 'results.xml'
    $logPath = Join-Path $runDir 'editor.log'
    $inputs = @{}
    foreach ($folder in @('Assets', 'Packages', 'ProjectSettings', 'Tools')) {
        Get-ChildItem -LiteralPath (Join-Path $project $folder) -Recurse -File | ForEach-Object {
            $relative = [IO.Path]::GetRelativePath($project, $_.FullName).Replace('\','/')
            $inputs[$relative] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    }
    $inputs | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $runDir 'inputs-before.json') -Encoding utf8
    $arguments = @('-batchmode', '-nographics', '-projectPath', $project, '-runTests', '-testPlatform', $Platform,
        '-assemblyNames', ("RimworldTweak.Tests." + $Platform), '-testResults', $resultPath, '-logFile', $logPath)
    if (@($arguments | Where-Object { $_ -isnot [string] -or [string]::IsNullOrWhiteSpace($_) }).Count -gt 0) { throw "Invalid command argument." }
    $info = [Diagnostics.ProcessStartInfo]::new($editor.FullName)
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.WorkingDirectory = $project
    foreach ($argument in $arguments) { $info.ArgumentList.Add($argument) }
    $metadata = [ordered]@{
        RunId = $runId; StartedUtc = [DateTime]::UtcNow.ToString('o'); EditorVersion = $version
        EditorPath = $editor.FullName; Arguments = $arguments; ExpectedTests = $expected
        PackageManifestSha256 = $inputs['Packages/manifest.json']; PackageLockSha256 = $inputs['Packages/packages-lock.json']
    }
    $metadata | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $runDir 'run.json') -Encoding utf8
    Write-Host "Run directory: $runDir"
    $process = [Diagnostics.Process]::Start($info)
    $timedOut = -not $process.WaitForExit($TimeoutSeconds * 1000)
    if ($timedOut) {
        # Only this invocation's process tree can be stopped; never touch a user Editor.
        $process.Kill($true)
        $process.WaitForExit()
    }
    $verdict = Get-TestVerdict -ResultPath $resultPath -ExpectedNames $expected -ExitCode $process.ExitCode -TimedOut $timedOut
    $after = @{}
    foreach ($folder in @('Assets', 'Packages', 'ProjectSettings', 'Tools')) {
        Get-ChildItem -LiteralPath (Join-Path $project $folder) -Recurse -File | ForEach-Object {
            $relative = [IO.Path]::GetRelativePath($project, $_.FullName).Replace('\','/')
            $after[$relative] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    }
    $after | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $runDir 'inputs-after.json') -Encoding utf8
    $changed = @(@($inputs.Keys) + @($after.Keys) | Sort-Object -Unique | Where-Object { $inputs[$_] -ne $after[$_] })
    if ($changed.Count -gt 0) {
        $verdict.Passed = $false
        $verdict.Issues += @($changed | ForEach-Object { "InputChanged:$_" })
    }
    if (-not (Test-Path -LiteralPath $logPath)) { $verdict.Passed = $false; $verdict.Issues += 'MissingEditorLog' }
    $summary = [ordered]@{ FinishedUtc = [DateTime]::UtcNow.ToString('o'); ProcessExitCode = $process.ExitCode; TimedOut = $timedOut; Verdict = $verdict }
    $summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $runDir 'summary.json') -Encoding utf8
    $summary | ConvertTo-Json -Depth 8 | Write-Output
    if ($verdict.Passed) { $exit = 0 }
} catch {
    if ($runDir) {
        [ordered]@{ Passed = $false; Error = $_.Exception.Message; FinishedUtc = [DateTime]::UtcNow.ToString('o') } |
            ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runDir 'runner-error.json') -Encoding utf8
    }
    Write-Error $_ -ErrorAction Continue
} finally {
    if ($process) {
        try {
            if (-not $process.HasExited) {
                Write-Warning "Stopping only this runner's unfinished Unity process (PID $($process.Id))."
                $process.Kill($true)
                $process.WaitForExit()
            }
        } finally { $process.Dispose() }
    }
    if ($guard) { $guard.Dispose() }
}
exit $exit
