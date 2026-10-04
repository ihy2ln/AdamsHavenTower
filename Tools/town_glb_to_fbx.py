"""Blender (headless) step: Trellis GLB of a town building -> game-ready FBX + baked base-colour PNG.

  blender -b -P Tools/town_glb_to_fbx.py -- <in.glb> <outdir> <name>

The reference pictures are isometric, so Trellis can return the building turned on the ground plane. This squares it
to the grid (the yaw with the smallest footprint rectangle), sets the origin to the bottom centre, and scales it so
the footprint fits a 1 x 1 tile (the TOWN view scales it per rank band). Writes <outdir>/<name>.fbx and <name>.png.
"""
import bpy, sys, math
from mathutils import Vector
from pathlib import Path

glb, outdir, name = sys.argv[-3:]
TARGET_FACES = 30000
outdir = Path(outdir); outdir.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
mesh = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
mesh.name = name
bpy.context.view_layer.objects.active = mesh
mesh.select_set(True)

# Save the baked base colour.
base = None
for node in mesh.active_material.node_tree.nodes:
    if node.type == 'TEX_IMAGE' and node.image:
        for out in node.outputs:
            for link in out.links:
                if link.to_socket.name == 'Base Color': base = node.image
if base is None: raise SystemExit('no base colour texture')
base.colorspace_settings.name = 'sRGB'
sc = bpy.context.scene
sc.view_settings.view_transform = 'Standard'; sc.display_settings.display_device = 'sRGB'
sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGB'
base.save_render(str(outdir / f'{name}.png'), scene=sc)

bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
verts = mesh.data.vertices

# Level it: Trellis reads the picture's 35-degree camera pitch as part of the building, so the model comes out tilted.
# The plinth's underside is the biggest downward-facing area; turn its area-weighted normal to straight down. Twice,
# with a tighter cone the second time, so walls and roof overhangs do not pull the estimate.
for cone in (-0.5, -0.85):
    down = Vector((0, 0, 0))
    for poly in mesh.data.polygons:
        if poly.normal.z < cone: down += poly.normal * poly.area
    if down.length < 1e-9: break
    rot = down.normalized().rotation_difference(Vector((0, 0, -1))).to_matrix()
    for v in verts: v.co = rot @ v.co
    mesh.data.update()
    print('LEVEL', round(math.degrees(down.normalized().angle(Vector((0, 0, -1)))), 1))

# Square to the grid: the yaw (0-89 degrees) whose axis-aligned footprint rectangle is smallest. Blender is Z-up.
step = max(1, len(verts) // 4000)   # Blender collections do not take slice steps
pts = [(verts[i].co.x, verts[i].co.y) for i in range(0, len(verts), step)]
best, best_area = 0, float('inf')
for deg in range(90):
    a = math.radians(deg); c, s = math.cos(a), math.sin(a)
    xs = [x * c - y * s for x, y in pts]; ys = [x * s + y * c for x, y in pts]
    area = (max(xs) - min(xs)) * (max(ys) - min(ys))
    if area < best_area: best, best_area = deg, area
a = math.radians(best); c, s = math.cos(a), math.sin(a)
for v in verts:
    x, y = v.co.x, v.co.y
    v.co.x, v.co.y = x * c - y * s, x * s + y * c
mesh.data.update()

mn = Vector((1e9,) * 3); mx = Vector((-1e9,) * 3)
for v in verts:
    mn = Vector(map(min, mn, v.co)); mx = Vector(map(max, mx, v.co))
size = mx - mn
k = 1.0 / max(size.x, size.y)   # uniform: the footprint's long side becomes 1 tile
for v in verts:
    v.co.x = (v.co.x - (mn.x + mx.x) / 2) * k
    v.co.y = (v.co.y - (mn.y + mx.y) / 2) * k
    v.co.z = (v.co.z - mn.z) * k
mesh.data.update()
print('YAW', best, 'SIZE', size[:], 'HEIGHT', size.z * k)

faces = len(mesh.data.polygons)
mod = mesh.modifiers.new('decimate', 'DECIMATE')
mod.ratio = min(1.0, TARGET_FACES / faces); mod.delimit = {'UV'}
bpy.ops.object.modifier_apply(modifier=mod.name)
print('FACES', faces, '->', len(mesh.data.polygons))

mesh.data.materials.clear()   # Unity builds its own (TowerModelImporter)
bpy.ops.export_scene.fbx(filepath=str(outdir / f'{name}.fbx'), use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', add_leaf_bones=False, mesh_smooth_type='FACE',
                         bake_space_transform=False)
