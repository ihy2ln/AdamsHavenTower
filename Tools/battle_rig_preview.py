"""Render battle-camera frames of rig clips for review (contact sheets are assembled by the caller).

blender -b --factory-startup --python Tools/battle_rig_preview.py -- <model.glb|.fbx|.blend> <out_dir> <frames> [clip ...]

Camera matches the Unity battle field: orthographic, the model faces screen-right in a three-quarter view
(BattleModels.cs turns the -Y-forward export 155 degrees). Each frame is written as <clip>_<NN>.png.
With no clip names every action is rendered. Workbench + texture colour keeps it fast and deterministic.
"""
import bpy, sys, math, pathlib
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:]
src, out, frames, clips = argv[0], pathlib.Path(argv[1]), int(argv[2]), argv[3:]
out.mkdir(parents=True, exist_ok=True)

if src.endswith('.blend'):
    bpy.ops.wm.open_mainfile(filepath=src)
else:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if src.endswith('.fbx'):
        bpy.ops.import_scene.fbx(filepath=src)
    else:
        bpy.ops.import_scene.gltf(filepath=src)
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name.startswith('Icosphere'):
        bpy.data.objects.remove(o, do_unlink=True)
    elif o.type in {'CAMERA', 'LIGHT'}:
        bpy.data.objects.remove(o, do_unlink=True)

arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
meshes = [o for o in bpy.data.objects if o.type == 'MESH' and o.visible_get()]
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'TEXTURE'
scene.display.shading.show_cavity = False
scene.render.film_transparent = False
scene.world = scene.world or bpy.data.worlds.new('World')
import os
scene.render.resolution_x = scene.render.resolution_y = int(os.environ.get('PREVIEW_RES', 384))
ZOOM = float(os.environ.get('PREVIEW_ZOOM', 1.0))
scene.render.image_settings.file_format = 'PNG'


def bounds():
    bpy.context.view_layer.update()
    pts = []
    dg = bpy.context.evaluated_depsgraph_get()
    for o in meshes:
        ev = o.evaluated_get(dg)
        m = ev.to_mesh()
        pts += [o.matrix_world @ m.vertices[i].co for i in range(0, len(m.vertices), 25)]
        ev.to_mesh_clear()
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


arm.animation_data_create()
arm.animation_data.action = None
for pb in arm.pose.bones:
    pb.matrix_basis.identity()
lo, hi = bounds()
height = hi.z - lo.z
center = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z + height * 0.5))

cam_data = bpy.data.cameras.new('BattleCam')
cam_data.type = 'ORTHO'
cam_data.ortho_scale = height * 1.5 / ZOOM   # same framing as the 3-unit ortho camera on a 2-unit model
cam = bpy.data.objects.new('BattleCam', cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
ang = math.radians(25)                        # 25 degrees from profile toward the front
d = Vector((-math.cos(ang), -math.sin(ang), 0.0)) * (height * 4)
cam.location = center + d
cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()

names = clips or [a.name for a in bpy.data.actions]
for name in names:
    act = bpy.data.actions.get(name)
    if not act:
        print('MISSING', name, flush=True)
        continue
    arm.animation_data.action = act
    if hasattr(arm.animation_data, 'action_slot') and act.slots:
        arm.animation_data.action_slot = act.slots[0]
    f0, f1 = act.frame_range
    for i in range(frames):
        f = f0 + (f1 - f0) * i / max(1, frames - 1)
        scene.frame_set(int(f), subframe=f - int(f))
        scene.render.filepath = str(out / f"{name.split('|')[-1]}_{i:02d}.png")
        bpy.ops.render.render(write_still=True)
    print('RENDERED', name, round(f0, 1), round(f1, 1), flush=True)
