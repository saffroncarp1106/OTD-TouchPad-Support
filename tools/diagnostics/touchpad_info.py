import argparse
import ctypes as C
from ctypes import wintypes as W
from datetime import datetime, timezone
import json
from pathlib import Path
import struct
import sys

if sys.platform != 'win32':
    raise SystemExit('This diagnostic requires Windows.')
import winreg

user32 = C.WinDLL('user32', use_last_error=True)
hid = C.WinDLL('hid', use_last_error=True)
ERROR = 0xffffffff
SUCCESS = 0x110000

class Device(C.Structure):
    _fields_ = [('handle', W.HANDLE), ('type', W.DWORD)]

def api(lib, name, args, result):
    fn = getattr(lib, name)
    fn.argtypes, fn.restype = args, result
    return fn

get_list = api(user32, 'GetRawInputDeviceList', [C.c_void_p, C.POINTER(W.UINT), W.UINT], W.UINT)
get_info = api(user32, 'GetRawInputDeviceInfoW', [W.HANDLE, W.UINT, C.c_void_p, C.POINTER(W.UINT)], W.UINT)
get_caps = api(hid, 'HidP_GetCaps', [C.c_void_p, C.c_void_p], C.c_long)
value_caps = api(hid, 'HidP_GetValueCaps', [C.c_int, C.c_void_p, C.POINTER(W.USHORT), C.c_void_p], C.c_long)
button_caps = api(hid, 'HidP_GetButtonCaps', [C.c_int, C.c_void_p, C.POINTER(W.USHORT), C.c_void_p], C.c_long)

def checked(result):
    if result == ERROR:
        raise C.WinError(C.get_last_error())
    return result

def metadata(path):


    parts = path.removeprefix('\\\\?\\').split('#')
    if len(parts) < 4:
        return {}
    instance = '\\'.join(parts[:3])
    result = {'instance_id': instance}
    try:
        with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, 'SYSTEM\\CurrentControlSet\\Enum\\' + instance) as key:
            for field in ('FriendlyName', 'DeviceDesc', 'Mfg', 'HardwareID', 'CompatibleIDs', 'Service', 'Driver'):
                try:
                    result[field] = winreg.QueryValueEx(key, field)[0]
                except OSError:
                    pass
    except OSError as exc:
        result['metadata_note'] = str(exc)
    return result

def capabilities(handle):
    size = W.UINT()
    checked(get_info(handle, 0x20000005, None, C.byref(size)))
    if not size.value:
        raise RuntimeError('No HID preparsed data available')
    data = C.create_string_buffer(size.value)
    checked(get_info(handle, 0x20000005, data, C.byref(size)))
    caps = C.create_string_buffer(64)
    if get_caps(data, caps) != SUCCESS:
        raise RuntimeError('HidP_GetCaps failed')
    words = struct.unpack('<32H', caps.raw)
    result = {'input_report_bytes': words[2], 'output_report_bytes': words[3],
              'feature_report_bytes': words[4], 'link_collections': words[22], 'fields': []}
    for report_type, kind in enumerate(('input', 'output', 'feature')):
        for is_value, fn, count in ((True, value_caps, words[24 + report_type * 3]),
                                    (False, button_caps, words[23 + report_type * 3])):
            if not count:
                continue
            length = W.USHORT(count)
            buf = C.create_string_buffer(count * 72)
            status = fn(report_type, buf, C.byref(length), data)
            if status != SUCCESS:
                raise RuntimeError(f'{kind} capabilities failed: {status & ERROR:#x}')
            for index in range(length.value):
                b = buf.raw[index * 72:(index + 1) * 72]
                lo = struct.unpack_from('<H', b, 56)[0]
                hi = struct.unpack_from('<H', b, 58)[0] if b[12] else lo
                field = {'kind': kind, 'field_type': 'value' if is_value else 'button',
                         'usage_page': struct.unpack_from('<H', b)[0], 'usage_min': lo, 'usage_max': hi,
                         'report_id': b[2], 'link_collection': struct.unpack_from('<H', b, 6)[0],
                         'absolute': bool(b[15])}
                if is_value:
                    exponent, units, lmin, lmax, pmin, pmax = struct.unpack_from('<IIiiii', b, 32)
                    exponent = exponent & 15
                    if exponent >= 8:
                        exponent -= 16
                    field.update(bit_size=struct.unpack_from('<H', b, 18)[0],
                                 report_count=struct.unpack_from('<H', b, 20)[0],
                                 logical_min=lmin, logical_max=lmax, physical_min=pmin, physical_max=pmax,
                                 units=units, units_exponent=exponent)
                result['fields'].append(field)
    axes = {}
    for f in result['fields']:
        if f['kind'] == 'input' and f['field_type'] == 'value' and f['usage_page'] == 1 and f['absolute']:
            for usage, name in ((0x30, 'X'), (0x31, 'Y')):
                if f['usage_min'] <= usage <= f['usage_max'] and f['logical_max'] > f['logical_min']:
                    axes.setdefault((f['report_id'], f['link_collection']), set()).add(name)
    result['absolute_xy_collections'] = [{'report_id': report, 'link_collection': link}
                                        for (report, link), names in axes.items() if names == {'X', 'Y'}]
    return result

