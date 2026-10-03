"""Offline compile check of the Unity project with Unity's bundled Roslyn (no Editor lock, no Library writes).

  python typecheck.py [--add file.cs ...] [--swap Assets/Scripts/X.cs=draft.cs ...]

Builds Assembly-CSharp then Assembly-CSharp-Editor from the generated .csproj files into a temp folder.
--add compiles extra (not yet in the csproj) files into the assembly their path implies (Editor/ -> editor).
--swap replaces a project file with a draft without touching the project.
"""
import re
import subprocess
import sys
import tempfile
from pathlib import Path

PROJECT = Path('S:/AI/Game/Unity AHCG/My project')
EDITOR = Path('S:/AI/Game Engine/Unity/6000.6.3f1/Editor/Data')
DOTNET = EDITOR / 'DotNetSdk/dotnet.exe'
CSC = EDITOR / 'DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll'


def parse(csproj):
    text = csproj.read_text(encoding='utf-8')
    files = [PROJECT / m for m in re.findall(r'<Compile Include="([^"]+)"', text)]
    refs = [Path(m) for m in re.findall(r'<HintPath>([^<]+)</HintPath>', text)]
    projs = re.findall(r'<ProjectReference Include="([^"]+)\.csproj"', text)
    defines = re.search(r'<DefineConstants>([^<]+)</DefineConstants>', text).group(1)
    lang = re.search(r'<LangVersion>([^<]+)</LangVersion>', text).group(1)
    unsafe = '<AllowUnsafeBlocks>True</AllowUnsafeBlocks>' in text
    return files, refs, projs, defines, lang, unsafe


def build(name, extra, swaps, out, more_refs=()):
    files, refs, projs, defines, lang, unsafe = parse(PROJECT / f'{name}.csproj')
    files = [swaps.get(f.resolve(), f) for f in files] + list(extra)
    refs = [r if r.is_absolute() else PROJECT / r for r in refs]
    for p in projs:
        built = out / f'{p}.dll'
        refs.append(built if built.exists() else PROJECT / 'Library/ScriptAssemblies' / f'{p}.dll')
    refs += list(more_refs)
    dll = out / f'{name}.dll'
    rsp = out / f'{name}.rsp'
    lines = ['-nologo', '-noconfig', '-nostdlib+', '-target:library', f'-langversion:{lang}', f'-define:{defines}',
             '-nowarn:0169,0414,0649,0618,0067,0219,0168,0162,0105,1701,1702', f'-out:{dll}']
    if unsafe:
        lines.append('-unsafe')
    lines += [f'-r:"{r}"' for r in refs]
    lines += [f'"{f}"' for f in files]
    rsp.write_text('\n'.join(lines), encoding='utf-8')
    res = subprocess.run([str(DOTNET), str(CSC), f'@{rsp}'], capture_output=True, text=True)
    errors = [l for l in res.stdout.splitlines() if ': error ' in l]
    print(f'{name}: {len(files)} files, exit {res.returncode}, {len(errors)} errors')
    for l in errors[:200]:
        print('  ', l)
    return res.returncode == 0, dll


def main(argv):
    extra_rt, extra_ed, swaps = [], [], {}
    mode = None
    for a in argv:
        if a in ('--add', '--swap'):
            mode = a
        elif mode == '--add':
            p = Path(a).resolve()
            (extra_ed if '/Editor/' in p.as_posix() or p.name.endswith('Tests.cs') or 'Importer' in p.name else extra_rt).append(p)
        elif mode == '--swap':
            target, draft = a.split('=', 1)
            swaps[(PROJECT / target).resolve()] = Path(draft).resolve()
    out = Path(tempfile.mkdtemp(prefix='ahcg-typecheck-'))
    ok, rt = build('Assembly-CSharp', extra_rt, swaps, out)
    if not ok:
        return 1
    ok2, _ = build("Assembly-CSharp-Editor", extra_ed, swaps, out)
    return 0 if ok2 else 1


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
