"""Render frames of a built tower rig (<id>-tower.blend) for review.

blender -b <job>/<id>-tower.blend --python Tools/tower_rig_preview.py -- <out_dir> <yaw_deg> <frames> <clip> [clip ...]
yaw 0 = straight front, 90 = the character's right side. Writes <clip>_<k>.png (512 px, Workbench, flat texture)
and shows only the props that clip uses (same table as build_tower_rig.CLIP_PROPS).
"""
import bpy, sys, math, pathlib
from mathutils import Vector

argv = sys.argv[sys.argv.index('--') + 1:]
out, yaw, frames, clips = pathlib.Path(argv[0]), math.radians(float(argv[1])), int(argv[2]), argv[3:]
out.mkdir(parents=True, exist_ok=True)
CLIP_PROPS = {
    'AH_task_forge': ['Prop_Hammer', 'Set_Anvil'], 'AH_task_kitchen': ['Prop_Ladle', 'Set_Pot'],
    'AH_task_well': ['Set_Rope'], 'AH_task_lumber': ['Prop_Axe'], 'AH_task_quarry': ['Prop_Pickaxe'],
    'AH_task_tavern': ['Prop_Mug'], 'AH_sit': ['Set_Chair'], 'AH_sleep': ['Set_Bed'],
}
scene = bpy.context.scene
arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
body = bpy.data.objects['Body']
height = max((body.matrix_world @ v.co).z for v in body.data.vertices)
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'TEXTURE'
scene.display.shading.show_object_outline = True
scene.render.resolution_x = scene.render.resolution_y = 512
cd = bpy.data.cameras.new('cam'); cd.type = 'ORTHO'; cd.ortho_scale = height * 1.12
cam = bpy.data.objects.new('cam', cd); scene.collection.objects.link(cam)
cam.location = Vector((-math.sin(yaw) * 4, -math.cos(yaw) * 4, height * 0.52))
cam.rotation_euler = (math.radians(90), 0, -yaw)
scene.camera = cam
props = [o.name for o in bpy.data.objects if o.name.startswith(('Prop_', 'Set_'))]
for clip in clips:
    act = bpy.data.actions.get(clip)
    if act is None:
        print('missing', clip); continue
    arm.animation_data.action = act
    if hasattr(arm.animation_data, 'action_slot') and len(act.slots):
        arm.animation_data.action_slot = act.slots[0]
    for p in props:
        bpy.data.objects[p].hide_render = p not in CLIP_PROPS.get(clip, [])
    f0, f1 = act.frame_range
    for k in range(frames):
        scene.frame_set(int(round(f0 + (f1 - f0) * k / frames)))
        scene.render.filepath = str(out / f'{clip}_{k}.png')
        bpy.ops.render.render(write_still=True)
print('PREVIEW done', out)
