param(
    [Parameter(Mandatory = $true)][string]$InputPath,
    [Parameter(Mandatory = $true)][string]$OutputPath
)

Add-Type -AssemblyName System.Drawing

$source = [System.Drawing.Bitmap]::new((Resolve-Path -LiteralPath $InputPath).Path)
$output = [System.Drawing.Bitmap]::new($source.Width, $source.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

try {
    for ($y = 0; $y -lt $source.Height; $y++) {
        for ($x = 0; $x -lt $source.Width; $x++) {
            $pixel = $source.GetPixel($x, $y)
            $isGreenKey = $pixel.G -gt 120 -and $pixel.G -gt ($pixel.R + 55) -and $pixel.G -gt ($pixel.B + 55)

            if ($isGreenKey) {
                $output.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, $pixel.R, $pixel.G, $pixel.B))
            } else {
                $output.SetPixel($x, $y, $pixel)
            }
        }
    }

    $resolvedOutput = Join-Path (Get-Location) $OutputPath
    $output.Save($resolvedOutput, [System.Drawing.Imaging.ImageFormat]::Png)
} finally {
    $output.Dispose()
    $source.Dispose()
}
