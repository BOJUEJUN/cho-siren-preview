[CmdletBinding()]
param(
    [switch]$Sample,
    [string[]]$HeroIds,
    [ValidateRange(0.01, 0.24)]
    [double]$SafetyBorder = 0.05,
    [string]$PythonPath = ''
)

$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot 'prepare_member_runtime_art.py'

if ([string]::IsNullOrWhiteSpace($PythonPath)) {
    $pythonCandidates = @()
    if (-not [string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
        $pythonCandidates += Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
    }
    foreach ($commandName in @('python', 'python3', 'py')) {
        $command = Get-Command $commandName -ErrorAction SilentlyContinue
        if ($null -ne $command) { $pythonCandidates += $command.Source }
    }
    $PythonPath = $pythonCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($PythonPath) -or -not (Test-Path -LiteralPath $PythonPath -PathType Leaf)) {
    throw "Python was not found. Pass -PythonPath with a Python 3 path that has Pillow installed."
}

$arguments = @($scriptPath, '--safety-border', $SafetyBorder.ToString([Globalization.CultureInfo]::InvariantCulture))
if ($Sample) {
    $arguments += '--sample'
}
if ($HeroIds.Count -gt 0) {
    $arguments += @('--ids', ($HeroIds -join ','))
}

& $PythonPath @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Member runtime art preparation failed with exit code $LASTEXITCODE."
}
