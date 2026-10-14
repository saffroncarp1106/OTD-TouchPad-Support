import hashlib
from pathlib import Path
import plistlib
import shutil
import stat
import zipfile

root = Path(__file__).resolve().parent.parent
release = root / 'release'
release.mkdir(exist_ok=True)
app = release / 'OpenTabletDriver-Touchpad.app'
template = root / 'OpenTabletDriver-0.6.7_osx-x64/OpenTabletDriver.app'
shutil.copytree(template / 'Contents/Resources', app / 'Contents/Resources', dirs_exist_ok=True)
with (template / 'Contents/Info.plist').open('rb') as file:
    info = plistlib.load(file)
info['CFBundleName'] = 'OpenTabletDriver Touchpad'
info['CFBundleIdentifier'] = 'net.opentabletdriver.touchpad.experimental'
info['CFBundleShortVersionString'] = '0.1.0'
with (app / 'Contents/Info.plist').open('wb') as file:
    plistlib.dump(info, file)
destination = app / 'Contents/MacOS'
for part in ('daemon', 'ui'):
    shutil.copytree(root / 'build' / part, destination, dirs_exist_ok=True, ignore=shutil.ignore_patterns('*.pdb'))
for executable in ('OpenTabletDriver.Daemon', 'OpenTabletDriver.UX.MacOS'):
    data = (destination / executable).read_bytes()
    assert data[:4] == b'\xcf\xfa\xed\xfe', f'{executable} is not Mach-O'
    assert int.from_bytes(data[4:8], 'little') == 0x01000007, 'Expected x86_64'
shutil.copy2(root / 'macos/Prepare-Mac.command', release / 'Prepare-Mac.command')
shutil.copy2(root / 'macos/MERGED-README.md', release / 'README.md')
source = root / 'Experimental (v0.1.0)/upstream'
shutil.copy2(source / 'LICENSE', release / 'LICENSE')
with zipfile.ZipFile(release / 'Source.zip', 'w', zipfile.ZIP_DEFLATED) as archive:
    for file in source.rglob('*'):
        if not file.is_file() or any(p in ('bin', 'obj', '.git', '.vs') for p in file.relative_to(source).parts):
            continue
        archive.write(file, 'upstream/' + file.relative_to(source).as_posix())
    for folder in (root / 'macos', root / 'Experimental (v0.1.0)/tests/MacTouchpad'):
        for file in folder.iterdir():
            if file.is_file():
                prefix = 'macos' if folder.name == 'macos' else 'tests/MacTouchpad'
                archive.write(file, prefix + '/' + file.name)
archive_path = release / 'OTD-Touchpad-macOS-x64-test.zip'
with zipfile.ZipFile(archive_path, 'w', zipfile.ZIP_DEFLATED) as archive:
    for file in sorted(release.rglob('*')):
        if not file.is_file() or file == archive_path or file.suffix == '.sha256':
            continue
        entry = zipfile.ZipInfo(file.relative_to(release).as_posix())
        entry.create_system = 3
        executable = file.suffix == '.command' or file.name in ('OpenTabletDriver.Daemon', 'OpenTabletDriver.UX.MacOS', 'createdump')
        entry.external_attr = (stat.S_IFREG | (0o755 if executable else 0o644)) << 16
        entry.compress_type = zipfile.ZIP_DEFLATED
        archive.writestr(entry, file.read_bytes())
with zipfile.ZipFile(archive_path) as archive:
    assert archive.testzip() is None
digest = hashlib.sha256(archive_path.read_bytes()).hexdigest()
archive_path.with_suffix('.zip.sha256').write_text(f'{digest}  {archive_path.name}\n')
print(archive_path)
print('Verified ZIP integrity and x64 Mach-O executables.')
