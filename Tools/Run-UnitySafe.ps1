param(
    [string]$UnityExe = '',
    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot),
    [string]$LogPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Logs\safe-unity-run.log'),
    [string[]]$UnityArguments = @('-batchmode', '-accept-apiupdate', '-quit'),
    [switch]$Visible
)

$ErrorActionPreference = 'Stop'

$projectResolved = [IO.Path]::GetFullPath($ProjectPath)
$projectVersionPath = Join-Path $projectResolved 'ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path -LiteralPath $projectVersionPath)) {
    throw "Not a Unity project: $projectResolved"
}
$versionText = Get-Content -Raw -LiteralPath $projectVersionPath
$versionMatch = [regex]::Match($versionText, '(?m)^m_EditorVersion:\s*(\S+)')
if (-not $versionMatch.Success) { throw "Cannot read Unity version: $projectVersionPath" }
$projectUnityVersion = $versionMatch.Groups[1].Value

if ([string]::IsNullOrWhiteSpace($UnityExe)) {
    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace($env:UNITY_EDITOR)) { $candidates += $env:UNITY_EDITOR }
    if (-not [string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
        $candidates += Join-Path $env:USERPROFILE ("Unity\Hub\Editor\{0}\Editor\Unity.exe" -f $projectUnityVersion)
    }
    if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $candidates += Join-Path $env:ProgramFiles ("Unity\Hub\Editor\{0}\Editor\Unity.exe" -f $projectUnityVersion)
    }
    $UnityExe = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($UnityExe) -or -not (Test-Path -LiteralPath $UnityExe -PathType Leaf)) {
    throw 'Unity executable not found. Pass -UnityExe or set UNITY_EDITOR to the exact project version.'
}
$unityProductVersion = (Get-Item -LiteralPath $UnityExe).VersionInfo.ProductVersion
if ([string]::IsNullOrWhiteSpace($unityProductVersion) -or
    -not $unityProductVersion.StartsWith($projectUnityVersion + '_', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Unity executable version '$unityProductVersion' does not match project version '$projectUnityVersion'."
}

New-Item -ItemType Directory -Force -Path (Split-Path $LogPath) | Out-Null
$listener = [Net.HttpListener]::new()
$listener.Prefixes.Add('http://localhost:38000/unity/build-report/')
$listener.Prefixes.Add('http://127.0.0.1:38000/unity/build-report/')

try {
    # On this managed PC, Unity's Mono listener has repeatedly crashed while the iOA
    # data-protection module is loaded. Holding the exact optional Build Report REST
    # prefixes makes Unity skip that service while leaving Unity, iOA, licensing and
    # the build pipeline untouched. Run this wrapper outside the Codex sandbox: the
    # sandbox itself does not expose a usable Windows HTTP Server API handle.
    try {
        $listener.Start()
    }
    catch [System.Net.HttpListenerException] {
        # Port already reserved (often by a previous safe-run or HTTP.sys). That still
        # blocks Unity's fragile Build Report listener, so continue without re-binding.
        Write-Warning "Port 38000 already in use; continuing without owning the listener. $($_.Exception.Message)"
        $listener = $null
    }
    $arguments = @($UnityArguments) + @('-projectPath', $projectResolved, '-logFile', [IO.Path]::GetFullPath($LogPath))
    # Windows PowerShell's Start-Process joins a string[] without reliably quoting
    # individual values. Build one explicit command line so clones under paths such
    # as "D:\Unity Projects\CHO-SIREN" keep projectPath/testResults/logFile intact.
    $argumentLine = ($arguments | ForEach-Object {
        $argument = [string]$_
        if ($argument.Contains('"')) {
            throw "Unity argument contains an unsupported quote character: $argument"
        }
        if ($argument.Length -eq 0 -or $argument -match '\s') { '"' + $argument + '"' } else { $argument }
    }) -join ' '
    if ($Visible) {
        $process = Start-Process -FilePath $UnityExe -ArgumentList $argumentLine -PassThru
    }
    else {
        $process = Start-Process -FilePath $UnityExe -ArgumentList $argumentLine -PassThru -WindowStyle Hidden
    }
    $process.WaitForExit()
    exit $process.ExitCode
}
finally {
    if ($listener -ne $null -and $listener.IsListening) { $listener.Stop() }
    if ($listener -ne $null) { $listener.Close() }
}
