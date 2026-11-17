function Get-RenderVerdict {
    param([Parameter(Mandatory)][string] $Path, [int] $ExpectedWidth=1280, [int] $ExpectedHeight=800)
    Add-Type -AssemblyName System.Drawing
    $bitmap = [System.Drawing.Bitmap]::new($Path)
    try {
        $colors = [Collections.Generic.HashSet[int]]::new()
        for ($y = 0; $y -lt $bitmap.Height; $y += 16) {
            for ($x = 0; $x -lt $bitmap.Width; $x += 16) { [void]$colors.Add($bitmap.GetPixel($x,$y).ToArgb()) }
        }
        [pscustomobject]@{ Passed=($bitmap.Width -eq $ExpectedWidth -and $bitmap.Height -eq $ExpectedHeight -and $colors.Count -gt 8); Width=$bitmap.Width; Height=$bitmap.Height; SampledColors=$colors.Count }
    } finally { $bitmap.Dispose() }
}
