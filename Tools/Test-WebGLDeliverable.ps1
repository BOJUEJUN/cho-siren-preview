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
} | ConvertTo-Json -Depth 4
