"""Cut a staff/spear that is fused into a character mesh out as its own prop GLB.

blender -b --factory-startup --python Tools/extract_column_prop.py -- <in.glb> <out.glb> <side:+x|-x> [shaft_r=0.035] [head_r=0.09] [head_from=0.55]

The shaft's lower end hangs below the knees, outside the legs; its centroid gives the vertical axis. Every vertex
within shaft_r of that axis (head_r above head_from of the prop's height, for the flame/blade head) is kept.
The few hand vertices around the grip come along; they are hidden inside the rigged hand.
"""
import bpy, bmesh, sys
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
src, dst, side = a[0], a[1], a[2]
shaft_r = float(a[3]) if len(a) > 3 else 0.035
head_r = float(a[4]) if len(a) > 4 else 0.09
head_from = float(a[5]) if len(a) > 5 else 0.55

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
obj = next(o for o in bpy.data.objects if o.type == 'MESH')
bpy.context.view_layer.objects.active = obj
obj.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
me = obj.data
zs = [v.co.z for v in me.vertices]
zmin, zmax = min(zs), max(zs)
sx = 1 if side == '+x' else -1
xs = [v.co.x * sx for v in me.vertices]
xmax = max(xs)
low = [v.co for v in me.vertices if v.co.z < zmin + (zmax - zmin) * 0.25 and v.co.x * sx > xmax * 0.55]
axis = sum(low, Vector()) / len(low)
print('AXIS', round(axis.x, 3), round(axis.y, 3), 'from', len(low), 'verts', flush=True)
keep = []
for v in me.vertices:
    d = ((v.co.x - axis.x) ** 2 + (v.co.y - axis.y) ** 2) ** 0.5
    if d < shaft_r:
        keep.append(v.index)
kz = [me.vertices[i].co.z for i in keep]
top = max(kz)
bot = min(kz)
head_z = bot + (top - bot) * head_from
keep = set(keep) | {v.index for v in me.vertices if v.co.z > head_z and
                    ((v.co.x - axis.x) ** 2 + (v.co.y - axis.y) ** 2) ** 0.5 < head_r}
bm = bmesh.new()
bm.from_mesh(me)
bm.verts.ensure_lookup_table()
bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in keep], context='VERTS')
bm.to_mesh(me)
bm.free()
print('KEPT', len(me.vertices), 'verts', len(me.polygons), 'faces', flush=True)
bpy.ops.export_scene.gltf(filepath=dst, export_format='GLB', use_selection=True, export_image_format='JPEG')
