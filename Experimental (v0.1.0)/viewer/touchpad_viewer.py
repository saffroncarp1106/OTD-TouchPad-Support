from __future__ import annotations
import ctypes as C
from ctypes import wintypes as W
import struct
import sys
import time
import tkinter as tk
from collections import deque
from pathlib import Path
import csv

if sys.platform != "win32":
    raise SystemExit("This viewer requires Windows.")

user32 = C.WinDLL("user32", use_last_error=True)
hid = C.WinDLL("hid", use_last_error=True)
winmm = C.WinDLL('winmm')
for name in ('timeBeginPeriod', 'timeEndPeriod'):
    getattr(winmm, name).argtypes = [W.UINT]
    getattr(winmm, name).restype = W.UINT

try:
    user32.SetProcessDpiAwarenessContext.argtypes = [C.c_void_p]
    user32.SetProcessDpiAwarenessContext(C.c_void_p(-4))
except AttributeError:
    user32.SetProcessDPIAware()
UINT_ERROR = 0xFFFFFFFF
SUCCESS = 0x110000
LRESULT = C.c_ssize_t
WPARAM = C.c_size_t
LPARAM = C.c_ssize_t
WNDPROC = C.WINFUNCTYPE(LRESULT, W.HWND, W.UINT, WPARAM, LPARAM)

class DeviceList(C.Structure):
    _fields_ = [("handle", W.HANDLE), ("type", W.DWORD)]

class Registration(C.Structure):
    _fields_ = [("page", W.USHORT), ("usage", W.USHORT),
                ("flags", W.DWORD), ("target", W.HWND)]

class Header(C.Structure):
    _fields_ = [("type", W.DWORD), ("size", W.DWORD),
                ("device", W.HANDLE), ("wparam", WPARAM)]

def declare(lib, name, args, result):
    fn = getattr(lib, name)
    fn.argtypes, fn.restype = args, result
    return fn

get_list = declare(user32, "GetRawInputDeviceList", [C.c_void_p, C.POINTER(W.UINT), W.UINT], W.UINT)
get_info = declare(user32, "GetRawInputDeviceInfoW", [W.HANDLE, W.UINT, C.c_void_p, C.POINTER(W.UINT)], W.UINT)
get_input = declare(user32, "GetRawInputData", [W.HANDLE, W.UINT, C.c_void_p, C.POINTER(W.UINT), W.UINT], W.UINT)
register = declare(user32, "RegisterRawInputDevices", [C.POINTER(Registration), W.UINT, W.UINT], W.BOOL)
get_ancestor = declare(user32, "GetAncestor", [W.HWND, W.UINT], W.HWND)
set_proc = declare(user32, "SetWindowLongPtrW" if C.sizeof(C.c_void_p) == 8 else "SetWindowLongW", [W.HWND, C.c_int, C.c_void_p], C.c_void_p)
call_proc = declare(user32, "CallWindowProcW", [C.c_void_p, W.HWND, W.UINT, WPARAM, LPARAM], LRESULT)
get_caps = declare(hid, "HidP_GetCaps", [C.c_void_p, C.c_void_p], C.c_long)
get_value_caps = declare(hid, "HidP_GetValueCaps", [C.c_int, C.c_void_p, C.POINTER(W.USHORT), C.c_void_p], C.c_long)
get_value = declare(hid, "HidP_GetUsageValue", [C.c_int, W.USHORT, W.USHORT, W.USHORT, C.POINTER(W.ULONG), C.c_void_p, C.c_void_p, W.ULONG], C.c_long)
get_usages = declare(hid, "HidP_GetUsages", [C.c_int, W.USHORT, W.USHORT, C.c_void_p, C.POINTER(W.ULONG), C.c_void_p, C.c_void_p, W.ULONG], C.c_long)

