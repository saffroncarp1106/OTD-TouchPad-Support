import argparse
import ctypes as C
from datetime import datetime, timezone
import json
from pathlib import Path
import sys


class MacHID:
    def __init__(self):
        self.cf = C.CDLL('/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation')
        self.hid = C.CDLL('/System/Library/Frameworks/IOKit.framework/IOKit')
        ptr, index, uint = C.c_void_p, C.c_ssize_t, C.c_uint32
        self.bind(self.cf, 'CFRelease', [ptr], None)
        self.bind(self.cf, 'CFGetTypeID', [ptr], C.c_ulong)
        for name in ('String', 'Number', 'Boolean', 'Data', 'Array', 'Dictionary'):
            self.bind(self.cf, f'CF{name}GetTypeID', [], C.c_ulong)
        self.bind(self.cf, 'CFStringCreateWithCString', [ptr, C.c_char_p, uint], ptr)
        self.bind(self.cf, 'CFStringGetLength', [ptr], index)
        self.bind(self.cf, 'CFStringGetMaximumSizeForEncoding', [index, uint], index)
        self.bind(self.cf, 'CFStringGetCString', [ptr, ptr, index, uint], C.c_bool)
        self.bind(self.cf, 'CFNumberIsFloatType', [ptr], C.c_bool)
        self.bind(self.cf, 'CFNumberGetValue', [ptr, C.c_int, ptr], C.c_bool)
        self.bind(self.cf, 'CFBooleanGetValue', [ptr], C.c_bool)
        self.bind(self.cf, 'CFDataGetLength', [ptr], index)
        self.bind(self.cf, 'CFDataGetBytePtr', [ptr], ptr)
        self.bind(self.cf, 'CFArrayGetCount', [ptr], index)
        self.bind(self.cf, 'CFArrayGetValueAtIndex', [ptr, index], ptr)
        self.bind(self.cf, 'CFDictionaryGetCount', [ptr], index)
        self.bind(self.cf, 'CFDictionaryGetKeysAndValues', [ptr, ptr, ptr], None)
        self.bind(self.cf, 'CFSetGetCount', [ptr], index)
        self.bind(self.cf, 'CFSetGetValues', [ptr, ptr], None)
        self.bind(self.hid, 'IOHIDManagerCreate', [ptr, uint], ptr)
        self.bind(self.hid, 'IOHIDManagerSetDeviceMatching', [ptr, ptr], None)
        self.bind(self.hid, 'IOHIDManagerCopyDevices', [ptr], ptr)
        self.bind(self.hid, 'IOHIDDeviceGetProperty', [ptr, ptr], ptr)
        self.bind(self.hid, 'IOHIDDeviceCopyMatchingElements', [ptr, ptr, uint], ptr)
        self.bind(self.hid, 'IOHIDDeviceGetService', [ptr], uint)
        self.bind(self.hid, 'IORegistryEntryGetRegistryEntryID', [uint, C.POINTER(C.c_uint64)], C.c_int)
        for name in ('Type', 'UsagePage', 'Usage', 'ReportID', 'ReportSize', 'ReportCount', 'Unit', 'UnitExponent', 'Cookie'):
            self.bind(self.hid, f'IOHIDElementGet{name}', [ptr], uint)
        for name in ('LogicalMin', 'LogicalMax', 'PhysicalMin', 'PhysicalMax'):
            self.bind(self.hid, f'IOHIDElementGet{name}', [ptr], index)
        self.bind(self.hid, 'IOHIDElementGetParent', [ptr], ptr)
        self.bind(self.hid, 'IOHIDElementIsRelative', [ptr], C.c_bool)

    @staticmethod
    def bind(lib, name, args, result):
        fn = getattr(lib, name)
        fn.argtypes, fn.restype = args, result

    def convert(self, ref, depth=0):
        if not ref:
            return None
        if depth > 6:
            return {'unavailable': 'nested property depth limit'}
        cf = self.cf
        kind = cf.CFGetTypeID(ref)
        if kind == cf.CFStringGetTypeID():
            length = cf.CFStringGetMaximumSizeForEncoding(cf.CFStringGetLength(ref), 0x08000100) + 1
            buf = C.create_string_buffer(length)
            return buf.value.decode('utf-8', errors='replace') if cf.CFStringGetCString(ref, buf, length, 0x08000100) else None
        if kind == cf.CFBooleanGetTypeID():
            return bool(cf.CFBooleanGetValue(ref))
        if kind == cf.CFNumberGetTypeID():
            floating = cf.CFNumberIsFloatType(ref)
            value = C.c_double() if floating else C.c_int64()
            return value.value if cf.CFNumberGetValue(ref, 13 if floating else 4, C.byref(value)) else None
        if kind == cf.CFDataGetTypeID():
            length = cf.CFDataGetLength(ref)
            limit = min(length, 1048576)
            data = C.string_at(cf.CFDataGetBytePtr(ref), limit) if limit else b''
            return {'byte_length': length, 'hex': data.hex(), 'truncated': limit != length}
        if kind == cf.CFArrayGetTypeID():
            return [self.convert(cf.CFArrayGetValueAtIndex(ref, i), depth + 1) for i in range(cf.CFArrayGetCount(ref))]
        if kind == cf.CFDictionaryGetTypeID():
            count = cf.CFDictionaryGetCount(ref)
            keys, values = (C.c_void_p * count)(), (C.c_void_p * count)()
            cf.CFDictionaryGetKeysAndValues(ref, keys, values)
            return {str(self.convert(keys[i], depth + 1)): self.convert(values[i], depth + 1) for i in range(count)}
        return {'unavailable': 'unsupported CoreFoundation type', 'type_id': kind}

    def property(self, device, name):
        key = self.cf.CFStringCreateWithCString(None, name.encode('utf-8'), 0x08000100)
        if not key:
            raise RuntimeError('Could not allocate property key')
        try:
            return self.convert(self.hid.IOHIDDeviceGetProperty(device, key))
        finally:
            self.cf.CFRelease(key)

    def elements(self, device):
        array = self.hid.IOHIDDeviceCopyMatchingElements(device, None, 0)
        if not array:
            return [], 'No HID elements exposed through IOHIDDeviceCopyMatchingElements'
        fields = []
        try:
            for i in range(self.cf.CFArrayGetCount(array)):
                element = self.cf.CFArrayGetValueAtIndex(array, i)
                get = lambda name: getattr(self.hid, f'IOHIDElementGet{name}')(element)
                kind = get('Type')
                parent = self.hid.IOHIDElementGetParent(element)
                exponent = get('UnitExponent') & 15
                field = {
                    'kind': 'input' if kind in (1, 2, 3, 4) else {129: 'output', 257: 'feature', 513: 'collection'}.get(kind, 'unknown'),
                    'native_type': kind,
                    'field_type': 'button' if kind == 2 else 'collection' if kind == 513 else 'value',
                    'element_cookie': get('Cookie'),
                    'parent_cookie': self.hid.IOHIDElementGetCookie(parent) if parent else None,
                    'usage_page': get('UsagePage'), 'usage': get('Usage'),
                    'report_id': get('ReportID'), 'bit_size': get('ReportSize'),
                    'report_count': get('ReportCount'),
                    'absolute': not bool(self.hid.IOHIDElementIsRelative(element)) if kind != 513 else None,
                    'logical_min': get('LogicalMin'), 'logical_max': get('LogicalMax'),
                    'physical_min': get('PhysicalMin'), 'physical_max': get('PhysicalMax'),
                    'units': get('Unit'), 'units_exponent': exponent - 16 if exponent >= 8 else exponent,
                }
                fields.append(field)
        finally:
            self.cf.CFRelease(array)
        return fields, None

    def inventory(self):
        manager = self.hid.IOHIDManagerCreate(None, 0)
        if not manager:
            raise RuntimeError('IOHIDManagerCreate failed')
        devices = None
        try:
            self.hid.IOHIDManagerSetDeviceMatching(manager, None)
            devices = self.hid.IOHIDManagerCopyDevices(manager)
            if not devices:
                return []
            count = self.cf.CFSetGetCount(devices)
            refs = (C.c_void_p * count)()
            self.cf.CFSetGetValues(devices, refs)
            return [self.inspect(ref) for ref in refs]
        finally:
            if devices:
                self.cf.CFRelease(devices)
            self.cf.CFRelease(manager)

    def inspect(self, device):
        names = ('Product', 'Manufacturer', 'VendorID', 'ProductID', 'VersionNumber', 'Transport',
                 'PrimaryUsagePage', 'PrimaryUsage', 'DeviceUsagePairs', 'LocationID', 'Built-In',
                 'MaxInputReportSize', 'MaxOutputReportSize', 'MaxFeatureReportSize', 'ReportDescriptor')
        props = {name: self.property(device, name) for name in names}
        fields, note = self.elements(device)
        registry_id = C.c_uint64()
        service = self.hid.IOHIDDeviceGetService(device)
        identity = registry_id.value if service and self.hid.IORegistryEntryGetRegistryEntryID(service, C.byref(registry_id)) == 0 else None
        pairs = props.get('DeviceUsagePairs') or []
        standard = (props['PrimaryUsagePage'], props['PrimaryUsage']) == (13, 5)
        standard = standard or any(isinstance(p, dict) and (p.get('DeviceUsagePage'), p.get('DeviceUsage')) == (13, 5) for p in pairs)
        standard = standard or any(f['kind'] == 'collection' and (f['usage_page'], f['usage']) == (13, 5) for f in fields)
        label = str(props.get('Product') or '')
        named = any(word in label.casefold() for word in ('trackpad', 'touchpad', 'multitouch', 'multi-touch'))
        axes = {}
        for field in fields:
            if field['kind'] == 'input' and field['absolute'] and field['usage_page'] == 1 and field['usage'] in (48, 49) and field['logical_max'] > field['logical_min']:
                axes.setdefault((field['report_id'], field['parent_cookie']), set()).add(field['usage'])
        return {
            'name': props['Product'], 'registry_entry_id': identity,
            'vendor_id': f"{props['VendorID']:04X}" if isinstance(props['VendorID'], int) else None,
            'product_id': f"{props['ProductID']:04X}" if isinstance(props['ProductID'], int) else None,
            'usage_page': props['PrimaryUsagePage'], 'usage': props['PrimaryUsage'],
            'touchpad_collection': standard, 'name_candidate': named,
            'candidate_reason': 'standard touchpad collection' if standard else 'name only; unverified' if named else None,
            'macos': props,
            'capabilities': {
                'input_report_bytes': props['MaxInputReportSize'],
                'output_report_bytes': props['MaxOutputReportSize'],
                'feature_report_bytes': props['MaxFeatureReportSize'],
                'fields': fields,
                'absolute_xy_collections': [{'report_id': r, 'parent_cookie': p} for (r, p), usages in axes.items() if usages == {48, 49}],
                'note': note,
            },
        }


