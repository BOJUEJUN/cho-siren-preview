param(
    [string]$BuildPath = (Join-Path $PSScriptRoot '..\Builds\WebGL')
)

$ErrorActionPreference = 'Stop'
$resolvedBuildPath = [IO.Path]::GetFullPath($BuildPath)
$indexPath = Join-Path $resolvedBuildPath 'index.html'
$pagesMarkerPath = Join-Path $resolvedBuildPath '.nojekyll'

if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf)) {
    throw "Missing WebGL entry point: $indexPath"
}
if (-not (Test-Path -LiteralPath $pagesMarkerPath -PathType Leaf)) {
    throw "Missing GitHub Pages marker: $pagesMarkerPath"
}

$html = Get-Content -Raw -LiteralPath $indexPath
if ($html -notmatch '<canvas[^>]+width="720"[^>]+height="1536"') {
    throw 'WebGL canvas is not built at the required 720 x 1536 portrait size.'
}
if ($html -notmatch '--portrait-ratio:\s*720\s*/\s*1536') {
    throw 'WebGL page shell does not match the 720 x 1536 canvas ratio.'
}

$lobbyVideoPath = Join-Path $resolvedBuildPath 'StreamingAssets\Lobby\lobby-loop.mp4'
if (-not (Test-Path -LiteralPath $lobbyVideoPath -PathType Leaf)) {
    throw "Missing lobby loop video: $lobbyVideoPath"
}
if ((Get-Item -LiteralPath $lobbyVideoPath).Length -lt 1024) {
    throw 'Lobby loop video is unexpectedly empty.'
}
if ($html -notmatch 'new URL\("Build/", pageUrl\)') {
    throw 'WebGL assets are not resolved relative to the GitHub Pages project path.'
}
if ($html -notmatch 'Cache-Control[^>]+no-cache') {
    throw 'The mutable index.html does not carry the cache-bypass metadata.'
}

$assetPattern = 'buildAssetUrl\("(?<file>[^\"]+\.(?:data\.unityweb|framework\.js\.unityweb|wasm\.unityweb|loader\.js))"\)'
$matches = [regex]::Matches($html, $assetPattern)
if ($matches.Count -ne 4) {
    throw "Expected four WebGL build asset references, found $($matches.Count)."
}

$githubFileLimit = 100MB
$recommendedDataLimit = 90MB
$assets = foreach ($match in $matches) {
    $fileName = $match.Groups['file'].Value
    if ($fileName -notmatch '^[0-9a-f]{32}\.') {
        throw "WebGL asset is not content-hash named: $fileName"
    }

    $assetPath = Join-Path (Join-Path $resolvedBuildPath 'Build') $fileName
    if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
        throw "Referenced WebGL asset is missing: $assetPath"
    }

    $item = Get-Item -LiteralPath $assetPath
    if ($item.Length -ge $githubFileLimit) {
        throw "WebGL asset exceeds GitHub's 100 MiB file limit: $fileName ($($item.Length) bytes)."
    }
    if ($fileName -match '\.data\.unityweb$' -and $item.Length -gt $recommendedDataLimit) {
        Write-Warning "WebGL data is publishable but above the preferred 90 MiB budget: $($item.Length) bytes."
    }
    [pscustomobject]@{
        file = $fileName
        bytes = $item.Length
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $assetPath).Hash.ToLowerInvariant()
    }
}

$referencedNames = @($assets | ForEach-Object { $_.file })
$unexpectedBuildEntries = @(Get-ChildItem -LiteralPath (Join-Path $resolvedBuildPath 'Build') -Force |
    Where-Object { $_.PSIsContainer -or $_.Name -notin $referencedNames })
if ($unexpectedBuildEntries.Count -gt 0) {
    throw "WebGL Build contains entries not referenced by index.html: $($unexpectedBuildEntries.Name -join ', ')"
}

# Keep the original 25 streamed assets plus exactly the three complete Chapter 01 paintings.
$storyManifestPath = Join-Path $resolvedBuildPath 'StreamingAssets/Story1019/manifest.json'
if (-not (Test-Path -LiteralPath $storyManifestPath -PathType Leaf)) {
    throw "Missing Story1019 original-image manifest: $storyManifestPath"
}
$storyManifest = Get-Content -Raw -LiteralPath $storyManifestPath | ConvertFrom-Json
$storyEntries = @($storyManifest.entries)
if ($storyEntries.Count -ne 28) {
    throw "Expected exactly 28 Story1019 originals, found $($storyEntries.Count)."
}
$requiredStoryKeys = @(
    foreach ($chapter in 1..2) { foreach ($stage in 1..10) { "Art/Story/Scenes1019/stage-$chapter-$stage" } }
    foreach ($id in @('xingli', 'feiyin', 'wubai', 'yeying')) { "Art/Story/Portraits1019/$id" }
    'Art/Story/Transitions1019/xingli-hand-reach-cg'
    foreach ($name in @('chapter-01-rehearsal-solo', 'chapter-01-rehearsal-arrival', 'chapter-01-alley-outro')) {
        "Art/Story/Scenes1019/$name"
    }
)
$storyKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$storyArt = foreach ($entry in $storyEntries) {
    if ($entry.key -isnot [string] -or $entry.key -cnotin $requiredStoryKeys -or -not $storyKeys.Add($entry.key)) {
        throw "Unexpected or duplicate Story1019 key: $($entry.key)"
    }
    if ($entry.file -isnot [string] -or
        $entry.file -cnotmatch '^Story1019/[0-9a-f]{16}-(?:scenes1019|portraits1019|transitions1019)-[a-z0-9-]+\.(?:png|jpe?g)$') {
        throw "Unsafe Story1019 original path: $($entry.file)"
    }
    $storyPath = Join-Path (Join-Path $resolvedBuildPath 'StreamingAssets') $entry.file
    if (-not (Test-Path -LiteralPath $storyPath -PathType Leaf)) {
        throw "Missing Story1019 original: $storyPath"
    }
    $storyItem = Get-Item -LiteralPath $storyPath
    $storyHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $storyPath).Hash.ToLowerInvariant()
    if ($entry.sha256 -cne $storyHash -or $entry.bytes -ne $storyItem.Length -or
        -not $entry.file.StartsWith('Story1019/' + $storyHash.Substring(0, 16) + '-', [StringComparison]::Ordinal)) {
        throw "Story1019 original bytes/SHA differ: $($entry.key)"
    }
    [pscustomobject]@{ key = $entry.key; file = $entry.file; bytes = $storyItem.Length; sha256 = $storyHash }
}
if (-not $storyKeys.SetEquals([string[]]$requiredStoryKeys)) {
    throw 'Story1019 is missing a required original.'
}

[pscustomobject]@{
    success = $true
    buildPath = $resolvedBuildPath
    canvas = '720x1536'
    githubPagesMarker = $pagesMarkerPath
    githubPerFileLimitBytes = $githubFileLimit
    preferredDataLimitBytes = $recommendedDataLimit
    lobbyVideo = [pscustomobject]@{
        file = 'StreamingAssets/Lobby/lobby-loop.mp4'
        bytes = (Get-Item -LiteralPath $lobbyVideoPath).Length
        sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $lobbyVideoPath).Hash.ToLowerInvariant()
    }
    assets = @($assets)
    storyArt = @($storyArt)
} | ConvertTo-Json -Depth 4
