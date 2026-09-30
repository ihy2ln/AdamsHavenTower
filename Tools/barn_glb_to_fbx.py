"""Blender (headless) step: Trellis GLB -> game-ready FBX + texture PNGs.

  blender -b -P Tools/barn_glb_to_fbx.py -- <in.glb> <outdir> <tier> <bays> <aspect_h_over_w>

Keeps the open front on -Y (toward the ortho camera), sets the origin to bottom-centre and scales to
bays*2 world units wide (one tower Cell = 2 units), height from the painting's aspect, 1.1 units deep.
Writes <outdir>/barn_<tier>.fbx, barn_<tier>_base.png, barn_<tier>_normal.png.
"""
import bpy, sys, math
from mathutils import Vector
from pathlib import Path

glb, outdir, tier, bays, aspect = sys.argv[-5:]
bays, aspect = int(bays), float(aspect)
TARGET_FACES = 45000
outdir = Path(outdir); outdir.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
mesh = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
mesh.name = f'barn_{tier}'
bpy.context.view_layer.objects.active = mesh
mesh.select_set(True)

# save textures from the material
mat = mesh.active_material
base = normal = None
for node in mat.node_tree.nodes:
    if node.type == 'TEX_IMAGE' and node.image:
        for out in node.outputs:
            for link in out.links:
                sock = link.to_socket.name
                if sock == 'Base Color': base = node.image
                elif link.to_node.type == 'NORMAL_MAP': normal = node.image
for name, img in (('base', base), ('normal', normal)):
    if img is None: print('WARN no', name); continue
    img.colorspace_settings.name = 'sRGB'          # round-trips bytes unchanged through save_render
    sc = bpy.context.scene
    sc.view_settings.view_transform = 'Standard'; sc.display_settings.display_device = 'sRGB'
    sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGB'
    img.save_render(str(outdir / f'barn_{tier}_{name}.png'), scene=sc)

# Trellis paints the gable end toward +X in Blender space; turn the painted front to face -Y (the game camera).
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for v in mesh.data.vertices:
    v.co.x, v.co.y = v.co.y, -v.co.x
mesh.data.update()
mn = Vector((1e9,) * 3); mx = Vector((-1e9,) * 3)
for v in mesh.data.vertices:
    mn = Vector(map(min, mn, v.co)); mx = Vector(map(max, mx, v.co))
size = mx - mn
tw, th, td = bays * 2.0, bays * 2.0 * aspect, 1.1
for v in mesh.data.vertices:
    v.co.x = (v.co.x - (mn.x + mx.x) / 2) * tw / size.x
    v.co.y = (v.co.y - (mn.y + mx.y) / 2) * td / size.y
    v.co.z = (v.co.z - mn.z) * th / size.z
mesh.data.update()
print('SIZE', tier, size[:], '->', tw, td, th)

# The tower shows many barns at once (and ships on Android): cut the 120k-face bake mesh down, keeping UV seams.
faces = len(mesh.data.polygons)
mod = mesh.modifiers.new('decimate', 'DECIMATE')
mod.ratio = min(1.0, TARGET_FACES / faces); mod.delimit = {'UV'}
bpy.ops.object.modifier_apply(modifier=mod.name)
print('FACES', faces, '->', len(mesh.data.polygons))

# Unity imports its own materials
mesh.data.materials.clear()
bpy.ops.export_scene.fbx(filepath=str(outdir / f'barn_{tier}.fbx'), use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', add_leaf_bones=False, mesh_smooth_type='FACE',
                         bake_space_transform=False)
