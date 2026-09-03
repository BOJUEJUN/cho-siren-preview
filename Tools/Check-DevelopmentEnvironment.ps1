[CmdletBinding()]
param(
    [string]$UnityExe = ''
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$requiredUnityVersion = '6000.6.0f1'
$requiredUnityRevision = 'f7f8ed4d1e24'
$failures = New-Object 'System.Collections.Generic.List[string]'
$warnings = New-Object 'System.Collections.Generic.List[string]'
$gitRepositoryAvailable = $false
$trackedPathSet = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)

function Write-CheckResult {
    param(
        [string]$Level,
        [string]$Message
    )

    $colour = switch ($Level) {
        'PASS' { 'Green' }
        'WARN' { 'Yellow' }
        default { 'Red' }
    }
    Write-Host ("[{0}] {1}" -f $Level, $Message) -ForegroundColor $colour
}

function Add-Failure {
    param([string]$Message)
    $script:failures.Add($Message)
    Write-CheckResult 'FAIL' $Message
}

function Add-Warning {
    param([string]$Message)
    $script:warnings.Add($Message)
    Write-CheckResult 'WARN' $Message
}

Write-Host 'CHO-SIREN development environment check (read-only)'
Write-Host ("Project: {0}" -f $projectRoot)

$versionFile = Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf)) {
    Add-Failure 'ProjectSettings/ProjectVersion.txt is missing.'
    $expectedUnityVersion = $requiredUnityVersion
}
else {
    $versionText = Get-Content -Raw -LiteralPath $versionFile
    $versionMatch = [regex]::Match($versionText, '(?m)^m_EditorVersion:\s*(\S+)')
    $revisionMatch = [regex]::Match($versionText, '(?m)^m_EditorVersionWithRevision:\s*(.+)$')
    if (-not $versionMatch.Success) {
        Add-Failure 'Unity version could not be read from ProjectVersion.txt.'
        $expectedUnityVersion = $requiredUnityVersion
    }
    else {
        $expectedUnityVersion = $versionMatch.Groups[1].Value
        if ($expectedUnityVersion -ne $requiredUnityVersion) {
            Add-Failure ("Expected Unity {0} but the project declares {1}." -f $requiredUnityVersion, $expectedUnityVersion)
        }
        elseif (-not $revisionMatch.Success -or
            $revisionMatch.Groups[1].Value.IndexOf($requiredUnityRevision, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            Add-Failure ("Expected Unity revision {0}; ProjectVersion.txt does not declare it." -f $requiredUnityRevision)
        }
        else {
            $revision = if ($revisionMatch.Success) { $revisionMatch.Groups[1].Value.Trim() } else { $expectedUnityVersion }
            Write-CheckResult 'PASS' ("Project version: {0}" -f $revision)
        }
    }
}

$unityCandidates = New-Object 'System.Collections.Generic.List[string]'
if (-not [string]::IsNullOrWhiteSpace($UnityExe)) { $unityCandidates.Add($UnityExe) }
if (-not [string]::IsNullOrWhiteSpace($env:UNITY_EDITOR)) { $unityCandidates.Add($env:UNITY_EDITOR) }
if (-not [string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
    $unityCandidates.Add((Join-Path $env:USERPROFILE ("Unity\Hub\Editor\{0}\Editor\Unity.exe" -f $expectedUnityVersion)))
}
if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
    $unityCandidates.Add((Join-Path $env:ProgramFiles ("Unity\Hub\Editor\{0}\Editor\Unity.exe" -f $expectedUnityVersion)))
}

$resolvedUnity = $unityCandidates | Where-Object {
    -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path -LiteralPath $_ -PathType Leaf)
} | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($resolvedUnity)) {
    Add-Failure ("Unity {0} was not found. Pass -UnityExe or set UNITY_EDITOR." -f $expectedUnityVersion)
}
else {
    $unityItem = Get-Item -LiteralPath $resolvedUnity
    $unityProductVersion = $unityItem.VersionInfo.ProductVersion
    if ([string]::IsNullOrWhiteSpace($unityProductVersion) -or
        -not $unityProductVersion.StartsWith($expectedUnityVersion + '_', [StringComparison]::OrdinalIgnoreCase) -or
        $unityProductVersion.IndexOf($requiredUnityRevision, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        Add-Failure ("Unity executable does not match {0} revision {1}: {2}" -f $expectedUnityVersion, $requiredUnityRevision, $unityProductVersion)
    }
    else {
        Write-CheckResult 'PASS' ("Unity executable: {0} ({1})" -f $unityItem.FullName, $unityProductVersion)
    }
}

$git = Get-Command git -ErrorAction SilentlyContinue
if ($null -eq $git) {
    Add-Failure 'Git is not available on PATH.'
}
else {
    $inside = & $git.Source -C $projectRoot rev-parse --is-inside-work-tree 2>$null
    if ($LASTEXITCODE -ne 0 -or $inside -ne 'true') {
        Add-Failure 'Project directory is not a Git working tree.'
    }
    else {
        $gitRepositoryAvailable = $true
        Write-CheckResult 'PASS' 'Git working tree detected.'

        $remoteNames = @(& $git.Source -C $projectRoot remote)
        if ($remoteNames -notcontains 'origin') {
            Add-Failure 'No origin remote is configured; another computer cannot clone this source yet.'
        }
        else {
            $origin = & $git.Source -C $projectRoot remote get-url origin
            Write-CheckResult 'PASS' ("origin: {0}" -f $origin)
        }

        $lfsVersion = & $git.Source lfs version 2>$null
        $attributesPath = Join-Path $projectRoot '.gitattributes'
        $hasLfsRules = $false
        if (Test-Path -LiteralPath $attributesPath -PathType Leaf) {
            $attributeText = Get-Content -Raw -LiteralPath $attributesPath
            $hasLfsRules = [regex]::IsMatch($attributeText, '(?m)^[^#\r\n]*\bfilter=lfs\b')
        }
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($lfsVersion | Out-String))) {
            if ($hasLfsRules) { Add-Failure 'Git LFS rules are active, but git-lfs is unavailable.' }
            else { Add-Warning 'git-lfs is unavailable. Install it before adding source files >= 50 MiB.' }
        }
        else {
            $lfsCount = @(& $git.Source -C $projectRoot lfs ls-files 2>$null).Count
            Write-CheckResult 'PASS' ("Git LFS available; tracked LFS objects: {0}." -f $lfsCount)
        }

        foreach ($trackedPath in @(& $git.Source -c core.quotepath=false -C $projectRoot ls-files)) {
            [void]$trackedPathSet.Add($trackedPath.Replace('\', '/'))
        }
    }
}

$requiredFiles = @(
    'Packages\manifest.json',
    'Packages\packages-lock.json',
    'ProjectSettings\EditorBuildSettings.asset',
    'Assets\Scenes\Main.unity',
    'Assets\Editor\ChoSirenBuild.cs',
    'Assets\Resources\Data\tactics.json',
    'Assets\Resources\Art\LevelMapUser\chapter-01-city-clean-ai-v1.png',
    'Assets\Resources\Art\BattleUser\boss-throne-user-v1.png',
    'Assets\Resources\Art\BattleUser\dice-face-1-user-v1.png',
    'Assets\Resources\Art\BattleUser\dice-face-2-user-v1.png',
    'Assets\Resources\Art\BattleUser\dice-face-3-user-v1.png',
    'Assets\Resources\Art\BattleUser\dice-face-4-user-v1.png',
    'Assets\Resources\Art\BattleUser\dice-face-5-user-v1.png',
    'Assets\Resources\Art\BattleUser\dice-face-6-user-v1.png',
    'Assets\Resources\Fonts\NotoSansSC-Subset.otf',
    'SourceAssets\Fonts\NotoSansSC-Regular.otf',
    'Assets\StreamingAssets\Lobby\lobby-loop.mp4',
    'Assets\Resources\Art\LobbyAI\lobby-stage-hotspot-v2.png',
    'Assets\Resources\Art\LobbyAI\lobby-story-hotspot-v2.png',
    'Assets\Resources\Art\LobbyAI\lobby-task-hotspot-v2.png',
    'Assets\Resources\Art\LobbyAI\lobby-perform-cta-v2.png',
    'Assets\Resources\Art\GachaAI\gacha-stage-bg-ai-v1-20260903.png',
    'Assets\Resources\Art\GachaAI\gacha-emblem-debut-ai-v1-20260903.png',
    'Assets\Resources\Art\GachaAI\gacha-emblem-standard-ai-v1-20260903.png',
    'Assets\Resources\Art\GachaAI\gacha-emblem-costume-ai-v1-20260903.png',
    'Assets\Resources\Art\GachaAI\gacha-ten-pull-frame-ai-v1-20260903.png',
    'Assets\Resources\Art\TaskAI\task-board-bg-ai-v1-20260903.png',
    'Assets\Resources\Art\BattleAI\battle-stage-hud-v1.png',
    'Assets\Resources\Art\BattleAI\dice-frame-v1.png',
    'Assets\Resources\Art\BattleAI\reroll-ring-v1.png',
    'Assets\Resources\Art\BattleAI\member-skill-frame-v1.png',
    'Assets\Resources\Art\BattleAI\skill-button-frame-v1.png',
    'Assets\Resources\Art\BattleAI\battle-hit-slash-ai-v1.png',
    'Assets\Resources\Art\BattleAI\battle-heart-impact-ai-v1.png',
    'Assets\Resources\Art\BattleAI\battle-charge-aura-ai-v1.png',
    'Assets\Resources\Art\BattleAI\battle-low-health-frame-ai-v1.png',
    'Assets\Resources\Art\LevelMapAI\stage-node-frame-ai-v1.png',
    'Assets\Resources\Art\LevelMapAI\chapter-progress-ring-ai-v1.png',
    'Assets\Resources\Art\LevelMapAI\chapter-reward-chest-ai-v1.png',
    'Assets\Resources\Art\LevelMapAI\chapter-action-frame-ai-v1.png',
    'Assets\Resources\Art\AccessoryAI\accessory-calm-bg-ai-v2-20260903.png',
    'Assets\Scripts\ButtonInteractionFeedback.cs',
    'Assets\Scripts\ButtonInteractionFeedbackInstaller.cs',
    'Tools\Run-UnitySafe.ps1',
    'Tools\Open-UnityEditorSafe.ps1',
    'Tools\Test-WebGLDeliverable.ps1',
    'Tools\Publish-WebGLToPages.ps1',
    'Tools\Process-AILevelMapAtlas.ps1',
    'Tools\Process-AIBattleVfxAtlas.ps1',
    'Docs\CHAPTER-01-PROGRESSION.md',
    'Docs\BATTLE-VFX-ANIMATION.md'
)

foreach ($relativePath in $requiredFiles) {
    $absolutePath = Join-Path $projectRoot $relativePath
    if (-not (Test-Path -LiteralPath $absolutePath -PathType Leaf)) {
        Add-Failure ("Required file is missing: {0}" -f $relativePath)
        continue
    }

    if ($gitRepositoryAvailable) {
        $gitPath = $relativePath.Replace('\', '/')
        if (-not $trackedPathSet.Contains($gitPath)) {
            Add-Failure ("Required file is not tracked by Git: {0}" -f $relativePath)
            continue
        }
    }
    Write-CheckResult 'PASS' ("Required file: {0}" -f $relativePath)

    if ($relativePath.StartsWith('Assets\', [StringComparison]::OrdinalIgnoreCase) -and
        -not $relativePath.EndsWith('.meta', [StringComparison]::OrdinalIgnoreCase)) {
        $metaPath = $relativePath + '.meta'
        $absoluteMetaPath = Join-Path $projectRoot $metaPath
        if (-not (Test-Path -LiteralPath $absoluteMetaPath -PathType Leaf)) {
            Add-Failure ("Unity asset is missing its meta file: {0}" -f $metaPath)
        }
        elseif ($gitRepositoryAvailable -and -not $trackedPathSet.Contains($metaPath.Replace('\', '/'))) {
            Add-Failure ("Unity meta file is not tracked by Git: {0}" -f $metaPath)
        }
    }
}

foreach ($jsonPath in @('Packages\manifest.json', 'Packages\packages-lock.json', 'Assets\Resources\Data\tactics.json')) {
    $absolutePath = Join-Path $projectRoot $jsonPath
    if (Test-Path -LiteralPath $absolutePath -PathType Leaf) {
        try {
            [IO.File]::ReadAllText($absolutePath, [Text.Encoding]::UTF8) | ConvertFrom-Json | Out-Null
            Write-CheckResult 'PASS' ("Valid JSON: {0}" -f $jsonPath)
        }
        catch {
            Add-Failure ("Invalid JSON: {0} ({1})" -f $jsonPath, $_.Exception.Message)
        }
    }
}

$tacticsPath = Join-Path $projectRoot 'Assets\Resources\Data\tactics.json'
if (Test-Path -LiteralPath $tacticsPath -PathType Leaf) {
    try {
        $tacticsData = [IO.File]::ReadAllText($tacticsPath, [Text.Encoding]::UTF8) | ConvertFrom-Json
        $chapterIds = @($tacticsData.Stages | ForEach-Object { [string]$_.Id } | Where-Object { $_ -like 'stage-1-*' })
        $uniqueChapterIds = @($chapterIds | Sort-Object -Unique)
        $expectedChapterIds = @(1..10 | ForEach-Object { 'stage-1-{0}' -f $_ })
        $missingChapterIds = @($expectedChapterIds | Where-Object { $chapterIds -notcontains $_ })
        $unexpectedChapterIds = @($uniqueChapterIds | Where-Object { $expectedChapterIds -notcontains $_ })
        if ($chapterIds.Count -ne 10 -or $uniqueChapterIds.Count -ne 10 -or
            $missingChapterIds.Count -gt 0 -or $unexpectedChapterIds.Count -gt 0) {
            Add-Failure ("Chapter 1 must contain exactly stage-1-1 through stage-1-10. Found: {0}" -f ($uniqueChapterIds -join ', '))
        }
        else {
            Write-CheckResult 'PASS' 'Chapter 1 contains exactly stage-1-1 through stage-1-10.'
        }
    }
    catch {
        Add-Failure ("Chapter 1 stage validation failed: {0}" -f $_.Exception.Message)
    }
}

$rootLaunchers = @(Get-ChildItem -LiteralPath $projectRoot -File -Filter '*.cmd' -ErrorAction SilentlyContinue | Where-Object {
    (Get-Content -Raw -LiteralPath $_.FullName) -match 'Tools[\\/]+Open-UnityEditorSafe\.ps1'
})
if ($rootLaunchers.Count -eq 0) {
    Add-Failure 'No root .cmd launcher delegates to Tools/Open-UnityEditorSafe.ps1.'
}
elseif ($gitRepositoryAvailable -and -not @($rootLaunchers | Where-Object { $trackedPathSet.Contains($_.Name.Replace('\', '/')) }).Count) {
    Add-Failure 'The root Unity .cmd launcher exists but is not tracked by Git.'
}
else {
    Write-CheckResult 'PASS' ("Root Unity launcher: {0}" -f $rootLaunchers[0].Name)
}

$buildScript = Join-Path $projectRoot 'Assets\Editor\ChoSirenBuild.cs'
if (Test-Path -LiteralPath $buildScript -PathType Leaf) {
    $buildText = Get-Content -Raw -LiteralPath $buildScript
    foreach ($entryPoint in @('BuildWindows', 'BuildWebGL')) {
        if ($buildText -notmatch ("public static void\s+{0}\s*\(" -f $entryPoint)) {
            Add-Failure ("Build launcher is missing entry point: {0}." -f $entryPoint)
        }
        else {
            Write-CheckResult 'PASS' ("Build entry point: ChoSiren.Editor.ChoSirenBuild.{0}" -f $entryPoint)
        }
    }
}

$buildSettingsPath = Join-Path $projectRoot 'ProjectSettings\EditorBuildSettings.asset'
if (Test-Path -LiteralPath $buildSettingsPath -PathType Leaf) {
    $buildSettings = Get-Content -Raw -LiteralPath $buildSettingsPath
    if ($buildSettings -notmatch 'path:\s+Assets/Scenes/Main\.unity') {
        Add-Failure 'Assets/Scenes/Main.unity is not present in EditorBuildSettings.'
    }
    else {
        Write-CheckResult 'PASS' 'Main scene is present in EditorBuildSettings.'
    }
}

if ($gitRepositoryAvailable) {
    $trackedPaths = @($trackedPathSet)
    $largeTracked = @()
    foreach ($relativePath in $trackedPaths) {
        $absolutePath = Join-Path $projectRoot $relativePath
        if (Test-Path -LiteralPath $absolutePath -PathType Leaf) {
            $item = Get-Item -LiteralPath $absolutePath
            if ($item.Length -ge 50MB) {
                $largeTracked += ("{0:N2} MiB {1}" -f ($item.Length / 1MB), $relativePath)
            }
        }
    }
    if ($largeTracked.Count -eq 0) {
        Write-CheckResult 'PASS' 'No tracked working-tree file is >= 50 MiB.'
    }
    else {
        foreach ($entry in $largeTracked) { Add-Warning ("Large tracked file: {0}" -f $entry) }
    }

    $portableChanges = @(& $git.Source -c core.quotepath=false -C $projectRoot status --porcelain=v1 --untracked-files=all -- Assets Packages ProjectSettings Tools Docs README.md .gitignore .gitattributes)
    if ($portableChanges.Count -gt 0) {
        Add-Warning ("Portable source has {0} uncommitted path(s); commit and push before switching computers." -f $portableChanges.Count)
    }
    else {
        Write-CheckResult 'PASS' 'Portable source paths are clean.'
    }
}

Write-Host ''
Write-Host ("Summary: {0} failure(s), {1} warning(s)." -f $failures.Count, $warnings.Count)
if ($failures.Count -gt 0) { exit 1 }
exit 0
