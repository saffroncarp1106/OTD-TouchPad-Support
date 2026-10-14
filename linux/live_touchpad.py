#!/usr/bin/env python3
"""Read-only Linux touchpad inventory and absolute-position JSONL logger."""
import argparse
import array
from collections import deque
from datetime import datetime, timezone
import fcntl
import json
import math
from pathlib import Path
import select
import sys
import time


def normalize(value, axis):
    return round(max(0, min(1, (value - axis.min) / (axis.max - axis.min))) * 65535)


def describe(device, e):
    caps = device.capabilities()
    axes = dict(caps.get(e.EV_ABS, []))
    keys = caps.get(e.EV_KEY, [])
    props = device.input_props()
    xy = (e.ABS_MT_POSITION_X, e.ABS_MT_POSITION_Y) if e.ABS_MT_POSITION_X in axes else (e.ABS_X, e.ABS_Y)
    absolute = all(code in axes and axes[code].max > axes[code].min for code in xy)
    candidate = absolute and e.INPUT_PROP_DIRECT not in props and (
        e.BTN_TOOL_FINGER in keys or e.INPUT_PROP_POINTER in props or 'touchpad' in device.name.lower())
    protocol = 'B' if e.ABS_MT_SLOT in axes else 'A' if e.ABS_MT_POSITION_X in axes else 'single'
    return {
        'path': device.path, 'name': device.name, 'phys': device.phys,
        'vendor': f'{device.info.vendor:04X}', 'product': f'{device.info.product:04X}',
        'bus': device.info.bustype, 'version': device.info.version,
        'properties': props, 'keys': keys, 'touchpad_candidate': candidate,
        'absolute_xy': absolute, 'protocol': protocol,
        'axes': {str(code): dict(info._asdict()) for code, info in axes.items()},
        'width_mm': (axes[xy[0]].max - axes[xy[0]].min) / axes[xy[0]].resolution
        if xy[0] in axes and axes[xy[0]].resolution > 0 else None,
        'height_mm': (axes[xy[1]].max - axes[xy[1]].min) / axes[xy[1]].resolution
        if xy[1] in axes and axes[xy[1]].resolution > 0 else None,
    }


class Frames:
    """Accumulate slot deltas until SYN_REPORT; preserve unchanged contacts."""
    def __init__(self, device, e):
        self.device, self.e = device, e
        self.axes = dict(device.capabilities().get(e.EV_ABS, []))
        self.mt = e.ABS_MT_SLOT in self.axes
        self.slot = 0
        self.slots = {}
        self.keys = set()
        self.single = {}
        self.resync()

    def resync(self):
        e = self.e
        self.keys = set(self.device.active_keys())
        if self.mt:
            info = self.device.absinfo(e.ABS_MT_SLOT)
            self.slot = info.value
            # EVIOCGMTSLOTS: query kernel's current state, including fingers
            # already touching when this independent event reader is opened.
            size = info.max + 1
            if size < 1 or size > 256:
                raise RuntimeError('Unsupported multitouch slot range')
            values = {}
            for code in (e.ABS_MT_TRACKING_ID, e.ABS_MT_POSITION_X, e.ABS_MT_POSITION_Y):
                buf = array.array('i', [code] + [0] * size)
                request = (2 << 30) | (len(buf) * buf.itemsize << 16) | (ord('E') << 8) | 0x0a
                fcntl.ioctl(self.device.fd, request, buf, True)
                values[code] = buf[1:]
            self.slots = {slot: {code: data[slot] for code, data in values.items()}
                          for slot in range(size)}
        else:
            self.single = {code: self.device.absinfo(code).value for code in (e.ABS_X, e.ABS_Y)}

    def push(self, event):
        e = self.e
        if event.type == e.EV_KEY:
            if event.value:
                self.keys.add(event.code)
            else:
                self.keys.discard(event.code)
        elif event.type == e.EV_ABS:
            if self.mt:
                if event.code == e.ABS_MT_SLOT:
                    self.slot = event.value
                else:
                    contact = self.slots.setdefault(self.slot, {})
                    if event.code == e.ABS_MT_TRACKING_ID and event.value != contact.get(event.code):
                        contact.clear()
                    contact[event.code] = event.value
            else:
                self.single[event.code] = event.value
        if event.type != e.EV_SYN or event.code != e.SYN_REPORT:
            return None
        xcode, ycode = (e.ABS_MT_POSITION_X, e.ABS_MT_POSITION_Y) if self.mt else (e.ABS_X, e.ABS_Y)
        contacts = [(slot, point) for slot, point in sorted(self.slots.items())
                    if point.get(e.ABS_MT_TRACKING_ID, -1) >= 0] if self.mt else (
                        [(0, self.single)] if e.BTN_TOUCH in self.keys else [])
        points = []
        for slot, point in contacts:
            if xcode not in point or ycode not in point:
                continue
            x, y = point[xcode], point[ycode]
            points.append({'Id': point.get(e.ABS_MT_TRACKING_ID, 0), 'X': normalize(x, self.axes[xcode]),
                           'Y': normalize(y, self.axes[ycode]), 'raw_x': x, 'raw_y': y,
                           'slot': slot, 'raw_axes': {str(code): value for code, value in point.items()}})
        return {'count': len(contacts), 'points': points, 'complete': len(points) == len(contacts),
                'buttons': sorted(self.keys)}


