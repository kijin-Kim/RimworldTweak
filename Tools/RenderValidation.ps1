function Get-RenderVerdict {
    param([Parameter(Mandatory)][string] $Path)
    Add-Type -AssemblyName System.Drawing
    $bitmap = [System.Drawing.Bitmap]::new($Path)
    try {
        $colors = [Collections.Generic.HashSet[int]]::new()
        for ($y = 0; $y -lt $bitmap.Height; $y += 16) {
            for ($x = 0; $x -lt $bitmap.Width; $x += 16) { [void]$colors.Add($bitmap.GetPixel($x,$y).ToArgb()) }
        }
        [pscustomobject]@{ Passed=($bitmap.Width -eq 1280 -and $bitmap.Height -eq 800 -and $colors.Count -gt 8); Width=$bitmap.Width; Height=$bitmap.Height; SampledColors=$colors.Count }
    } finally { $bitmap.Dispose() }
}
