import argparse
from collections import deque
from datetime import datetime
import json
from pathlib import Path
import subprocess
import sys

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--list', action='store_true')
    parser.add_argument('--device', type=int)
    parser.add_argument('--seconds', type=float, default=30)
    args=parser.parse_args()
    folder=Path(__file__).resolve().parent
    exe=folder/'Touchpad-Probe.exe'
    if not exe.exists():
        raise SystemExit('Touchpad-Probe.exe must be beside this script; use the packaged Surface-Test folder.')
    if args.list:
        return subprocess.call([str(exe),'--list'])
    path=folder/'results'/('live-'+datetime.now().strftime('%Y%m%d-%H%M%S-%f')+'.jsonl')
    path.parent.mkdir(exist_ok=True)
    command=[str(exe),'--seconds',str(args.seconds)]
    if args.device is not None:command+=['--device',str(args.device)]
    print('Move one finger continuously, then lift and repeat. Capture:',path)
    times=deque(maxlen=240)
    next_display=0
    with path.open('w',encoding='utf-8') as log:
        with subprocess.Popen(command,stdout=subprocess.PIPE,text=True,encoding='utf-8') as process:
            try:
                for line in process.stdout:
                    log.write(line)
                    row=json.loads(line)
                    if row['kind']=='frame':
                        now=row['seconds']; times.append(now)
                        if now>=next_display:
                            while times and times[0]<now-2:times.popleft()
                            hz=(len(times)-1)/(times[-1]-times[0]) if len(times)>1 and times[-1]>times[0] else 0
                            print(f"{hz:6.1f} frames/s | contacts={row['count']} | {row['points']}")
                            next_display=now+.25
                    else:print(row)
            except KeyboardInterrupt:

                try:process.wait(timeout=3)
                except subprocess.TimeoutExpired:process.terminate()
            return process.wait()

if __name__=='__main__':sys.exit(main())
