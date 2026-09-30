"""Convert a ComfyUI UI-format workflow JSON to API prompt format via /object_info. Usage: ui2api.py in.json out.json"""
import json, sys, urllib.request
URL = 'http://127.0.0.1:8188'
w = json.load(open(sys.argv[1]))
oi = {}
def info(t):
    if t not in oi:
        oi[t] = json.load(urllib.request.urlopen(f'{URL}/object_info/{t}'))[t]
    return oi[t]
SKIP = {'Note', 'MarkdownNote', 'Preview3DAdvanced', 'Save3DAdvanced', 'PrimitiveNode'}
links = {l[0]: l for l in w['links']}
nodes = {n['id']: n for n in w['nodes']}
def resolve(nid, slot):
    """Follow through muted/bypassed nodes is not needed here; return [id, slot]."""
    return [str(nid), slot]
out = {}
for n in w['nodes']:
    t = n['type']
    if t in SKIP or n.get('mode') in (2, 4):
        continue
    d = info(t)
    ins = {}
    linked = {}
    for i in n.get('inputs', []):
        if i.get('link') is not None:
            l = links[i['link']]
            linked[i['name']] = resolve(l[1], l[2])
    wv = list(n.get('widgets_values') or [])
    order = list(d['input'].get('required', {}).items()) + list(d['input'].get('optional', {}).items())
    for name, spec in order:
        typ = spec[0]
        is_widget = isinstance(typ, list) or typ in ('INT', 'FLOAT', 'STRING', 'BOOLEAN', 'COMBO')
        if name in linked:
            ins[name] = linked[name]
            if is_widget and wv and any(i['name'] == name and 'widget' in i for i in n['inputs']):
                wv.pop(0)
            continue
        if is_widget:
            if not wv: continue
            v = wv.pop(0)
            ins[name] = v
            if len(spec) > 1 and isinstance(spec[1], dict) and spec[1].get('control_after_generate') and wv:
                wv.pop(0)
    for k, v in linked.items():
        ins.setdefault(k, v)
    out[str(n['id'])] = {'class_type': t, 'inputs': ins, '_meta': {'title': n.get('title') or t}}
# drop links to skipped nodes
for k, nd in out.items():
    for name, v in list(nd['inputs'].items()):
        if isinstance(v, list) and len(v) == 2 and isinstance(v[0], str) and v[0] not in out:
            del nd['inputs'][name]
json.dump(out, open(sys.argv[2], 'w'), indent=1)
print(len(out), 'nodes')
