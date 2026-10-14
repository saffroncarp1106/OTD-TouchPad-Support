$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$experimental = Join-Path $projectRoot 'Experimental (v0.1.0)'
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$env:DOTNET_CLI_HOME = Join-Path $experimental '.build-home'
$env:APPDATA = $env:DOTNET_CLI_HOME
$env:NUGET_PACKAGES = Join-Path $experimental '.packages'
foreach ($target in @(@('OpenTabletDriver.Daemon', 'daemon'), @('OpenTabletDriver.UX.MacOS', 'ui'))) {
    & $dotnet publish (Join-Path $experimental "upstream/$($target[0])/$($target[0]).csproj") -c Release -r osx-x64 --self-contained true -o (Join-Path $projectRoot "build/$($target[1])") -p:MacBuildBundle=false -p:PublishTrimmed=false -p:PublishSingleFile=false -p:UseSharedCompilation=false -p:NuGetAudit=false -v minimal --nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $($target[0])" }
}
