"""Split detached weapons/props off a Meshy character so the body can be auto-rigged on its own.

blender -b --factory-startup --python Tools/split_weapon.py -- <in.glb> <out_dir> <name> [max_tex=2048]

Meshy's humanoid auto-rig fails when a weapon stands beside the character, so the body is rigged alone
and the weapon is attached to a hand bone afterwards (Tools/build_battle_rig.py).
Vertices are welded first (glTF splits them at UV seams), then the mesh is separated into loose parts.
The largest part is the body; every other part is written to <name>_props.glb, one object per part.
<name>_body.glb gets JPEG textures capped at max_tex so it stays under the 10 MB browser upload limit.
"""
import bpy, sys, json, pathlib
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:]
src, out_dir, name = argv[0], pathlib.Path(argv[1]), argv[2]
max_tex = int(argv[3]) if len(argv) > 3 else 2048
out_dir.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
obj = next(o for o in bpy.data.objects if o.type == 'MESH')
bpy.context.view_layer.objects.active = obj
obj.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.remove_doubles(threshold=0.0005)
bpy.ops.mesh.separate(type='LOOSE')
bpy.ops.object.mode_set(mode='OBJECT')

parts = sorted([o for o in bpy.data.objects if o.type == 'MESH'], key=lambda o: len(o.data.polygons), reverse=True)
body, props = parts[0], parts[1:]
body.name = name + '_body'


def bounds(o):
    ws = [o.matrix_world @ Vector(c) for c in o.bound_box]
    return [round(min(v[i] for v in ws), 4) for i in range(3)], [round(max(v[i] for v in ws), 4) for i in range(3)]


report = {'source': src, 'body': {'faces': len(body.data.polygons), 'bounds': bounds(body)}, 'props': []}
for i, p in enumerate(props):
    p.name = f'{name}_prop{i}'
    report['props'].append({'name': p.name, 'faces': len(p.data.polygons), 'bounds': bounds(p)})

for img in bpy.data.images:
    if img.size[0] > max_tex:
        img.scale(max_tex, max(1, int(img.size[1] * max_tex / img.size[0])))


def export(objs, path):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(path), use_selection=True, export_format='GLB',
                              export_image_format='JPEG', export_jpeg_quality=88, export_apply=True)


export([body], out_dir / f'{name}_body.glb')
if props:
    export(props, out_dir / f'{name}_props.glb')
(out_dir / f'{name}_split.json').write_text(json.dumps(report, indent=2))
print('SPLIT', json.dumps(report), flush=True)