def main():
    parser = argparse.ArgumentParser(description='Read-only macOS HID inventory; no pointer changes or live capture.')
    parser.add_argument('--all', action='store_true', help='Display all HID devices, including vendor-defined devices')
    parser.add_argument('--match', help='Display devices containing this name or identifier')
    parser.add_argument('--output', type=Path, help='JSON output path')
    args = parser.parse_args()
    if sys.platform != 'darwin':
        parser.exit(1, 'Run this script on macOS with Python 3.9 or newer.\n')
    try:
        devices = MacHID().inventory()
    except (OSError, AttributeError, RuntimeError) as exc:
        parser.exit(1, f'HID inventory failed: {exc}\n')
    devices.sort(key=lambda d: (not d['touchpad_collection'], not d['name_candidate'], str(d['name']), d['registry_entry_id'] or 0))
    output = args.output or Path(__file__).resolve().parent / 'results' / (datetime.now().strftime('macos-%Y%m%d-%H%M%S-%f') + '.json')
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps({'platform': 'macos', 'created_utc': datetime.now(timezone.utc).isoformat(),
                                 'scope': 'Public IOHIDManager inventory; no live input or compatibility verification',
                                 'devices': devices}, indent=2), encoding='utf-8')
    shown = 0
    for device in devices:
        show = args.match.casefold() in json.dumps(device).casefold() if args.match else args.all or device['touchpad_collection'] or device['name_candidate']
        if not show:
            continue
        shown += 1
        print(f"\n[{shown}] {device['name'] or 'Unnamed HID device'}")
        print(f"  VID/PID: {device['vendor_id']} / {device['product_id']} | Transport: {device['macos']['Transport']}")
        print(f"  Usage page/usage: {device['usage_page']} / {device['usage']} | {device['candidate_reason'] or 'other HID device'}")
        print('  Absolute X/Y groups:', device['capabilities']['absolute_xy_collections'])
        print('  Input report bytes:', device['capabilities']['input_report_bytes'])
        print('  HID elements:', len(device['capabilities']['fields']))
    if not shown:
        print('No matching candidates. Try --all; Apple trackpads may expose vendor-defined interfaces.')
    print(f'\nSaved {len(devices)} devices to {output}')
    print('A listed device is not proof that absolute finger coordinates are accessible.')


if __name__ == '__main__':
    main()
