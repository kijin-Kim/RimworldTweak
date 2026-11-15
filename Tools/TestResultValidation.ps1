Set-StrictMode -Version Latest

function Get-TestVerdict {
    param(
        [Parameter(Mandatory)][string] $ResultPath,
        [Parameter(Mandatory)][string[]] $ExpectedNames,
        [int] $ExitCode = 0,
        [bool] $TimedOut = $false
    )
    $issues = [System.Collections.Generic.List[string]]::new()
    if ($TimedOut) { $issues.Add('Timeout') }
    if ($ExitCode -ne 0) { $issues.Add("ProcessExitCode:$ExitCode") }
    if ($ExpectedNames.Count -eq 0 -or @($ExpectedNames | Select-Object -Unique).Count -ne $ExpectedNames.Count) {
        $issues.Add('InvalidExpectedTestList')
    }
    $cases = @()
    if (-not (Test-Path -LiteralPath $ResultPath -PathType Leaf)) {
        $issues.Add('MissingResultXml')
    } else {
        try {
            $settings = [System.Xml.XmlReaderSettings]::new()
            $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
            $reader = [System.Xml.XmlReader]::Create($ResultPath, $settings)
            try { $xml = [System.Xml.XmlDocument]::new(); $xml.XmlResolver = $null; $xml.Load($reader) }
            finally { $reader.Dispose() }
            $run = $xml.SelectSingleNode('/test-run')
            if ($null -eq $run) { throw 'Missing test-run root' }
            $cases = @($run.SelectNodes('.//test-case'))
            if ($cases.Count -eq 0) { $issues.Add('ZeroTests') }
            if ($run.GetAttribute('result') -ne 'Passed') { $issues.Add('RunNotPassed') }
            foreach ($name in $ExpectedNames) {
                $found = @($cases | Where-Object { $_.GetAttribute('fullname') -ceq $name })
                if ($found.Count -ne 1) { $issues.Add("ExpectedTestCount:$name=$($found.Count)") }
            }
            foreach ($case in $cases) {
                $name = $case.GetAttribute('fullname')
                if ($ExpectedNames -cnotcontains $name) { $issues.Add("UnexpectedTest:$name") }
                if ($case.GetAttribute('result') -ne 'Passed') { $issues.Add("TestNotPassed:$name") }
            }
            foreach ($attribute in @('failed', 'skipped', 'inconclusive')) {
                if ($run.HasAttribute($attribute) -and [int]$run.GetAttribute($attribute) -ne 0) {
                    $issues.Add("NonzeroSummary:$attribute")
                }
            }
            if (-not $run.HasAttribute('total') -or [int]$run.GetAttribute('total') -ne $cases.Count) {
                $issues.Add('TotalCountMismatch')
            }
        } catch { $issues.Add("InvalidResultXml:$($_.Exception.Message)") }
    }
    [pscustomobject]@{
        Passed = $issues.Count -eq 0
        ExpectedCount = $ExpectedNames.Count
        ActualCount = $cases.Count
        Issues = @($issues.ToArray())
    }
}
