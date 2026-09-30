"""Blender (headless): delete mesh faces that project outside the painting's silhouette, then re-export the FBX.

  blender -b -P Tools/barn_trim.py -- <barn.fbx> <painting.png> <bays> <x0> <x1> <y0> <y1>

Trellis guesses the hidden roof back; from the game's front camera that guess shows up as slabs above the painted roof.
A face whose front-projected centroid lands on a transparent painting pixel (after a small dilation) is removed.
"""
import bpy, sys, bmesh
import numpy as np

fbx, paint, bays, x0, x1, y0, y1 = sys.argv[-7:]
bays = int(bays); x0, x1, y0, y1 = map(float, (x0, x1, y0, y1))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
mesh = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
img = bpy.data.images.load(paint)
W, H = img.size
alpha = np.empty(W * H * 4, np.float32); img.pixels.foreach_get(alpha)
alpha = alpha.reshape(H, W, 4)[:, :, 3][::-1] > 0.08          # row 0 = top of the painting
pad = 10
solid = alpha.copy()
for dy in range(-pad, pad + 1, 2):
    for dx in range(-pad, pad + 1, 2):
        solid |= np.roll(np.roll(alpha, dy, 0), dx, 1)

zs = [v.co.z for v in mesh.data.vertices]; xs = [v.co.x for v in mesh.data.vertices]
tw, th = max(xs) - min(xs), max(zs) - min(zs)
bm = bmesh.new(); bm.from_mesh(mesh.data)
kill = []
for f in bm.faces:
    c = f.calc_center_median()
    px = int(x0 + (c.x / tw + 0.5) * (x1 - x0)); py = int(y1 - (c.z / th) * (y1 - y0))
    if not (0 <= px < W and 0 <= py < H) or not solid[py, px]: kill.append(f)
bmesh.ops.delete(bm, geom=kill, context='FACES')
loose = [v for v in bm.verts if not v.link_faces]
bmesh.ops.delete(bm, geom=loose, context='VERTS')
print('TRIM removed', len(kill), 'of', len(kill) + len(bm.faces), 'faces')
bm.to_mesh(mesh.data); bm.free()
mesh.select_set(True); bpy.context.view_layer.objects.active = mesh
bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', add_leaf_bones=False, mesh_smooth_type='FACE',
                         bake_space_transform=False)
