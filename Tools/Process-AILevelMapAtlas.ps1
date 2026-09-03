param(
    [Parameter(Mandatory = $true)]
    [string]$Source,

    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\Assets\Resources\Art\LevelMapAI")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$resolvedSource = (Resolve-Path -LiteralPath $Source).Path
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null

$atlas = [System.Drawing.Bitmap]::FromFile($resolvedSource)
$transparent = New-Object System.Drawing.Bitmap($atlas.Width, $atlas.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

try {
    # Image generators sometimes render a checkerboard instead of real alpha.
    # Remove only bright, almost-neutral pixels; coloured neon glows remain feathered.
    for ($y = 0; $y -lt $atlas.Height; $y++) {
        for ($x = 0; $x -lt $atlas.Width; $x++) {
            $pixel = $atlas.GetPixel($x, $y)
            $maximum = [Math]::Max($pixel.R, [Math]::Max($pixel.G, $pixel.B))
            $minimum = [Math]::Min($pixel.R, [Math]::Min($pixel.G, $pixel.B))
            $chroma = $maximum - $minimum
            $luma = (0.2126 * $pixel.R) + (0.7152 * $pixel.G) + (0.0722 * $pixel.B)

            $neutrality = 1.0 - [Math]::Min(1.0, [Math]::Max(0.0, ($chroma - 3.0) / 20.0))
            $brightness = [Math]::Min(1.0, [Math]::Max(0.0, ($luma - 205.0) / 35.0))
            $background = $neutrality * $brightness
            $alpha = [int][Math]::Round(255.0 * (1.0 - $background))
            if ($alpha -lt 5) { $alpha = 0 }

            $transparent.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($alpha, $pixel.R, $pixel.G, $pixel.B))
        }
    }

    $halfWidth = [int][Math]::Floor($atlas.Width / 2)
    $halfHeight = [int][Math]::Floor($atlas.Height / 2)
    $rightWidth = $atlas.Width - $halfWidth
    $bottomHeight = $atlas.Height - $halfHeight
    $items = @(
        @{ Name = "stage-node-frame-ai-v1.png"; Region = New-Object System.Drawing.Rectangle(0, 0, $halfWidth, $halfHeight) },
        @{ Name = "chapter-progress-ring-ai-v1.png"; Region = New-Object System.Drawing.Rectangle($halfWidth, 0, $rightWidth, $halfHeight) },
        @{ Name = "chapter-reward-chest-ai-v1.png"; Region = New-Object System.Drawing.Rectangle(0, $halfHeight, $halfWidth, $bottomHeight) },
        @{ Name = "chapter-action-frame-ai-v1.png"; Region = New-Object System.Drawing.Rectangle($halfWidth, $halfHeight, $rightWidth, $bottomHeight) }
    )

    foreach ($item in $items) {
        $region = $item.Region
        $left = $region.Right
        $top = $region.Bottom
        $right = $region.Left
        $bottom = $region.Top

        for ($y = $region.Top; $y -lt $region.Bottom; $y++) {
            for ($x = $region.Left; $x -lt $region.Right; $x++) {
                if ($transparent.GetPixel($x, $y).A -gt 10) {
                    if ($x -lt $left) { $left = $x }
                    if ($x -gt $right) { $right = $x }
                    if ($y -lt $top) { $top = $y }
                    if ($y -gt $bottom) { $bottom = $y }
                }
            }
        }

        if ($right -lt $left -or $bottom -lt $top) {
            throw "No foreground pixels found for $($item.Name)."
        }

        $padding = 18
        $left = [Math]::Max($region.Left, $left - $padding)
        $top = [Math]::Max($region.Top, $top - $padding)
        $right = [Math]::Min($region.Right - 1, $right + $padding)
        $bottom = [Math]::Min($region.Bottom - 1, $bottom + $padding)
        $cropWidth = $right - $left + 1
        $cropHeight = $bottom - $top + 1
        $cropRect = New-Object System.Drawing.Rectangle($left, $top, $cropWidth, $cropHeight)
        $crop = $transparent.Clone($cropRect, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $destination = Join-Path $resolvedOutput $item.Name
            $crop.Save($destination, [System.Drawing.Imaging.ImageFormat]::Png)
            Write-Host "Created $destination ($($crop.Width)x$($crop.Height))"
        }
        finally {
            $crop.Dispose()
        }
    }
}
finally {
    $transparent.Dispose()
    $atlas.Dispose()
}
