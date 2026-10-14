$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$common = @('-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=true','-p:IncludeAllContentForSelfExtract=true','-p:EnableCompressionInSingleFile=true','-p:UseSharedCompilation=false','-m:1')
& dotnet publish upstream/OpenTabletDriver.UX.Wpf/OpenTabletDriver.UX.Wpf.csproj @common -o rebuilt
if ($LASTEXITCODE -ne 0) { throw 'OTD build failed' }
& dotnet publish testing/TouchpadProbe/TouchpadProbe.csproj @common -o rebuilt/Touchpad-Test
if ($LASTEXITCODE -ne 0) { throw 'Probe build failed' }
Copy-Item testing/*.py rebuilt/Touchpad-Test
