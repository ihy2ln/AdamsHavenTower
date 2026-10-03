"""Bundle reviewed clips for an optional Seedance upgrade pass, and bring Seedance renders back in.

  python Tools/export_seedance_handoff.py kaela                    # every field segment of the fighter
  python Tools/export_seedance_handoff.py kaela AH_skill_hook      # one action's segments
  python Tools/export_seedance_handoff.py cine cine_kaela_*        # match-cut / ultimate cinematics (produce_cine.py jobs)

Writes SeedanceHandoff/<name>/: first.png and last.png (the unmatted keys MiniMax H3 used), prompt.txt, and job.json
(frames, fps, duration, aspect, contact frame). Use them with Seedance's first/last-frame mode, in the browser or
through ComfyUI's ByteDance nodes.

Seedance's shortest clip (4 s on 2.x, 3 s on 1.x) is longer than a field segment (0.4-2 s). Render the minimum length;
the import retimes it to the segment's frame count:
  python Tools/export_seedance_handoff.py import kaela AH_skill_hook 0 path/to/seedance.mp4
  python Tools/produce_fighter_clips.py kaela matte AH_skill_hook
  python Tools/produce_fighter_clips.py kaela pack
  python Tools/export_seedance_handoff.py import-cine cine_kaela_shatter_hook path/to/seedance.mp4
The H3 render is kept next to it as h3_24.orig.mp4 / h3_hi.orig.mp4.
"""
import json
import shutil
import subprocess
import sys
from pathlib import Path

PROJECT = Path(__file__).resolve().parent.parent
WORK = PROJECT / 'BattleMotion'
OUT = PROJECT / 'SeedanceHandoff'
ULT_OUT = PROJECT / 'Assets/Resources/AdamsHaven/UltCutIns'
SPECS = Path(__file__).resolve().parent / 'fighter_clips'


def aspect(w, h):
    r = w / h
    return min(('1:1', 1), ('16:9', 16 / 9), ('9:16', 9 / 16), ('4:3', 4 / 3), ('3:4', 3 / 4), key=lambda a: abs(a[1] - r))[0]


def bundle(name, first, last, prompt, info):
    d = OUT / name
    d.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(first, d / 'first.png')
    shutil.copyfile(last, d / 'last.png')
    (d / 'prompt.txt').write_text(prompt, encoding='utf-8')
    (d / 'job.json').write_text(json.dumps(info, indent=2))
    print('->', d)


def fighter(unit, only=None):
    spec = json.loads((SPECS / f'{unit}.json').read_text(encoding='utf-8'))
    w, h = spec['canvas']
    for action, act in spec['actions'].items():
        if only and action != only:
            continue
        for i, seg in enumerate(act['segments']):
            sd = WORK / f'fighter_{unit}' / 'segments' / f'{action}_{i}'
            if not (sd / 'spec.json').exists():
                continue
            s = json.loads((sd / 'spec.json').read_text())
            bundle(f'{unit}_{action}_{i}', sd / 'key_first.png', sd / 'key_last.png', s['prompt'], dict(
                kind='field segment', unit=unit, action=action, segment=i, first_key=seg[0], last_key=seg[1],
                frames=s['frames'], fps=24, duration_s=round(s['frames'] / 24, 3), size=[w, h], aspect=aspect(w, h),
                contact_is_last_frame=act.get('contact') == seg[1],
                note='Locked camera, character in place, flat grey background. The import retimes any length.'))


def cine(pattern):
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from produce_cine import JOBS  # noqa: E402
    for job, spec in JOBS.items():
        if not (job == pattern or (pattern.endswith('*') and job.startswith(pattern[:-1]))):
            continue
        d = WORK / job
        if not (d / 'key_first.png').exists():
            continue
        w, h = spec['size']
        bundle(job, d / 'key_first.png', d / 'key_last.png', spec['prompt'], dict(
            kind='skill cinematic', card=spec['card'], frames=spec['length'], fps=24,
            duration_s=round(spec['length'] / 24, 3), size=[w, h], aspect=aspect(w, h),
            note='Starts and ends on the same frame so the battle match-cuts in and out of it.'))


def duration(mp4):
    out = subprocess.run(['ffprobe', '-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', str(mp4)],
                         capture_output=True, text=True, check=True)
    return float(out.stdout.strip())


def import_segment(unit, action, index, mp4):
    spec = json.loads((SPECS / f'{unit}.json').read_text(encoding='utf-8'))
    sd = WORK / f'fighter_{unit}' / 'segments' / f'{action}_{index}'
    s = json.loads((sd / 'spec.json').read_text())
    w, h = spec['canvas']
    if (sd / 'h3_24.mp4').exists() and not (sd / 'h3_24.orig.mp4').exists():
        shutil.copyfile(sd / 'h3_24.mp4', sd / 'h3_24.orig.mp4')
    stretch = (s['frames'] / 24) / duration(mp4)
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', str(mp4), '-vf',
                    f'setpts={stretch:.6f}*PTS,fps=24,scale={w}:{h}:flags=lanczos', '-frames:v', str(s['frames']),
                    '-c:v', 'libx264', '-crf', '15', '-pix_fmt', 'yuv420p', '-an', str(sd / 'h3_24.mp4')], check=True)
    s['model'] = 'Seedance (imported ' + Path(mp4).name + ')'
    (sd / 'spec.json').write_text(json.dumps(s, indent=2))
    print('imported ->', sd / 'h3_24.mp4', f'retimed x{stretch:.3f}; now run matte + pack')


def import_cine(job, mp4):
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    from produce_cine import JOBS  # noqa: E402
    d = WORK / job
    if (d / 'h3_hi.mp4').exists() and not (d / 'h3_hi.orig.mp4').exists():
        shutil.copyfile(d / 'h3_hi.mp4', d / 'h3_hi.orig.mp4')
    shutil.copyfile(mp4, d / 'seedance.mp4')
    dst = ULT_OUT / f"{JOBS[job]['card']}.mp4"
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', str(mp4), '-vf', 'scale=1280:720:flags=lanczos',
                    '-c:v', 'libx264', '-crf', '18', '-preset', 'slow', '-pix_fmt', 'yuv420p', '-an',
                    '-movflags', '+faststart', str(dst)], check=True)
    print('imported ->', dst)


if __name__ == '__main__':
    a = sys.argv[1:]
    if a[0] == 'import':
        import_segment(a[1], a[2], int(a[3]), a[4])
    elif a[0] == 'import-cine':
        import_cine(a[1], a[2])
    elif a[0] == 'cine':
        cine(a[1])
    else:
        fighter(a[0], a[1] if len(a) > 1 else None)
