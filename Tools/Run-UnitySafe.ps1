param(
    [string]$UnityExe = 'C:\Users\51908\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe',
    [string]$ProjectPath = (Split-Path -Parent $PSScriptRoot),
    [string]$LogPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Logs\safe-unity-run.log'),
    [string[]]$UnityArguments = @('-batchmode', '-accept-apiupdate', '-quit'),
    [switch]$Visible
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $UnityExe)) { throw "Unity executable not found: $UnityExe" }
$projectResolved = [IO.Path]::GetFullPath($ProjectPath)
if (-not (Test-Path -LiteralPath (Join-Path $projectResolved 'ProjectSettings\ProjectVersion.txt'))) {
    throw "Not a Unity project: $projectResolved"
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
    $arguments = @($UnityArguments) + @('-projectPath', $projectResolved, '-logFile', $LogPath)
    if ($Visible) {
        $process = Start-Process -FilePath $UnityExe -ArgumentList $arguments -PassThru
    }
    else {
        $process = Start-Process -FilePath $UnityExe -ArgumentList $arguments -PassThru -WindowStyle Hidden
    }
    $process.WaitForExit()
    exit $process.ExitCode
}
finally {
    if ($listener -ne $null -and $listener.IsListening) { $listener.Stop() }
    if ($listener -ne $null) { $listener.Close() }
}
