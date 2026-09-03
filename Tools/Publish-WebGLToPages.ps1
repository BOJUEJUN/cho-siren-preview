[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$BuildPath = (Join-Path $PSScriptRoot '..\Builds\WebGL'),
    [string]$PagesPath = (Join-Path $PSScriptRoot '..\..\cho-siren-pages'),
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$effectiveDryRun = [bool]($DryRun -or $WhatIfPreference)

$unityAssetPattern = '^[0-9a-f]{32}\.(?:data\.unityweb|framework\.js\.unityweb|wasm\.unityweb|loader\.js)$'
$indexAssetPattern = 'buildAssetUrl\("(?<file>[^\"]+\.(?:data\.unityweb|framework\.js\.unityweb|wasm\.unityweb|loader\.js))"\)'
$pathComparison = [StringComparison]::OrdinalIgnoreCase

function Resolve-ExistingDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $resolved = [IO.Path]::GetFullPath($Path).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $resolved -PathType Container)) {
        throw "$Label directory does not exist: $resolved"
    }

    return $resolved
}

function Test-IsSameOrChildPath {
    param(
        [Parameter(Mandatory = $true)][string]$Candidate,
        [Parameter(Mandatory = $true)][string]$Root
    )

    $candidatePath = [IO.Path]::GetFullPath($Candidate).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    if ($candidatePath.Equals($rootPath, $pathComparison)) {
        return $true
    }

    $rootPrefix = $rootPath + [IO.Path]::DirectorySeparatorChar
    return $candidatePath.StartsWith($rootPrefix, $pathComparison)
}

function Assert-ChildPath {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $resolved = [IO.Path]::GetFullPath($Path)
    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    if (-not (Test-IsSameOrChildPath -Candidate $resolved -Root $rootPath) -or
        $resolved.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar).Equals($rootPath, $pathComparison)) {
        throw "$Label escaped its approved root: $resolved (root: $rootPath)"
    }

    return $resolved
}

function Assert-NoReparsePoints {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Label,
        [switch]$RootOnly
    )

    $rootItem = Get-Item -LiteralPath $Root -Force
    if (($rootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "$Label root must not be a symbolic link or junction: $Root"
    }
    if ($RootOnly) {
        return
    }

    $reparsePoints = @(Get-ChildItem -LiteralPath $Root -Force -Recurse -ErrorAction Stop |
        Where-Object { ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0 })
    if ($reparsePoints.Count -gt 0) {
        throw "$Label contains a symbolic link or junction: $($reparsePoints[0].FullName)"
    }
}

function Get-IndexBuildAssets {
    param(
        [Parameter(Mandatory = $true)][string]$IndexPath,
        [Parameter(Mandatory = $true)][string]$Label
    )

    if (-not (Test-Path -LiteralPath $IndexPath -PathType Leaf)) {
        throw "$Label index.html is missing: $IndexPath"
    }

    $html = Get-Content -Raw -LiteralPath $IndexPath
    $matches = [regex]::Matches($html, $indexAssetPattern)
    if ($matches.Count -ne 4) {
        throw "$Label index.html must reference exactly four Unity build assets; found $($matches.Count)."
    }

    $assets = @($matches | ForEach-Object { $_.Groups['file'].Value })
    if (@($assets | Sort-Object -Unique).Count -ne 4) {
        throw "$Label index.html contains duplicate Unity build asset references."
    }

    foreach ($asset in $assets) {
        if ($asset -notmatch $unityAssetPattern -or [IO.Path]::GetFileName($asset) -ne $asset) {
            throw "$Label index.html contains an unsafe or non-hashed Unity build asset: $asset"
        }
    }

    $expectedSuffixes = @(
        '.data.unityweb',
        '.framework.js.unityweb',
        '.wasm.unityweb',
        '.loader.js'
    )
    foreach ($suffix in $expectedSuffixes) {
        if (@($assets | Where-Object { $_.EndsWith($suffix, [StringComparison]::Ordinal) }).Count -ne 1) {
            throw "$Label index.html must reference exactly one *$suffix asset."
        }
    }

    return $assets
}

