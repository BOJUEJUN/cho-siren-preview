$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$runner = Join-Path $PSScriptRoot 'Run-UnitySafe.ps1'

& $runner `
    -ProjectPath $projectRoot `
    -LogPath (Join-Path $projectRoot 'Logs\editor-safe.log') `
    -UnityArguments @('-accept-apiupdate') `
    -Visible
