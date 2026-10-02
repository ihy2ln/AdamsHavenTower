"""Convert a ComfyUI UI-format workflow JSON into an API prompt using the target server's /object_info.

  from comfy_ui2api import convert
  prompt = convert(workflow_dict, 'http://127.0.0.1:8190')

Bypassed (mode 4) / muted (mode 2) nodes are dropped and their links rewired through the first matching input where
possible. Note/Markdown/reroute-only nodes are skipped.
"""
import json, urllib.parse, urllib.request

PASSTHROUGH = {'PreviewImage', 'MaskPreview'}      # newer frontends give these an output that mirrors their input
SKIP = {'Note', 'MarkdownNote', 'PreviewImage', 'MaskPreview', 'Preview3DAdvanced', 'PreviewAny', 'Reroute'}
PRIMITIVE = {'INT', 'FLOAT', 'STRING', 'BOOLEAN', 'COMBO', 'COLOR'}


class _Info(dict):
    """Lazy per-type /object_info/<type> (the full listing can reset the connection on busy servers)."""
    def __init__(self, url): super().__init__(); self.url = url
    def __missing__(self, t):
        try:
            d = json.load(urllib.request.urlopen(f'{self.url}/object_info/{urllib.parse.quote(t)}', timeout=120))
        except Exception:
            raise KeyError(t)
        if t not in d: raise KeyError(t)
        self[t] = d[t]
        return d[t]
    def __contains__(self, t):
        try: self[t]; return True
        except KeyError: return False


def _info(url):
    return _Info(url)


def _is_widget(spec):
    t = spec[0]
    return isinstance(t, list) or t in PRIMITIVE or (isinstance(t, str) and t.startswith('COMFY_'))


def convert(wf, url):
    info = _info(url)
    links = {l[0]: l for l in wf['links']}
    nodes = {n['id']: n for n in wf['nodes']}
    prompt = {}
    bypassed = {n['id'] for n in wf['nodes'] if n.get('mode') in (2, 4)}

    def source(link_id):
        _, src, slot, *_ = links[link_id]
        # walk through bypassed nodes: forward their first same-typed linked input
        while src in bypassed or nodes[src]['type'] in PASSTHROUGH:
            n = nodes[src]
            wanted = n['outputs'][slot]['type']
            nxt = next((i for i in n.get('inputs', []) if i.get('link') is not None and i['type'] == wanted), None)
            if nxt is None:
                return None
            _, src, slot, *_ = links[nxt['link']]
        return [str(src), slot]

    for n in wf['nodes']:
        t = n['type']
        if t in SKIP or n['id'] in bypassed:
            continue
        if t not in info:
            raise KeyError(f'node type {t} (id {n["id"]}) is not installed on {url}')
        spec = info[t]['input']
        ordered = list(spec.get('required', {}).items()) + list(spec.get('optional', {}).items())
        wvals = list(n.get('widgets_values') or [])
        inputs = {}
        linked = {i['name']: i['link'] for i in n.get('inputs', []) if i.get('link') is not None}
        for name, (itype, *cfg) in [(k, v) for k, v in ordered]:
            if name in linked:
                src = source(linked[name])
                if src is not None:
                    inputs[name] = src
        for name, sp in ordered:
            if not _is_widget(sp):
                continue
            if not wvals:
                break
            val = wvals.pop(0)
            if name not in inputs:
                inputs[name] = val
            if len(sp) > 1 and isinstance(sp[1], dict) and sp[1].get('control_after_generate') and wvals:
                wvals.pop(0)                                        # 'fixed' / 'randomize' companion widget
        prompt[str(n['id'])] = {'class_type': t, 'inputs': inputs}
    return prompt