function Get-SafeRelativeFileMap {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $map = @{}
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) {
        return $map
    }

    $rootPath = [IO.Path]::GetFullPath($Root).TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
    foreach ($item in @(Get-ChildItem -LiteralPath $rootPath -File -Force -Recurse)) {
        $fullPath = Assert-ChildPath -Path $item.FullName -Root $rootPath -Label $Label
        $relativePath = $fullPath.Substring($rootPath.Length).TrimStart(
            [IO.Path]::DirectorySeparatorChar,
            [IO.Path]::AltDirectorySeparatorChar)
        if ([string]::IsNullOrWhiteSpace($relativePath) -or
            [IO.Path]::IsPathRooted($relativePath) -or
            $relativePath -match '(^|[\\/])\.\.([\\/]|$)') {
            throw "$Label contains an unsafe relative path: $relativePath"
        }

        $key = $relativePath.Replace('/', '\')
        if ($map.ContainsKey($key)) {
            throw "$Label contains a duplicate relative path: $key"
        }
        $map[$key] = $fullPath
    }

    return $map
}

function Test-FilesEqual {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    if (-not (Test-Path -LiteralPath $Destination -PathType Leaf)) {
        return $false
    }
    if ((Get-Item -LiteralPath $Source).Length -ne (Get-Item -LiteralPath $Destination).Length) {
        return $false
    }

    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Source).Hash -eq
        (Get-FileHash -Algorithm SHA256 -LiteralPath $Destination).Hash
}

function Add-Operation {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][Collections.Generic.List[object]]$Operations,
        [Parameter(Mandatory = $true)][string]$Action,
        [string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination,
        [Parameter(Mandatory = $true)][string]$Reason
    )

    $Operations.Add([pscustomobject]@{
        action = $Action
        source = $Source
        destination = $Destination
        reason = $Reason
    }) | Out-Null
}

