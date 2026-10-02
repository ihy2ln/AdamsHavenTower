"""Extract the embedded images of a GLB (and report which material slot uses each).

  python Tools/glb_textures.py <model.glb> <out_dir>
"""
import json, struct, sys
from pathlib import Path


def read_glb(path):
    data = Path(path).read_bytes()
    assert data[:4] == b'glTF', 'not a GLB'
    off = 12
    js, binc = None, None
    while off < len(data):
        ln, typ = struct.unpack_from('<II', data, off)
        chunk = data[off + 8: off + 8 + ln]
        if typ == 0x4E4F534A:
            js = json.loads(chunk)
        elif typ == 0x004E4942:
            binc = chunk
        off += 8 + ln
    return js, binc


def main():
    js, binc = read_glb(sys.argv[1])
    out = Path(sys.argv[2]); out.mkdir(parents=True, exist_ok=True)
    use = {}
    for m in js.get('materials', []):
        pbr = m.get('pbrMetallicRoughness', {})
        for slot, ref in [('baseColor', pbr.get('baseColorTexture')), ('metalRough', pbr.get('metallicRoughnessTexture')),
                          ('normal', m.get('normalTexture')), ('emissive', m.get('emissiveTexture'))]:
            if ref:
                src = js['textures'][ref['index']]['source']
                use.setdefault(src, []).append(slot)
    for i, im in enumerate(js.get('images', [])):
        bv = js['bufferViews'][im['bufferView']]
        blob = binc[bv.get('byteOffset', 0): bv.get('byteOffset', 0) + bv['byteLength']]
        ext = '.png' if im.get('mimeType') == 'image/png' else '.jpg'
        name = f"{i}_{'_'.join(use.get(i, ['unused']))}{ext}"
        (out / name).write_bytes(blob)
        print(name, len(blob))
    tris = 0
    for mesh in js.get('meshes', []):
        for p in mesh['primitives']:
            if 'indices' in p:
                tris += js['accessors'][p['indices']]['count'] // 3
    print('triangles', tris, 'skins', len(js.get('skins', [])), 'animations', [a.get('name') for a in js.get('animations', [])])


if __name__ == '__main__':
    main()