class Touchpad:
    def __init__(self, handle, path):
        self.handle, self.path = handle, path
        size = W.UINT()
        if get_info(handle, 0x20000005, None, C.byref(size)) == UINT_ERROR or not size.value:
            raise C.WinError(C.get_last_error())
        self.descriptor = C.create_string_buffer(size.value)
        if get_info(handle, 0x20000005, self.descriptor, C.byref(size)) == UINT_ERROR:
            raise C.WinError(C.get_last_error())
        caps = C.create_string_buffer(64)
        if get_caps(self.descriptor, caps) != SUCCESS:
            raise RuntimeError("Cannot read touchpad capabilities.")
        count = W.USHORT(struct.unpack_from('<H', caps.raw, 48)[0])
        values = C.create_string_buffer(count.value * 72)
        if get_value_caps(0, values, C.byref(count), self.descriptor) != SUCCESS:
            raise RuntimeError("Cannot read touchpad coordinate ranges.")
        self.axes = {}
        self.scan_link = None
        for i in range(count.value):
            cap = values.raw[i * 72:(i + 1) * 72]
            page, link, usage = (struct.unpack_from('<H', cap, p)[0] for p in (0, 6, 56))
            if page == 13 and not cap[12] and usage == 0x56:
                self.scan_link = link
            if page == 1 and not cap[12] and cap[15] and usage in (0x30, 0x31):
                self.axes[usage] = (link, *struct.unpack_from('<ii', cap, 40))
        if set(self.axes) != {0x30, 0x31}:
            raise RuntimeError("No absolute X/Y axes were found.")
        if any(high <= low for _, low, high in self.axes.values()):
            raise RuntimeError("Invalid touchpad coordinate range.")
        self.multitouch = False

    def value(self, report, page, link, usage):
        value = W.ULONG()
        if get_value(0, page, link, usage, C.byref(value), self.descriptor, report, len(report)) != SUCCESS:
            return None
        return value.value

    def decode(self, raw):
        report = C.create_string_buffer(raw, len(raw))
        count = self.value(report, 13, 0, 0x54)
        if count:
            self.multitouch = count > 1
        if self.multitouch:
            return None
        link = self.axes[0x30][0]
        usages, size = (W.USHORT * 32)(), W.ULONG(32)
        if get_usages(0, 13, link, usages, C.byref(size), self.descriptor, report, len(raw)) != SUCCESS:
            return None
        pressed = set(usages[:size.value])
        if not {0x42, 0x47}.issubset(pressed):
            return None
        x = self.value(report, 1, link, 0x30)
        y = self.value(report, 1, self.axes[0x31][0], 0x31)
        return None if x is None or y is None else (x, y)

    def normalized(self, point):
        return tuple(max(0.0, min(1.0, (v - self.axes[u][1]) / (self.axes[u][2] - self.axes[u][1])))
                     for v, u in zip(point, (0x30, 0x31)))


def find_touchpad():
    count = W.UINT()
    if get_list(None, C.byref(count), C.sizeof(DeviceList)) == UINT_ERROR:
        raise C.WinError(C.get_last_error())
    devices = (DeviceList * count.value)()
    result = get_list(devices, C.byref(count), C.sizeof(DeviceList))
    if result == UINT_ERROR:
        raise C.WinError(C.get_last_error())
    for device in devices[:result]:
        if device.type != 2:
            continue
        size = W.UINT()
        get_info(device.handle, 0x20000007, None, C.byref(size))
        if not size.value:
            continue
        name = C.create_unicode_buffer(size.value + 1)
        if get_info(device.handle, 0x20000007, name, C.byref(size)) == UINT_ERROR:
            continue
        if 'ASUE1209&COL02' in name.value.upper():
            return Touchpad(device.handle, name.value)
    return None


