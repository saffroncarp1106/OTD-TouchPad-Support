[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('windows', 'macos')]
    [string]$Platform,
    [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$destination = Join-Path $root "artifacts/$Platform"
$common = @('-c', 'Release', '--self-contained', 'true', '-p:UseSharedCompilation=false', '-m:1')
function Publish-Project([string]$Project, [string]$Output, [string[]]$Options) {
    & $Dotnet publish (Join-Path $root $Project) @common @Options -o (Join-Path $destination $Output)
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $Project" }
}
if ($Platform -eq 'windows') {
    $options = @('-r', 'win-x64', '-p:PublishSingleFile=true', '-p:IncludeAllContentForSelfExtract=true', '-p:EnableCompressionInSingleFile=true')
    Publish-Project 'upstream/OpenTabletDriver.UX.Wpf/OpenTabletDriver.UX.Wpf.csproj' 'app' $options
    Publish-Project 'tools/TouchpadProbe/TouchpadProbe.csproj' 'app/Touchpad-Test' $options
    Copy-Item (Join-Path $root 'tools/diagnostics/*') (Join-Path $destination 'app/Touchpad-Test') -Force
    Set-Content (Join-Path $destination 'app/Select-Touchpad.cmd') '@echo off
start "" "%~dp0OTD-Touchpad-Experimental.exe" --touchpad-setup' -Encoding ascii
} else {
    $options = @('-r', 'osx-x64', '-p:MacBuildBundle=false', '-p:PublishTrimmed=false', '-p:PublishSingleFile=false')
    Publish-Project 'upstream/OpenTabletDriver.Daemon/OpenTabletDriver.Daemon.csproj' 'daemon' $options
    Publish-Project 'upstream/OpenTabletDriver.UX.MacOS/OpenTabletDriver.UX.MacOS.csproj' 'ui' $options
}
Write-Host "Build complete: $destination"
