[CmdletBinding()]
param(
    [switch]$Sample,
    [string[]]$HeroIds,
    [ValidateRange(0.01, 0.24)]
    [double]$SafetyBorder = 0.05,
    [string]$PythonPath = 'C:\Users\51908\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
)

$ErrorActionPreference = 'Stop'
$scriptPath = Join-Path $PSScriptRoot 'prepare_member_runtime_art.py'

if (-not (Test-Path -LiteralPath $PythonPath -PathType Leaf)) {
    $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
    if ($null -eq $pythonCommand) {
        throw "Python was not found. Pass -PythonPath with a Python 3 path that has Pillow installed."
    }
    $PythonPath = $pythonCommand.Source
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