def inventory():

    for _ in range(3):
        count = W.UINT()
        checked(get_list(None, C.byref(count), C.sizeof(Device)))
        devices = (Device * count.value)()
        total = get_list(devices, C.byref(count), C.sizeof(Device))
        if total != ERROR:
            break
        if C.get_last_error() != 122:
            checked(total)
    else:
        raise RuntimeError('Device list keeps changing; retry the scan')
    results = []
    for device in devices[:total]:
        if device.type != 2:
            continue
        item = {}
        try:
            size = W.UINT()
            checked(get_info(device.handle, 0x20000007, None, C.byref(size)))
            name = C.create_unicode_buffer(size.value + 1)
            size.value += 1
            checked(get_info(device.handle, 0x20000007, name, C.byref(size)))
            item['path'] = name.value
            item['windows'] = metadata(name.value)
            info = C.create_string_buffer(32)
            struct.pack_into('<I', info, 0, 32)
            size = W.UINT(32)
            checked(get_info(device.handle, 0x2000000b, info, C.byref(size)))
            vid, pid, version, page, usage = struct.unpack_from('<IIIHH', info.raw, 8)
            item.update(vendor_id=f'{vid:04X}', product_id=f'{pid:04X}', version=version,
                        usage_page=page, usage=usage, touchpad_collection=(page == 13 and usage == 5))
            item['capabilities'] = capabilities(device.handle)
        except (OSError, RuntimeError) as exc:
            item['error'] = str(exc)
        results.append(item)
    return results

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--all', action='store_true', help='Display every Raw Input HID collection')
    parser.add_argument('--match', help='Filter by device path, hardware ID, or Windows name (substring)')
    parser.add_argument('--output', type=Path, help='JSON destination (default: timestamped results file)')
    args = parser.parse_args()
    devices = inventory()
    output = args.output or Path(__file__).resolve().parent / 'results' / (datetime.now().strftime('%Y%m%d-%H%M%S-%f') + '.json')
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps({'created_utc': datetime.now(timezone.utc).isoformat(),
                                 'scope': 'Raw Input HID collections; capability discovery, not live input validation',
                                 'devices': devices}, indent=2), encoding='utf-8')
    shown = 0
    for d in devices:
        if args.match:
            show = args.match.casefold() in json.dumps(d).casefold()
        else:
            show = args.all or d.get('touchpad_collection') or 'touchpad' in json.dumps(d.get('windows', {})).casefold()
        if not show:
            continue
        shown += 1
        meta = d.get('windows', {})
        label = meta.get('FriendlyName') or meta.get('DeviceDesc', 'Unnamed HID collection').split(';')[-1]
        print(f'\n[{shown}] {label}')
        print(f"  VID: {d.get('vendor_id', '?')}  PID: {d.get('product_id', '?')}  Usage: {d.get('usage_page', 0):04X}/{d.get('usage', 0):04X}")
        print('  Path:', d.get('path', '?'))
        print('  Hardware IDs:', ', '.join(meta.get('HardwareID', [])) or 'unavailable')
        caps = d.get('capabilities', {})
        print('  Absolute X/Y collections:', caps.get('absolute_xy_collections', []))
        print('  Input report bytes:', caps.get('input_report_bytes', 'unknown'))
        if 'error' in d:
            print('  Error:', d['error'])
    if not shown:
        print('No matching touchpad found. Try --all or --match with a Device Manager hardware ID.')
    print(f'\nSaved {len(devices)} HID collections to {output}')
    print('Identification only: live coordinate decoding/support is not yet confirmed.')

if __name__ == '__main__':
    main()
