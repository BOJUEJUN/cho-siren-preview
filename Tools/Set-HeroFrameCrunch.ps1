param(
    [string]$HeroFramesPath = (Join-Path $PSScriptRoot '..\Assets\Resources\Art\HeroFrames'),
    [int]$CompressionQuality = 70,
    [switch]$Revert,
    [switch]$WhatIf
)

# Batch-edits the Standalone and WebGL platform blocks of hero_*.png.meta so the
# 238-frame lobby animation is imported with Crunch compression. DefaultTexturePlatform
# and Android blocks, maxTextureSize and frame count are deliberately left untouched.
# Mirrors Assets/Editor/HeroFrameImportProcessor.cs; use -Revert to restore 82 / no crunch.
# Files are rewritten as UTF-8 without BOM and keep LF line endings.

$ErrorActionPreference = 'Stop'

$root = [IO.Path]::GetFullPath($HeroFramesPath)
if (-not (Test-Path -LiteralPath $root)) {
    throw "HeroFrames folder not found: $root"
}

$fromQuality = if ($Revert) { $CompressionQuality } else { 82 }
$toQuality = if ($Revert) { 82 } else { $CompressionQuality }
$fromCrunch = if ($Revert) { 1 } else { 0 }
$toCrunch = if ($Revert) { 0 } else { 1 }

# A platform block starts with "  - serializedVersion: 4" followed by its buildTarget and
# continues while lines are indented by exactly four spaces.
$blockPattern = [regex]'(?m)^  - serializedVersion: 4\n    buildTarget: (?:Standalone|WebGL)\n(?:    [^\n]*\n)*'
$qualityPattern = [regex]"(?m)^(    compressionQuality: )$fromQuality$"
$crunchPattern = [regex]"(?m)^(    crunchedCompression: )$fromCrunch$"

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$files = Get-ChildItem -LiteralPath $root -Filter 'hero_*.png.meta' -File | Sort-Object Name
$changed = 0
$unchanged = 0
$blocksTouched = 0

foreach ($file in $files) {
    $original = [IO.File]::ReadAllText($file.FullName, $utf8NoBom)
    if ($original.Contains("`r")) {
        throw "Unexpected CR in $($file.Name); refusing to rewrite line endings."
    }

    $localBlocks = @($blockPattern.Matches($original) | Where-Object {
        $qualityPattern.IsMatch($_.Value) -or $crunchPattern.IsMatch($_.Value)
    }).Count
    $updated = $blockPattern.Replace($original, {
        param($match)
        $block = $match.Value
        $block = $qualityPattern.Replace($block, ('${1}' + $toQuality))
        $crunchPattern.Replace($block, ('${1}' + $toCrunch))
    })

    if ($updated -eq $original) {
        $unchanged++
        continue
    }

    $blocksTouched += $localBlocks
    $changed++
    if (-not $WhatIf) {
        [IO.File]::WriteAllText($file.FullName, $updated, $utf8NoBom)
    }
}

[pscustomobject]@{
    mode = if ($Revert) { 'revert' } else { 'apply' }
    whatIf = [bool]$WhatIf
    folder = $root
    totalMetaFiles = $files.Count
    changedFiles = $changed
    unchangedFiles = $unchanged
    platformBlocksTouched = $blocksTouched
    compressionQuality = $toQuality
    crunchedCompression = $toCrunch
} | ConvertTo-Json
