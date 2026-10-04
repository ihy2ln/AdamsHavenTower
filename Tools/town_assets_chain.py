"""Unattended: render every missing TownPrompts picture, then turn every picture into a game model.
Safe to re-run; both steps skip work already done. Logs to TownModels/chain.log.

  python Tools/town_assets_chain.py            # wait for a running render_town_refs.py first, then do both steps
"""
from pathlib import Path
import subprocess, sys, time

PROJECT = Path(__file__).resolve().parent.parent
LOG = PROJECT / 'TownModels' / 'chain.log'


def running(pattern):
    r = subprocess.run(['powershell', '-NoProfile', '-Command',
                        "Get-CimInstance Win32_Process -Filter \"Name='python.exe'\" | Where-Object { $_.CommandLine -match '" +
                        pattern + "' } | Measure-Object | Select-Object -ExpandProperty Count"], capture_output=True, text=True)
    return int((r.stdout.strip() or '0')) > 0


def step(script, *args):
    with open(LOG, 'a', encoding='utf-8') as log:
        log.write(f'\n=== {script} {" ".join(args)} {time.ctime()}\n'); log.flush()
        code = subprocess.run([sys.executable, '-u', str(PROJECT / 'Tools' / script), *args], cwd=PROJECT,
                              stdout=log, stderr=subprocess.STDOUT).returncode
        log.write(f'=== exit {code} {time.ctime()}\n')
    return code


if __name__ == '__main__':
    LOG.parent.mkdir(exist_ok=True)
    while running('render_town_refs'):
        time.sleep(60)
    for attempt in range(3):            # ComfyUI restarts now and then; each pass resumes where the last stopped
        if step('render_town_refs.py', 'all') == 0: break
        time.sleep(120)
    for attempt in range(3):
        if step('town_to_3d.py', 'all') == 0: break
        time.sleep(120)
