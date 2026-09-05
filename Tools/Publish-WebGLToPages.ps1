[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$BuildPath = (Join-Path $PSScriptRoot '..\Builds\WebGL'),
    [string]$PagesPath = (Join-Path $PSScriptRoot '..\..\cho-siren-pages'),
    [string]$FallbackBuildPath,
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# One implementation for macOS and Windows. Never delete the last complete bundle.
$node = Get-Command node -ErrorAction Stop
$stager = Join-Path $PSScriptRoot 'Stage-WebGL.mjs'
$arguments = @($stager, '--build', $BuildPath, '--pages', $PagesPath)
if ($FallbackBuildPath) { $arguments += @('--fallback-build', $FallbackBuildPath) }
if (-not ($DryRun -or $WhatIfPreference) -and
    $PSCmdlet.ShouldProcess($PagesPath, 'Stage verified WebGL and retain previous release')) {
    $arguments += '--apply'
}
& $node.Source @arguments
if ($LASTEXITCODE -ne 0) { throw 'WebGL staging failed; no commit or push was performed.' }