function Ensure-Directory {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (Test-Path -LiteralPath $Path -PathType Container) {
        return
    }
    if (-not $effectiveDryRun -and $PSCmdlet.ShouldProcess($Path, 'Create directory')) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

$sourceRoot = Resolve-ExistingDirectory -Path $BuildPath -Label 'WebGL build'
$pagesRoot = Resolve-ExistingDirectory -Path $PagesPath -Label 'Pages repository'
if ((Test-IsSameOrChildPath -Candidate $sourceRoot -Root $pagesRoot) -or
    (Test-IsSameOrChildPath -Candidate $pagesRoot -Root $sourceRoot)) {
    throw "WebGL build and Pages repository must be separate directories: $sourceRoot / $pagesRoot"
}

Assert-NoReparsePoints -Root $sourceRoot -Label 'WebGL build'
Assert-NoReparsePoints -Root $pagesRoot -Label 'Pages repository' -RootOnly

$packagePath = Join-Path $pagesRoot 'package.json'
if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
    throw "Pages package.json is missing: $packagePath"
}
if (((Get-Item -LiteralPath $packagePath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw "Pages package.json must not be a symbolic link: $packagePath"
}
$package = Get-Content -Raw -LiteralPath $packagePath | ConvertFrom-Json
if ($package.name -ne 'cho-siren-preview') {
    throw "Refusing to publish into an unexpected repository (package name: $($package.name))."
}

$validatorPath = Join-Path $PSScriptRoot 'Test-WebGLDeliverable.ps1'
if (-not (Test-Path -LiteralPath $validatorPath -PathType Leaf)) {
    throw "WebGL validator is missing: $validatorPath"
}
$validationJson = (& $validatorPath -BuildPath $sourceRoot | Out-String)
$validation = $validationJson | ConvertFrom-Json
if (-not $validation.success) {
    throw 'WebGL source validation did not report success.'
}

$sourceIndex = Join-Path $sourceRoot 'index.html'
$sourceMarker = Join-Path $sourceRoot '.nojekyll'
$sourceBuild = Join-Path $sourceRoot 'Build'
$sourceStreaming = Join-Path $sourceRoot 'StreamingAssets'
$pagesIndex = Join-Path $pagesRoot 'index.html'
$pagesMarker = Join-Path $pagesRoot '.nojekyll'
$pagesBuild = Join-Path $pagesRoot 'Build'
$pagesStreaming = Join-Path $pagesRoot 'StreamingAssets'

if ((Test-Path -LiteralPath $pagesMarker) -and -not (Test-Path -LiteralPath $pagesMarker -PathType Leaf)) {
    throw "Pages .nojekyll exists but is not a file: $pagesMarker"
}
foreach ($filePath in @($pagesIndex, $pagesMarker)) {
    if ((Test-Path -LiteralPath $filePath) -and
        (((Get-Item -LiteralPath $filePath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0)) {
        throw "Pages publication file must not be a symbolic link: $filePath"
    }
}
if ((Test-Path -LiteralPath $pagesBuild) -and -not (Test-Path -LiteralPath $pagesBuild -PathType Container)) {
    throw "Pages Build exists but is not a directory: $pagesBuild"
}
if ((Test-Path -LiteralPath $pagesStreaming) -and -not (Test-Path -LiteralPath $pagesStreaming -PathType Container)) {
    throw "Pages StreamingAssets exists but is not a directory: $pagesStreaming"
}
if (Test-Path -LiteralPath $pagesBuild -PathType Container) {
    Assert-NoReparsePoints -Root $pagesBuild -Label 'Pages Build'
}
if (Test-Path -LiteralPath $pagesStreaming -PathType Container) {
    Assert-NoReparsePoints -Root $pagesStreaming -Label 'Pages StreamingAssets'
}

$sourceAssets = @(Get-IndexBuildAssets -IndexPath $sourceIndex -Label 'WebGL build')
$previousAssets = @(Get-IndexBuildAssets -IndexPath $pagesIndex -Label 'Pages repository')

foreach ($asset in $sourceAssets) {
    $sourceAssetPath = Assert-ChildPath -Path (Join-Path $sourceBuild $asset) -Root $sourceBuild -Label 'WebGL build asset'
    if (-not (Test-Path -LiteralPath $sourceAssetPath -PathType Leaf)) {
        throw "Referenced WebGL build asset is missing: $sourceAssetPath"
    }
}
foreach ($asset in $previousAssets) {
    $previousAssetPath = Assert-ChildPath -Path (Join-Path $pagesBuild $asset) -Root $pagesBuild -Label 'Pages build asset'
    if (-not (Test-Path -LiteralPath $previousAssetPath -PathType Leaf)) {
        throw "The current Pages index references a missing asset: $previousAssetPath"
    }
}

$pagesBuildFiles = @()
if (Test-Path -LiteralPath $pagesBuild -PathType Container) {
    $pagesBuildEntries = @(Get-ChildItem -LiteralPath $pagesBuild -Force)
    $unexpectedBuildEntries = @($pagesBuildEntries | Where-Object { $_.PSIsContainer -or $_.Name -notmatch $unityAssetPattern })
    if ($unexpectedBuildEntries.Count -gt 0) {
        throw "Pages Build contains non-Unity entries; refusing to alter them: $($unexpectedBuildEntries.Name -join ', ')"
    }
    $pagesBuildFiles = @($pagesBuildEntries | Where-Object { -not $_.PSIsContainer })
}

$sourceStreamingFiles = Get-SafeRelativeFileMap -Root $sourceStreaming -Label 'WebGL StreamingAssets'
$pagesStreamingFiles = Get-SafeRelativeFileMap -Root $pagesStreaming -Label 'Pages StreamingAssets'
$operations = New-Object 'Collections.Generic.List[object]'

if (-not (Test-Path -LiteralPath $pagesBuild -PathType Container)) {
    Add-Operation -Operations $operations -Action 'create-directory' -Destination $pagesBuild -Reason 'Unity Build destination'
}
if (-not (Test-Path -LiteralPath $pagesStreaming -PathType Container)) {
    Add-Operation -Operations $operations -Action 'create-directory' -Destination $pagesStreaming -Reason 'Unity StreamingAssets destination'
}

foreach ($asset in $sourceAssets) {
    $sourceAssetPath = Assert-ChildPath -Path (Join-Path $sourceBuild $asset) -Root $sourceBuild -Label 'WebGL build asset'
    $destinationAssetPath = Assert-ChildPath -Path (Join-Path $pagesBuild $asset) -Root $pagesBuild -Label 'Pages build asset'
    if (-not (Test-FilesEqual -Source $sourceAssetPath -Destination $destinationAssetPath)) {
        Add-Operation -Operations $operations -Action 'copy-file' -Source $sourceAssetPath -Destination $destinationAssetPath -Reason 'New index.html reference'
    }
}

foreach ($relativePath in @($sourceStreamingFiles.Keys | Sort-Object)) {
    $sourceFile = $sourceStreamingFiles[$relativePath]
    $destinationFile = Assert-ChildPath -Path (Join-Path $pagesStreaming $relativePath) -Root $pagesStreaming -Label 'Pages StreamingAssets file'
    if (Test-Path -LiteralPath $destinationFile -PathType Container) {
        throw "Pages StreamingAssets has a directory where the build requires a file: $destinationFile"
    }
    $candidateParent = Split-Path -Parent $destinationFile
    while (-not $candidateParent.Equals($pagesStreaming, $pathComparison)) {
        Assert-ChildPath -Path $candidateParent -Root $pagesStreaming -Label 'Pages StreamingAssets parent' | Out-Null
        if ((Test-Path -LiteralPath $candidateParent) -and -not (Test-Path -LiteralPath $candidateParent -PathType Container)) {
            throw "Pages StreamingAssets has a file where the build requires a directory: $candidateParent"
        }
        $candidateParent = Split-Path -Parent $candidateParent
    }
    if (-not (Test-FilesEqual -Source $sourceFile -Destination $destinationFile)) {
        Add-Operation -Operations $operations -Action 'copy-file' -Source $sourceFile -Destination $destinationFile -Reason 'Synchronize StreamingAssets'
    }
}

if (-not (Test-FilesEqual -Source $sourceMarker -Destination $pagesMarker)) {
    Add-Operation -Operations $operations -Action 'copy-file' -Source $sourceMarker -Destination $pagesMarker -Reason 'GitHub Pages marker'
}
if (-not (Test-FilesEqual -Source $sourceIndex -Destination $pagesIndex)) {
    Add-Operation -Operations $operations -Action 'copy-file' -Source $sourceIndex -Destination $pagesIndex -Reason 'Publish index last after its dependencies'
}

$staleBuildFiles = @($pagesBuildFiles | Where-Object { $_.Name -notin $sourceAssets })
foreach ($item in $staleBuildFiles) {
    $stalePath = Assert-ChildPath -Path $item.FullName -Root $pagesBuild -Label 'Stale Pages build asset'
    $reason = 'Unreferenced hashed Unity build asset'
    if ($item.Name -in $previousAssets) {
        $reason = 'Previous index.html reference no longer used'
    }
    Add-Operation -Operations $operations -Action 'remove-file' -Destination $stalePath -Reason $reason
}

foreach ($relativePath in @($pagesStreamingFiles.Keys | Sort-Object)) {
    if (-not $sourceStreamingFiles.ContainsKey($relativePath)) {
        $stalePath = Assert-ChildPath -Path $pagesStreamingFiles[$relativePath] -Root $pagesStreaming -Label 'Stale Pages StreamingAssets file'
        Add-Operation -Operations $operations -Action 'remove-file' -Destination $stalePath -Reason 'Absent from new WebGL StreamingAssets'
    }
}

foreach ($operation in $operations) {
    switch ($operation.action) {
        'create-directory' {
            Ensure-Directory -Path $operation.destination
        }
        'copy-file' {
            $parentDirectory = Split-Path -Parent $operation.destination
            Ensure-Directory -Path $parentDirectory
            if (-not $effectiveDryRun -and $PSCmdlet.ShouldProcess($operation.destination, "Copy $($operation.source)")) {
                Copy-Item -LiteralPath $operation.source -Destination $operation.destination -Force
            }
        }
        'remove-file' {
            if (-not $effectiveDryRun -and $PSCmdlet.ShouldProcess($operation.destination, 'Remove stale published file')) {
                Remove-Item -LiteralPath $operation.destination -Force
            }
        }
        default {
            throw "Unknown publish operation: $($operation.action)"
        }
    }
}

if (-not $effectiveDryRun -and (Test-Path -LiteralPath $pagesStreaming -PathType Container)) {
    $streamingDirectories = @(Get-ChildItem -LiteralPath $pagesStreaming -Directory -Force -Recurse |
        Sort-Object { $_.FullName.Length } -Descending)
    foreach ($directory in $streamingDirectories) {
        $directoryPath = Assert-ChildPath -Path $directory.FullName -Root $pagesStreaming -Label 'Pages StreamingAssets directory'
        if (@(Get-ChildItem -LiteralPath $directoryPath -Force).Count -eq 0 -and
            $PSCmdlet.ShouldProcess($directoryPath, 'Remove empty StreamingAssets directory')) {
            Remove-Item -LiteralPath $directoryPath -Force
        }
    }
}

if (-not $effectiveDryRun) {
    if (-not (Test-FilesEqual -Source $sourceIndex -Destination $pagesIndex)) {
        throw 'Published index.html hash does not match the WebGL build.'
    }
    if (-not (Test-Path -LiteralPath $pagesMarker -PathType Leaf) -or
        -not (Test-FilesEqual -Source $sourceMarker -Destination $pagesMarker)) {
        throw 'Published .nojekyll does not match the WebGL build.'
    }

    $publishedAssets = @(Get-IndexBuildAssets -IndexPath $pagesIndex -Label 'Published Pages')
    if (@(Compare-Object -ReferenceObject ($sourceAssets | Sort-Object) -DifferenceObject ($publishedAssets | Sort-Object)).Count -ne 0) {
        throw 'Published index.html does not match the new WebGL asset references.'
    }

    $publishedBuildEntries = @(Get-ChildItem -LiteralPath $pagesBuild -Force)
    if (@($publishedBuildEntries | Where-Object { $_.PSIsContainer }).Count -gt 0) {
        throw 'Published Build directory contains an unexpected subdirectory.'
    }
    $publishedBuildFiles = @($publishedBuildEntries | Where-Object { -not $_.PSIsContainer })
    if (@(Compare-Object -ReferenceObject ($sourceAssets | Sort-Object) -DifferenceObject ($publishedBuildFiles.Name | Sort-Object)).Count -ne 0) {
        throw 'Published Build directory contains a file set different from the new index.html references.'
    }
    foreach ($asset in $sourceAssets) {
        $sourceFile = Join-Path $sourceBuild $asset
        $publishedFile = Join-Path $pagesBuild $asset
        if (-not (Test-FilesEqual -Source $sourceFile -Destination $publishedFile)) {
            throw "Published Unity asset hash mismatch: $asset"
        }
    }

    $publishedStreamingFiles = Get-SafeRelativeFileMap -Root $pagesStreaming -Label 'Published Pages StreamingAssets'
    if (@(Compare-Object -ReferenceObject ($sourceStreamingFiles.Keys | Sort-Object) -DifferenceObject ($publishedStreamingFiles.Keys | Sort-Object)).Count -ne 0) {
        throw 'Published StreamingAssets file set differs from the WebGL build.'
    }
    foreach ($relativePath in $sourceStreamingFiles.Keys) {
        if (-not (Test-FilesEqual -Source $sourceStreamingFiles[$relativePath] -Destination $publishedStreamingFiles[$relativePath])) {
            throw "Published StreamingAssets hash mismatch: $relativePath"
        }
    }
}

[pscustomobject]@{
    success = $true
    dryRun = $effectiveDryRun
    dryRunRequested = [bool]$DryRun
    whatIfRequested = [bool]$WhatIfPreference
    source = $sourceRoot
    destination = $pagesRoot
    newBuildAssets = $sourceAssets
    previousBuildAssets = $previousAssets
    staleBuildAssets = @($staleBuildFiles.Name)
    streamingAssets = $sourceStreamingFiles.Count
    operations = $operations.ToArray()
} | ConvertTo-Json -Depth 5
