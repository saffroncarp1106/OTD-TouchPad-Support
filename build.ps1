[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('windows', 'macos')]
    [string]$Platform,
    [ValidateSet('x64', 'arm64')]
    [string]$Architecture = 'x64',
    [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$destination = Join-Path $root "artifacts/$Platform-$Architecture"
$common = @('-c', 'Release', '--self-contained', 'true', '-p:UseSharedCompilation=false', '-m:1')
function Publish-Project([string]$Project, [string]$Output, [string[]]$Options) {
    & $Dotnet publish (Join-Path $root $Project) @common @Options -o (Join-Path $destination $Output)
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $Project" }
}
if ($Platform -eq 'windows') {
    $options = @('-r', "win-$Architecture", '-p:PublishSingleFile=true', '-p:IncludeAllContentForSelfExtract=true', '-p:EnableCompressionInSingleFile=true', '-p:EnableWindowsTargeting=true')
    Publish-Project 'upstream/OpenTabletDriver.UX.Wpf/OpenTabletDriver.UX.Wpf.csproj' 'app' $options
    Publish-Project 'tools/TouchpadProbe/TouchpadProbe.csproj' 'app/Touchpad-Test' $options
    Copy-Item (Join-Path $root 'tools/diagnostics/*') (Join-Path $destination 'app/Touchpad-Test') -Force
    Set-Content (Join-Path $destination 'app/Select-Touchpad.cmd') '@echo off
start "" "%~dp0OTD-Touchpad-Experimental.exe" --touchpad-setup' -Encoding ascii
} else {
    if (-not $IsMacOS) { throw 'Build the macOS app on a Mac so it can be signed.' }
    $app = 'OpenTabletDriver-Touchpad.app'
    $bundle = Join-Path $destination $app
    if (Test-Path $bundle) { Remove-Item $bundle -Recurse -Force }
    $options = @('-r', "osx-$Architecture", '-p:MacBuildBundle=false', '-p:PublishTrimmed=false', '-p:PublishSingleFile=false', '-p:_EnableMacOSCodeSign=false')
    Publish-Project 'upstream/OpenTabletDriver.Daemon/OpenTabletDriver.Daemon.csproj' "$app/Contents/MacOS" $options
    Publish-Project 'upstream/OpenTabletDriver.UX.MacOS/OpenTabletDriver.UX.MacOS.csproj' "$app/Contents/MacOS" $options
    $resources = Join-Path $bundle 'Contents/Resources'
    New-Item $resources -ItemType Directory -Force | Out-Null
    Copy-Item (Join-Path $root 'upstream/eng/bash/macos/Icon.icns') $resources
    Copy-Item (Join-Path $root 'upstream/LICENSE') $resources
    $plist = Join-Path $bundle 'Contents/Info.plist'
    Copy-Item (Join-Path $root 'upstream/eng/bash/macos/Info.plist') $plist
    & /usr/libexec/PlistBuddy -c 'Set :CFBundleIdentifier net.opentabletdriver.touchpad.experimental' $plist
    if ($LASTEXITCODE -ne 0) { throw 'Could not set the app identifier.' }
    & /usr/libexec/PlistBuddy -c 'Set :CFBundleName OpenTabletDriver Touchpad' $plist
    if ($LASTEXITCODE -ne 0) { throw 'Could not set the app name.' }
    [xml]$properties = Get-Content (Join-Path $root 'upstream/Directory.Build.props') -Raw
    $version = [string]$properties.Project.PropertyGroup[0].VersionBase
    & /usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $version" $plist
    if ($LASTEXITCODE -ne 0) { throw 'Could not set the app version.' }
    & chmod +x (Join-Path $bundle 'Contents/MacOS/OpenTabletDriver.Daemon') (Join-Path $bundle 'Contents/MacOS/OpenTabletDriver.UX.MacOS')
    if ($LASTEXITCODE -ne 0) { throw 'Could not set executable permissions.' }
    & codesign --force --deep --sign - $bundle
    if ($LASTEXITCODE -ne 0) { throw 'App signing failed.' }
    & codesign --verify --deep --strict $bundle
    if ($LASTEXITCODE -ne 0) { throw 'App signature verification failed.' }
    $archive = Join-Path $destination "OTD-Touchpad-$version-macos-$Architecture.zip"
    & /usr/bin/ditto -c -k --keepParent $bundle $archive
    if ($LASTEXITCODE -ne 0) { throw 'App archive failed.' }
    $checksum = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content "$archive.sha256" "$checksum  $([IO.Path]::GetFileName($archive))" -NoNewline -Encoding ascii
}
Write-Host "Build complete: $destination"