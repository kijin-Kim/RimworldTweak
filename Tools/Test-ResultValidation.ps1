#requires -Version 7.0
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'TestResultValidation.ps1')
$folder = Join-Path (Join-Path $PSScriptRoot '../TestResults') ('validator-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($folder) | Out-Null
$path = Join-Path $folder 'synthetic.xml'
$expected = @('Example.A', 'Example.B')
$valid = '<test-run result="Passed" total="2" failed="0" skipped="0" inconclusive="0"><test-suite><test-case fullname="Example.A" result="Passed"/><test-case fullname="Example.B" result="Passed"/></test-suite></test-run>'
$checks = [Collections.Generic.List[object]]::new()
function Check([string] $Name, [string] $XmlText, [bool] $ShouldPass, [int] $ExitCode = 0, [bool] $TimedOut = $false, [string[]] $Names = $expected) {
    Set-Content -LiteralPath $path -Value $XmlText -Encoding utf8
    $v = Get-TestVerdict -ResultPath $path -ExpectedNames $Names -ExitCode $ExitCode -TimedOut $TimedOut
    if ($v.Passed -ne $ShouldPass) { throw "$Name expected $ShouldPass but got $($v | ConvertTo-Json -Compress)" }
    $checks.Add([pscustomobject]@{ Case = $Name; Passed = $true; RejectionReasons = $v.Issues })
}
Check 'ValidExactSet' $valid $true
Check 'ZeroTests' '<test-run result="Passed" total="0" />' $false
Check 'MissingExpected' ($valid.Replace('<test-case fullname="Example.B" result="Passed"/>','').Replace('total="2"','total="1"')) $false
Check 'UnexpectedTest' ($valid.Replace('Example.B','Example.C')) $false
Check 'FailedCase' ($valid.Replace('fullname="Example.B" result="Passed"','fullname="Example.B" result="Failed"')) $false
Check 'SkippedCase' ($valid.Replace('fullname="Example.B" result="Passed"','fullname="Example.B" result="Skipped"')) $false
Check 'NonzeroProcess' $valid $false 2
Check 'Timeout' $valid $false 0 $true
Check 'MalformedXml' '<broken' $false
Check 'MissingRoot' '<test-suite />' $false
Check 'DuplicateCase' ($valid.Replace('Example.B','Example.A')) $false
Check 'SummaryMismatch' ($valid.Replace('total="2"','total="3"')) $false
Check 'RunFailure' ($valid.Replace('test-run result="Passed"','test-run result="Failed"')) $false
Check 'InconclusiveSummary' ($valid.Replace('inconclusive="0"','inconclusive="1"')) $false
$missing = Get-TestVerdict -ResultPath (Join-Path $folder 'absent.xml') -ExpectedNames $expected
if ($missing.Passed -or $missing.Issues -notcontains 'MissingResultXml') { throw 'MissingResultXml was not rejected.' }
$checks.Add([pscustomobject]@{ Case = 'MissingXml'; Passed = $true; RejectionReasons = $missing.Issues })
$checks | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $folder 'selftest-results.json') -Encoding utf8
Write-Output "PASS: $($checks.Count) validator checks. Evidence: $folder"