class Viewer:
    BG, INK, MUTED, ACCENT = '#f5f5f3', '#202725', '#56625d', '#137859'

    def __init__(self, root):
        self.root = root
        root.title(' ')
        root.geometry('720x610')
        root.minsize(480, 440)
        root.configure(bg=self.BG)
        self.device = None
        self.point = None
        self.last_report = 0.0
        self.reports = 0
        self.dirty = True
        self.error = None
        self.rescan_pending = False
        self.report_times = deque()
        self.update_times = deque()
        self.samples = deque(maxlen=120000)
        self.scan_hz = None
        self.previous_scan = None
        self.scan_intervals = deque(maxlen=240)
        self.started = time.perf_counter()
        self.input_pending = False
        self.next_status = self.started
        self.status = tk.StringVar(value='Update: measuring | Reports: waiting for touch | Scan: waiting')
        tk.Label(root, textvariable=self.status, bg=self.BG, fg=self.INK).pack(side='bottom', pady=8)
        self.canvas = tk.Canvas(root, bg=self.BG, highlightthickness=0, width=1, height=1)
        self.canvas.pack(fill='both', expand=True)
        self.box = self.canvas.create_rectangle(0, 0, 0, 0, fill='#ffffff', outline='#74837a', width=2)
        self.dot = self.canvas.create_oval(0, 0, 0, 0, fill=self.ACCENT, outline='white', width=2, state='hidden')
        self.canvas.bind('<Configure>', lambda _: self.redraw())
        root.update_idletasks()
        self.hwnd = get_ancestor(root.winfo_id(), 2)
        self.callback = WNDPROC(self.window_proc)
        C.set_last_error(0)
        self.previous = set_proc(self.hwnd, -4, C.cast(self.callback, C.c_void_p))
        if not self.previous:
            raise C.WinError(C.get_last_error())
        entry = Registration(13, 5, 0x2100, self.hwnd)
        if not register(C.byref(entry), 1, C.sizeof(entry)):
            set_proc(self.hwnd, -4, self.previous)
            raise C.WinError(C.get_last_error())
        root.protocol('WM_DELETE_WINDOW', self.close)
        self.rescan()
        self.precise_timer = winmm.timeBeginPeriod(1) == 0
        self.timer = root.after(1, self.refresh)

    def rescan(self):
        self.point, self.error = None, None
        try:
            self.device = find_touchpad()
        except Exception as exc:
            self.device = None
            self.error = str(exc)
        self.dirty = True

    def window_proc(self, hwnd, message, wparam, lparam):
        try:
            if message == 0x00FE:
                self.rescan_pending = True
            elif message == 0x00FF and self.device:
                self.read(lparam)
        except Exception as exc:
            self.error = str(exc)
        return call_proc(self.previous, hwnd, message, wparam, lparam)

    def read(self, handle):
        size = W.UINT()
        header_size = C.sizeof(Header)
        if get_input(handle, 0x10000003, None, C.byref(size), header_size) == UINT_ERROR:
            return
        if not header_size + 8 <= size.value <= 1048576:
            return
        buffer = C.create_string_buffer(size.value)
        if get_input(handle, 0x10000003, buffer, C.byref(size), header_size) == UINT_ERROR:
            return
        header = Header.from_buffer_copy(buffer)
        if header.type != 2 or header.device != self.device.handle:
            return
        length, count = struct.unpack_from('<II', buffer.raw, header_size)
        if not length or count * length > size.value - header_size - 8:
            return
        for i in range(count):
            offset = header_size + 8 + length * i
            raw = buffer.raw[offset:offset + length]
            arrived = time.perf_counter()
            self.point = self.device.decode(raw)
            scan = None
            if self.device.scan_link is not None:
                report = C.create_string_buffer(raw, len(raw))
                scan = self.device.value(report, 13, self.device.scan_link, 0x56)
            if scan is not None and self.previous_scan is not None:
                delta = (scan - self.previous_scan) & 0xffff
                if 0 < delta < 1000 and self.point:
                    self.scan_intervals.append(delta)
                    self.scan_hz = 10000 * len(self.scan_intervals) / sum(self.scan_intervals)
            self.previous_scan = scan if self.point else None
            self.report_times.append(arrived)
            self.samples.append((arrived - self.started, scan, *(self.point or ('', '')), raw.hex()))
            self.reports += 1
        self.last_report = time.monotonic()
        self.dirty = True


        self.input_pending = True

    def draw_latest(self):
        self.input_pending = False
        self.redraw()
        self.dirty = False
        self.update_times.append(time.perf_counter())

    def refresh(self):
        now = time.perf_counter()
        for times in (self.update_times, self.report_times):
            while times and times[0] < now - 2:
                times.popleft()
        if now >= self.next_status:
            def rate(times):
                return (len(times) - 1) / (times[-1] - times[0]) if len(times) > 1 and times[-1] > now - 0.25 else 0.0
            scan_text = f'{self.scan_hz:.1f} Hz' if self.scan_hz and self.point else 'waiting'
            self.status.set(f'Update: {rate(self.update_times):.1f} Hz (input-driven) | Reports: {rate(self.report_times):.1f} Hz | Scan: {scan_text}')
            self.next_status = now + 0.25
        if self.rescan_pending:
            self.rescan_pending = False
            self.rescan()
        if self.error:
            self.point = None
            self.dirty = True
            self.error = None
        if self.point and time.monotonic() - self.last_report > 0.5:
            self.point = None
            self.dirty = True
        if self.input_pending:
            self.draw_latest()
        elif self.dirty:
            self.redraw()
            self.dirty = False

        self.timer = self.root.after(1, self.refresh)

    def redraw(self):
        c = self.canvas
        available_w, available_h = max(1, c.winfo_width() - 56), max(1, c.winfo_height() - 60)
        width = min(available_w, available_h * 126.9 / 82)
        height = width * 82 / 126.9
        left, top = (c.winfo_width() - width) / 2, (c.winfo_height() - height) / 2
        right, bottom = left + width, top + height
        c.coords(self.box, left, top, right, bottom)
        if self.point and self.device:
            nx, ny = self.device.normalized(self.point)
            x, y = left + nx * width, top + ny * height
            c.coords(self.dot, x - 8, y - 8, x + 8, y + 8)
            c.itemconfigure(self.dot, state='normal')
        else:
            c.itemconfigure(self.dot, state='hidden')

    def close(self):
        self.root.after_cancel(self.timer)
        if self.precise_timer:
            winmm.timeEndPeriod(1)
            self.precise_timer = False
        if self.samples:
            folder = Path(__file__).resolve().parents[1] / '.local' / 'diagnostics'
            folder.mkdir(parents=True, exist_ok=True)
            with (folder / f'viewer-rate-{time.time_ns()}.csv').open('w', newline='') as stream:
                writer = csv.writer(stream)
                writer.writerow(('arrival_seconds', 'scan_100us', 'x', 'y', 'raw_hex'))
                writer.writerows(self.samples)
        entry = Registration(13, 5, 1, None)
        register(C.byref(entry), 1, C.sizeof(entry))
        set_proc(self.hwnd, -4, self.previous)
        self.root.destroy()


def main():
    if '--check' in sys.argv:
        Pad = find_touchpad()
        if not Pad:
            raise SystemExit('ASUE1209 touchpad not found.')
        print('Connected:', Pad.path)
        print('Axes:', Pad.axes)
        if '--verify' in sys.argv:
            import re
            path = sys.argv[sys.argv.index('--verify') + 1]
            checked = 0
            with open(path, encoding='utf-8-sig') as stream:
                for line in stream:
                    match = re.search(r'Z ([0-9A-F]+) TouchPoint', line)
                    if not match:
                        continue
                    raw = bytes.fromhex(match[1])
                    point = Pad.decode(raw)
                    assert point == struct.unpack_from('<HH', raw, 2), (point, raw.hex())
                    checked += 1
            assert checked > 0
            print(f'PASS: {checked} real contact reports decode correctly.')
        return
    root = tk.Tk()
    try:
        Viewer(root)
        root.mainloop()
    except Exception as exc:
        from tkinter import messagebox
        messagebox.showerror('Touchpad viewer', str(exc), parent=root)
        root.destroy()
        raise

if __name__ == '__main__':
    main()

