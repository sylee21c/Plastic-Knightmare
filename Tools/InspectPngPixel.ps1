param(
    [Parameter(Mandatory = $true)][string]$InputPath,
    [int]$X = 10,
    [int]$Y = 10
)

Add-Type -AssemblyName System.Drawing
$bitmap = [System.Drawing.Bitmap]::new((Resolve-Path -LiteralPath $InputPath).Path)
try {
    $pixel = $bitmap.GetPixel($X, $Y)
    Write-Output "A=$($pixel.A) R=$($pixel.R) G=$($pixel.G) B=$($pixel.B)"
} finally {
    $bitmap.Dispose()
}
