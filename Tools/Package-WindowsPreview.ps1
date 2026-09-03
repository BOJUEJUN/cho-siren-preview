param(
    [string]$Version = '0.3.0',
    [string]$BuildPath = (Join-Path $PSScriptRoot '..\Builds\Windows'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\Releases')
)

$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$buildRoot = [IO.Path]::GetFullPath($BuildPath)
$releaseRoot = [IO.Path]::GetFullPath($OutputDirectory)
$expectedBuildRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'Builds\Windows'))
$expectedReleaseRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'Releases'))

if ($buildRoot -ne $expectedBuildRoot) {
    throw "Unexpected Windows build path: $buildRoot"
}
if ($releaseRoot -ne $expectedReleaseRoot) {
    throw "Unexpected release output path: $releaseRoot"
}

$required = @(
    'CHO-SIREN.exe',
    'CHO-SIREN_Data',
    'MonoBleedingEdge',
    'UnityPlayer.dll'
)
foreach ($name in $required) {
    if (-not (Test-Path -LiteralPath (Join-Path $buildRoot $name))) {
        throw "Windows build is incomplete; missing $name"
    }
}

$safeVersion = $Version -replace '[^0-9A-Za-z._-]', '-'
$packageName = "CHO-SIREN-Windows-Preview-$safeVersion"
$stagingPath = Join-Path $releaseRoot $packageName
$archivePath = Join-Path $releaseRoot ($packageName + '.zip')

New-Item -ItemType Directory -Force -Path $releaseRoot | Out-Null
if (Test-Path -LiteralPath $stagingPath) {
    if (-not ([IO.Path]::GetFullPath($stagingPath).StartsWith($expectedReleaseRoot + [IO.Path]::DirectorySeparatorChar))) {
        throw "Refusing to replace staging path outside Releases: $stagingPath"
    }
    Remove-Item -LiteralPath $stagingPath -Recurse -Force
}
if (Test-Path -LiteralPath $archivePath) {
    if (-not ([IO.Path]::GetFullPath($archivePath).StartsWith($expectedReleaseRoot + [IO.Path]::DirectorySeparatorChar))) {
        throw "Refusing to replace archive outside Releases: $archivePath"
    }
    Remove-Item -LiteralPath $archivePath -Force
}

# Unity emits debug-only folders next to the player (Burst/IL2CPP symbols). They are
# useless to players, leak build internals and only inflate the archive.
$doNotShipPatterns = @(
    '*_BackUpThisFolder_ButDontShipItWithYourGame',
    '*_BurstDebugInformation_DoNotShip'
)

# Builds\Windows may still carry an older hand-written 预览说明.txt; its content is already
# covered by the 使用说明.txt generated from WindowsPreview-README.txt, so it is not copied.
$duplicateReadmeNames = @(
    '预览说明.txt'
)

# This shortcut only works while Builds\Windows is inside the source checkout.
# A standalone player archive intentionally contains no editable Unity project.
$developerOnlyNames = @(
    '打开Unity编辑器.cmd'
)

$projectVersionFile = Join-Path $projectRoot 'ProjectSettings\ProjectSettings.asset'
if (Test-Path -LiteralPath $projectVersionFile) {
    $bundleLine = Select-String -LiteralPath $projectVersionFile -Pattern '^\s*bundleVersion:\s*(\S+)' | Select-Object -First 1
    if ($bundleLine -and $bundleLine.Matches[0].Groups[1].Value -ne $Version) {
        $bundleVersion = $bundleLine.Matches[0].Groups[1].Value
        Write-Warning "PlayerSettings.bundleVersion is $bundleVersion but the package is labelled $Version; Application.version inside the game will not match this release."
    }
}

New-Item -ItemType Directory -Force -Path $stagingPath | Out-Null
Get-ChildItem -LiteralPath $buildRoot -Force | Where-Object {
    $item = $_
    -not ($doNotShipPatterns | Where-Object { $item.Name -like $_ }) -and
    -not ($duplicateReadmeNames -contains $item.Name) -and
    -not ($developerOnlyNames -contains $item.Name)
} | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $stagingPath $_.Name) -Recurse -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'WindowsPreview-README.txt') `
    -Destination (Join-Path $stagingPath '使用说明.txt') -Force

$files = Get-ChildItem -LiteralPath $stagingPath -Recurse -File
$manifest = [ordered]@{
    product = 'CHO-SIREN 幻域魅声'
    version = $Version
    target = 'Windows 64 位'
    createdAt = (Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz')
    entry = 'CHO-SIREN.exe'
    fileCount = $files.Count
    uncompressedBytes = ($files | Measure-Object -Property Length -Sum).Sum
    executableSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $stagingPath 'CHO-SIREN.exe')).Hash.ToLowerInvariant()
    onlinePreview = 'https://bojuejun.github.io/cho-siren-preview/'
    gitIgnoredJunkExcluded = $true
    excludedPatterns = @($doNotShipPatterns + $duplicateReadmeNames + $developerOnlyNames)
}
$manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $stagingPath '版本信息.json') -Encoding utf8

Compress-Archive -Path $stagingPath -DestinationPath $archivePath -CompressionLevel Optimal

$archive = Get-Item -LiteralPath $archivePath
[pscustomobject]@{
    success = $true
    package = $archive.FullName
    bytes = $archive.Length
    sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $archive.FullName).Hash.ToLowerInvariant()
    staging = $stagingPath
    fileCount = (Get-ChildItem -LiteralPath $stagingPath -Recurse -File).Count
} | ConvertTo-Json -Depth 4
