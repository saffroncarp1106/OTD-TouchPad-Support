from pathlib import Path
import hashlib
import shutil
import subprocess
import zipfile

root=Path(__file__).resolve().parents[1]
package=root/'release'/'OTD-Experimental'
package.mkdir(parents=True,exist_ok=True)
app_source=root/'build/experimental/OTD-Touchpad-Experimental.exe'
app_target=package/'OTD-Touchpad-Experimental.exe'
if not app_target.exists() or hashlib.sha256(app_source.read_bytes()).digest()!=hashlib.sha256(app_target.read_bytes()).digest():
    shutil.copy2(app_source,app_target)
shutil.copy2(root/'README.md',package/'README.md')
license_source=root/'build/inherited-release/OTD-Beta/Licenses'
if not license_source.exists():license_source=root/'release/OTD-Beta/Licenses'
if not license_source.exists():raise RuntimeError('Baseline license bundle missing')
shutil.copytree(license_source,package/'Licenses',dirs_exist_ok=True)
(package/'Select-Touchpad.cmd').write_text('@echo off\nstart "" "%~dp0OTD-Touchpad-Experimental.exe" --touchpad-setup\n')
test=package/'Touchpad-Test';test.mkdir(exist_ok=True)
shutil.copy2(root/'build/probe/Touchpad-Probe.exe',test/'Touchpad-Probe.exe')
for name in ['live_touchpad.py','touchpad_info.py','Scan-Touchpads.cmd','Test-Live-Touchpad.cmd']:
    shutil.copy2(root/'testing'/name,test/name)
shutil.copy2(root/'build/probe/Touchpad-Probe.exe',root/'testing/Touchpad-Probe.exe')
git=shutil.which('git') or 'C:/Program Files/Git/cmd/git.exe'
files=subprocess.check_output([git,'-C',str(root/'upstream'),'ls-files','--cached','--others','--exclude-standard','-z']).decode().split('\0')
with zipfile.ZipFile(package/'Source.zip','w',zipfile.ZIP_DEFLATED) as archive:
    for name in files:
        if name and (root/'upstream'/name).is_file():archive.write(root/'upstream'/name,'OpenTabletDriver/'+name)
    for name in ['Program.cs','TouchpadProbe.csproj']:
        content=(root/'testing/TouchpadProbe'/name).read_text().replace('../../upstream/','../../OpenTabletDriver/')
        archive.writestr('testing/TouchpadProbe/'+name,content)
    for name in ['live_touchpad.py','touchpad_info.py']:
        archive.write(root/'testing'/name,'testing/'+name)
    archive.writestr('Build.ps1', '''$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$common = @('-c','Release','-r','win-x64','--self-contained','true','-p:PublishSingleFile=true','-p:IncludeAllContentForSelfExtract=true','-p:EnableCompressionInSingleFile=true','-p:UseSharedCompilation=false','-m:1')
& dotnet publish OpenTabletDriver/OpenTabletDriver.UX.Wpf/OpenTabletDriver.UX.Wpf.csproj @common -o rebuilt
if ($LASTEXITCODE -ne 0) { throw 'OTD build failed' }
& dotnet publish testing/TouchpadProbe/TouchpadProbe.csproj @common -o rebuilt/Touchpad-Test
if ($LASTEXITCODE -ne 0) { throw 'Probe build failed' }
Copy-Item testing/*.py rebuilt/Touchpad-Test
''')
hashes=[]
def distributable(path):
    return path.is_file() and not any(part in ('.local','results','__pycache__','userdata','logs') for part in path.relative_to(package).parts)
for path in sorted(package.rglob('*')):
    if distributable(path) and path.name!='SHA256SUMS.txt':hashes.append(hashlib.sha256(path.read_bytes()).hexdigest()+'  '+path.relative_to(package).as_posix())
(package/'SHA256SUMS.txt').write_text('\n'.join(hashes)+'\n')
destination=root/'release/OTD-Experimental (v0.1.0).zip'
with zipfile.ZipFile(destination,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
    for path in sorted(package.rglob('*')):
        if distributable(path):archive.write(path,'OTD-Experimental/'+path.relative_to(package).as_posix())
with zipfile.ZipFile(destination) as archive:
    assert archive.testzip() is None
    assert not any(part in ('userdata','results','.git') for name in archive.namelist() for part in name.split('/'))
with zipfile.ZipFile(package/'Source.zip') as archive:
    assert any(name.endswith('/GenericTouchpad.cs') for name in archive.namelist())
    assert archive.testzip() is None
destination.with_suffix(destination.suffix+'.sha256').write_text(hashlib.sha256(destination.read_bytes()).hexdigest()+'  '+destination.name+'\n')
print('Verified package:',destination)
