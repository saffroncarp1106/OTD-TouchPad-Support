"""Summarize viewer captures; exclude lifts and gaps over 100 ms."""
import csv
import json
import statistics
import sys
from pathlib import Path


def summarize(path):
    rows = list(csv.DictReader(path.open(newline='')))
    arrival, scan = [], []
    changed = 0
    pairs = 0
    for a, b in zip(rows, rows[1:]):
        dt = float(b['arrival_seconds']) - float(a['arrival_seconds'])
        if not a['x'] or not b['x'] or not 0 < dt < 0.1:
            continue
        arrival.append(dt * 1000)
        pairs += 1
        changed += (a['x'], a['y']) != (b['x'], b['y'])
        if a['scan_100us'] and b['scan_100us']:
            ticks = (int(b['scan_100us']) - int(a['scan_100us'])) & 65535
            if 0 < ticks < 1000:
                scan.append(ticks / 10)

    def stats(values):
        if not values:
            return None
        ordered = sorted(values)
        return {'interval_count': len(values), 'mean_hz': round(1000 / statistics.mean(values), 2),
                'median_ms': round(statistics.median(values), 3),
                'p95_ms': round(ordered[int((len(ordered) - 1) * .95)], 3)}

    return {'file': str(path), 'reports': len(rows), 'active_arrival': stats(arrival),
            'active_device_scan': stats(scan),
            'coordinate_change_percent': round(100 * changed / pairs, 2) if pairs else None}


if __name__ == '__main__':
    folder = Path(__file__).resolve().parents[1] / '.local' / 'diagnostics'
    path = Path(sys.argv[1]) if len(sys.argv) > 1 else max(folder.glob('viewer-rate-*.csv'), key=lambda p: p.stat().st_mtime)
    print(json.dumps(summarize(path), indent=2))