def capture(device, metadata, args, e):
    frames = Frames(device, e)
    path = args.output or Path(__file__).resolve().parent / 'results' / (
        'live-' + datetime.now().strftime('%Y%m%d-%H%M%S-%f') + '.jsonl')
    path.parent.mkdir(parents=True, exist_ok=True)
    print('Move one finger, lift, then try multiple fingers. Capture:', path)
    start = time.monotonic()
    count = dropped = 0
    ignoring = False
    times = deque()
    next_display = 0
    reason = 'duration'
    with path.open('x', encoding='utf-8') as log:
        def emit(row):
            log.write(json.dumps(row, separators=(',', ':')) + '\n')
        emit({'kind': 'start', 'created_utc': datetime.now(timezone.utc).isoformat(),
              'schema_version': 1, 'device': metadata, 'duration_seconds': args.seconds,
              'coordinates': 'X/Y normalized 0..65535; raw_x/raw_y kernel units; Y not inverted',
              'source': 'Linux evdev, kernel-decoded absolute input; no grab or pointer output'})
        try:
            while time.monotonic() - start < args.seconds:
                remaining = args.seconds - (time.monotonic() - start)
                ready, _, _ = select.select([device.fd], [], [], max(0, min(0.25, remaining)))
                if not ready:
                    continue
                for event in device.read():
                    now = time.monotonic() - start
                    if event.type == e.EV_SYN and event.code == e.SYN_DROPPED:
                        ignoring = True
                        dropped += 1
                        emit({'kind': 'dropped', 'seconds': now, 'note': 'Discarding through next SYN_REPORT, then querying kernel state'})
                        continue
                    if ignoring:
                        if event.type == e.EV_SYN and event.code == e.SYN_REPORT:
                            frames.resync()
                            ignoring = False
                            emit({'kind': 'resync', 'seconds': now})
                        continue
                    row = frames.push(event)
                    if row is None:
                        continue
                    count += 1
                    emit(dict(kind='frame', seconds=now, scan=count, event_seconds=event.timestamp(), **row))
                    times.append(now)
                    while times and times[0] < now - 2:
                        times.popleft()
                    if now >= next_display:
                        hz = (len(times) - 1) / (times[-1] - times[0]) if len(times) > 1 and times[-1] > times[0] else 0
                        print(f'{hz:6.1f} frames/s | contacts={row["count"]} | {row["points"]}')
                        log.flush()
                        next_display = now + .25
        except KeyboardInterrupt:
            reason = 'interrupted'
        except (OSError, RuntimeError) as exc:
            reason = 'error'
            emit({'kind': 'error', 'seconds': time.monotonic() - start, 'message': str(exc)})
            raise
        finally:
            emit({'kind': 'end', 'frames': count, 'dropped': dropped,
                  'seconds': time.monotonic() - start, 'reason': reason})
    print(f'Saved {count} frames to {path}')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--list', action='store_true', help='List input devices and touchpad candidates as JSON')
    parser.add_argument('--device', help='Explicit /dev/input/eventN path from --list')
    parser.add_argument('--seconds', type=float, default=30)
    parser.add_argument('--output', type=Path, help='New JSONL capture file (or JSON inventory with --list)')
    args = parser.parse_args()
    if sys.platform != 'linux':
        parser.error('This diagnostic requires Linux')
    if not math.isfinite(args.seconds) or not 0 < args.seconds <= 3600:
        parser.error('--seconds must be greater than 0 and at most 3600')
    try:
        from evdev import InputDevice, ecodes as e, list_devices
    except ImportError:
        parser.exit(1, 'Missing evdev. Install python3-evdev using your distribution package manager, or pip install evdev in a virtual environment.\n')
    devices, entries = [], []
    try:
        for path in ([args.device] if args.device else sorted(list_devices())):
            try:
                device = InputDevice(path)
                devices.append(device)
                entries.append((device, describe(device, e)))
            except OSError as exc:
                entries.append((None, {'path': path, 'error': str(exc)}))
        if args.list:
            result = json.dumps({'kind': 'devices', 'devices': [row for _, row in entries]}, indent=2)
            print(result)
            if args.output:
                args.output.parent.mkdir(parents=True, exist_ok=True)
                with args.output.open('x', encoding='utf-8') as log:
                    log.write(result + '\n')
            return 0
        candidates = [(d, row) for d, row in entries if d and (args.device or row['touchpad_candidate'])]
        if len(candidates) != 1:
            raise RuntimeError('Expected one accessible touchpad. Run --list, then --device /dev/input/eventN. '
                               'If permission is denied, run this diagnostic with sudo. ' +
                               '; '.join(row['path'] + ': ' + row['error'] for _, row in entries if 'error' in row))
        device, metadata = candidates[0]
        if not metadata['absolute_xy']:
            raise RuntimeError('Selected device does not expose absolute X/Y positions')
        if metadata['protocol'] == 'A':
            raise RuntimeError('Legacy type A multitouch is not decoded by this first logger; select a type B or single-touch device')
        if metadata['protocol'] == 'single' and e.BTN_TOUCH not in metadata['keys']:
            raise RuntimeError('Single-touch device needs BTN_TOUCH to distinguish touching from lifting')
        capture(device, metadata, args, e)
        return 0
    except (OSError, RuntimeError) as exc:
        print(f'Error: {exc}', file=sys.stderr)
        return 1
    finally:
        for device in devices:
            device.close()


if __name__ == '__main__':
    sys.exit(main())
