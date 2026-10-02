"""Stage 2 of Tools/weapon_from_art.py (run inside Blender): height field -> closed textured mesh -> GLB.

blender -b --factory-startup --python Tools/weapon_from_art_mesh.py -- <field.npz> <texture.png> <out.glb>

Grid nodes inside the mask become a front sheet (+h) and a back sheet (-h); nodes on the outline are shared by
both sheets at h=0, so the result is closed. Texture coordinates are the node's image position on both sides.
Units: 1 px = 1 mm; Tools/build_battle_rig.py rescales the prop to its spec length anyway.
"""
import bpy, sys, math, pathlib
import numpy as np
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:]
field, tex_path, out = pathlib.Path(argv[0]), pathlib.Path(argv[1]), pathlib.Path(argv[2])
bpy.ops.wm.read_factory_settings(use_empty=True)
d = np.load(field)
mask, height, step = d['mask'], d['height'], int(d['step'])
H, W = mask.shape
S = 0.001

rows = list(range(0, H, step))
cols = list(range(0, W, step))
inside = np.array([[mask[y, x] for x in cols] for y in rows])
nr, nc = inside.shape


def boundary(i, j):
    for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        a, b = i + di, j + dj
        if a < 0 or b < 0 or a >= nr or b >= nc or not inside[a, b]:
            return True
    return False


verts, uvs_of = [], []
front, back = {}, {}
for i in range(nr):
    for j in range(nc):
        if not inside[i, j]:
            continue
        y, x = rows[i], cols[j]
        X, Z = (x - W / 2) * S, (H - y) * S
        uv = ((x + 0.5) / W, 1 - (y + 0.5) / H)
        if boundary(i, j):
            front[i, j] = back[i, j] = len(verts)
            verts.append((X, 0.0, Z)); uvs_of.append(uv)
        else:
            h = float(height[y, x]) * S
            front[i, j] = len(verts); verts.append((X, -h, Z)); uvs_of.append(uv)   # -Y faces the camera in Blender
            back[i, j] = len(verts); verts.append((X, h, Z)); uvs_of.append(uv)

faces = []
for i in range(nr - 1):
    for j in range(nc - 1):
        quad = [(i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1)]
        have = [q for q in quad if q in front]
        if len(have) < 3:
            continue
        f = [front[q] for q in have]
        b = [back[q] for q in have]
        if len(set(f)) == len(f):
            faces.append(f[::-1])
        if len(set(b)) == len(b) and b != f:
            faces.append(b)

me = bpy.data.meshes.new('Weapon')
me.from_pydata(verts, [], faces)
me.validate()
uv = me.uv_layers.new(name='UVMap')
for poly in me.polygons:
    for li in poly.loop_indices:
        uv.data[li].uv = uvs_of[me.loops[li].vertex_index]
for poly in me.polygons:
    poly.use_smooth = True
obj = bpy.data.objects.new(out.stem, me)
bpy.context.scene.collection.objects.link(obj)

img = bpy.data.images.load(str(tex_path))
mat = bpy.data.materials.new(out.stem)
mat.use_nodes = True
nt = mat.node_tree
bsdf = nt.nodes['Principled BSDF']
tex = nt.nodes.new('ShaderNodeTexImage')
tex.image = img
nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
nt.links.new(tex.outputs['Color'], bsdf.inputs['Emission Color'])
bsdf.inputs['Emission Strength'].default_value = 1.0
bsdf.inputs['Roughness'].default_value = 0.35
me.materials.append(mat)

bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.object.mode_set(mode='OBJECT')
out.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.export_scene.gltf(filepath=str(out), use_selection=True, export_format='GLB', export_image_format='AUTO')
print(f'WEAPON {out.name}: {len(verts)} verts, {len(faces)} faces, {W * S:.3f} x {H * S:.3f} m', flush=True)

# Preview: front, edge-on and three-quarter, textured.
sc = bpy.context.scene
sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'STUDIO'
sc.display.shading.color_type = 'TEXTURE'
sc.render.resolution_x, sc.render.resolution_y = 900, 600
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
sc.collection.objects.link(cam)
sc.camera = cam
cam.data.type = 'ORTHO'
cam.data.ortho_scale = max(W, H) * S * 1.05 * 1.5
c = Vector((0, 0, H * S / 2))
shots = []
for k, az in enumerate((0, 90, 35)):
    a = math.radians(az)
    dvec = Vector((math.sin(a), -math.cos(a), 0.15)).normalized()
    cam.location = c + dvec * 5
    cam.rotation_euler = (-dvec).to_track_quat('-Z', 'Y').to_euler()
    p = out.with_name(f'{out.stem}_view{k}.png')
    sc.render.filepath = str(p)
    bpy.ops.render.render(write_still=True)
    shots.append(p)
print('PREVIEW', ' '.join(str(s) for s in shots), flush=True)
