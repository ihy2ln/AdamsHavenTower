"""Retarget the CZN-referenced battle actions onto a new Meshy battle-outfit rig and export for Unity.

blender -b --factory-startup --python Tools/retarget_anime_battle.py -- <unit> <rigged.glb> <old battle.glb> [extra motion .glb ...]

<rigged.glb>   Meshy auto-rig of the anime battle-outfit model (Meshy 24-bone humanoid).
<old battle.glb>  chibi-shared-3d-v2/<character>/battle-motion-v1/battle.glb: nine AH_* actions on the same bone names.
extra motion   Meshy preset/AI motions exported from the same rig; the file stem becomes the clip name
               (e.g. AH_hit_react.glb, AH_death.glb, AH_victory.glb).

Bones are matched by name. Each frame copies the old bone's world direction and roll (corrected for the
two rigs' different rest poses), then the hips translation is scaled by the height ratio.
Output: Assets/Resources/AdamsHaven/BattleModels/<unit>/model.fbx + textures + provenance.json.
"""
import bpy, sys, json, pathlib, math
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index('--') + 1:]
unit, rig_path, old_path, extras = argv[0], argv[1], argv[2], argv[3:]
OUT = pathlib.Path(__file__).resolve().parent.parent / 'Assets' / 'Resources' / 'AdamsHaven' / 'BattleModels' / unit


def import_glb(path):
    before = set(bpy.data.objects)
    acts = set(bpy.data.actions)
    bpy.ops.import_scene.gltf(filepath=str(path))
    new = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in new if o.type == 'ARMATURE')
    return arm, new, [a for a in bpy.data.actions if a not in acts]


def rest_world(arm, name):
    return arm.matrix_world @ arm.data.bones[name].matrix_local


def height(arm):
    return max((arm.matrix_world @ b.head_local).z for b in arm.data.bones) - \
        min((arm.matrix_world @ b.head_local).z for b in arm.data.bones)


bpy.ops.wm.read_factory_settings(use_empty=True)
new_arm, new_objs, new_acts = import_glb(rig_path)
# Meshy preset motions downloaded with the rig ("All Added"): keep the battle ones under AH_* names.
MESHY_CLIPS = {'Double_Combo_Attack': 'AH_combo_double', 'Triple_Combo_Attack': 'AH_combo_triple',
               'Flying_Fist_Kick': 'AH_flying_kick', 'Hit_Reaction': 'AH_hit_react', 'Knock_Down': 'AH_knock_down',
               'victory': 'AH_victory', 'Victory': 'AH_victory', 'Walking': 'AH_walk', 'Charged_Slash': 'AH_charged_slash',
               'Charged_Spell_Cast': 'AH_charged_cast', 'Mage_Spell_Cast': 'AH_spell_cast', 'Sword_Judgment': 'AH_sword_judgment',
               'Archery_Shot': 'AH_archery_shot', 'Charged_Ground_Slam': 'AH_ground_slam', 'Heavy_Hammer_Swing': 'AH_hammer_swing',
               'Dead': 'AH_dead', 'Combat_Idle': 'AH_combat_idle'}
meshy_clips = []
for a in new_acts:
    if a.name in MESHY_CLIPS:
        a.name = MESHY_CLIPS[a.name]
        a.use_fake_user = True
        meshy_clips.append(a.name)
    else:
        bpy.data.actions.remove(a)
for o in new_objs:
    if o.type == 'MESH' and o.name.startswith('Icosphere'):
        bpy.data.objects.remove(o, do_unlink=True)


def facing(arm):
    l = arm.matrix_world @ arm.pose.bones['LeftUpLeg'].head
    r = arm.matrix_world @ arm.pose.bones['RightUpLeg'].head
    side = l - r
    side.z = 0
    f = side.cross(Vector((0, 0, 1)))
    return math.atan2(f.x, f.y)


# Meshy fight clips start in a side-on stance (~60 deg off the rest facing), which turns the model's back
# to the battle camera. Rotate the hips about the vertical so each clip starts facing the rest direction.
new_arm.animation_data_create()
for pb in new_arm.pose.bones:
    pb.matrix_basis = Matrix.Identity(4)
bpy.context.view_layer.update()
rest_yaw = facing(new_arm)
hips_name = 'Hips'
for a in [bpy.data.actions[n] for n in meshy_clips]:
    new_arm.animation_data.action = a
    f0, f1 = int(a.frame_range[0]), int(round(a.frame_range[1]))
    bpy.context.scene.frame_set(f0)
    off = math.atan2(math.sin(facing(new_arm) - rest_yaw), math.cos(facing(new_arm) - rest_yaw))
    if abs(math.degrees(off)) < 20:
        continue
    pb = new_arm.pose.bones[hips_name]
    pb.rotation_mode = 'QUATERNION'
    pivot = (new_arm.matrix_world @ new_arm.data.bones[hips_name].matrix_local).translation
    turn = Matrix.Translation(pivot) @ Matrix.Rotation(off, 4, 'Z') @ Matrix.Translation(-pivot)
    frames = []
    for f in range(f0, f1 + 1):
        bpy.context.scene.frame_set(f)
        frames.append((f, turn @ (new_arm.matrix_world @ pb.matrix)))
    for f, world in frames:
        bpy.context.scene.frame_set(f)
        pb.matrix = new_arm.matrix_world.inverted() @ world
        bpy.context.view_layer.update()
        pb.keyframe_insert('rotation_quaternion', frame=f)
        pb.keyframe_insert('location', frame=f)
    print('YAW-FIX', a.name, round(math.degrees(off)), flush=True)
new_arm.animation_data.action = None
old_arm, old_objs, old_acts = import_glb(old_path)
for o in old_objs:
    if o.type == 'MESH':
        bpy.data.objects.remove(o, do_unlink=True)

shared = [b.name for b in new_arm.data.bones if b.name in old_arm.data.bones]
missing = [b.name for b in new_arm.data.bones if b.name not in old_arm.data.bones]
ratio = height(new_arm) / max(1e-4, height(old_arm))

# Per-bone rest correction: align the old rest direction onto the new one, keeping the new roll.
fix = {}
for n in shared:
    o_rest, n_rest = rest_world(old_arm, n).to_quaternion(), rest_world(new_arm, n).to_quaternion()
    o_dir = rest_world(old_arm, n).to_3x3() @ Vector((0, 1, 0))
    n_dir = rest_world(new_arm, n).to_3x3() @ Vector((0, 1, 0))
    align = n_dir.rotation_difference(o_dir)  # world rotation taking new rest dir onto old rest dir
    fix[n] = o_rest.inverted() @ align @ n_rest

order = [b.name for b in new_arm.data.bones]  # parents precede children
hips = 'Hips' if 'Hips' in new_arm.data.bones else order[0]
old_hips_rest = rest_world(old_arm, hips).translation
new_hips_rest = rest_world(new_arm, hips).translation
for pb in new_arm.pose.bones:
    pb.rotation_mode = 'QUATERNION'

scene = bpy.context.scene
old_arm.animation_data_create()
new_arm.animation_data_create()
clips = list(meshy_clips)
for src in sorted(old_acts, key=lambda a: a.name):
    old_arm.animation_data.action = src
    f0, f1 = int(src.frame_range[0]), int(round(src.frame_range[1]))
    dst = bpy.data.actions.new('RT_' + src.name)  # renamed once the source actions are gone
    new_arm.animation_data.action = dst
    for f in range(f0, f1 + 1):
        scene.frame_set(f)
        for pb in new_arm.pose.bones:
            pb.matrix_basis = Matrix.Identity(4)
        bpy.context.view_layer.update()
        for n in order:
            if n not in fix:
                continue
            pb = new_arm.pose.bones[n]
            ow = old_arm.matrix_world @ old_arm.pose.bones[n].matrix
            rot = ow.to_quaternion() @ fix[n]
            if n == hips:
                pos = new_hips_rest + (ow.translation - old_hips_rest) * ratio
            else:
                pos = (new_arm.matrix_world @ pb.matrix).translation
            world = Matrix.Translation(pos) @ rot.to_matrix().to_4x4()
            pb.matrix = new_arm.matrix_world.inverted() @ world
            bpy.context.view_layer.update()
        for n in order:
            pb = new_arm.pose.bones[n]
            pb.keyframe_insert('rotation_quaternion', frame=f)
            if n == hips:
                pb.keyframe_insert('location', frame=f)
    dst.use_fake_user = True
    clips.append(src.name)

# Extra Meshy motions on the same rig: take their armature action as-is.
for path in extras:
    arm, objs, acts = import_glb(path)
    act = next((a for a in acts if a.fcurves), None) if acts else None
    if act:
        act.name = pathlib.Path(path).stem
        act.use_fake_user = True
        clips.append(act.name)
    for a in acts:
        if a is not act:
            bpy.data.actions.remove(a)
    for o in objs:
        bpy.data.objects.remove(o, do_unlink=True)

for o in [old_arm]:
    bpy.data.objects.remove(o, do_unlink=True)
for a in old_acts:
    bpy.data.actions.remove(a)
for a in list(bpy.data.actions):
    if a.name.startswith('RT_'):
        a.name = a.name[3:]
new_arm.animation_data.action = bpy.data.actions.get('AH_battle_guard')
bpy.data.orphans_purge(do_recursive=True)  # drops the old rig's images/materials

OUT.mkdir(parents=True, exist_ok=True)
for old in list(OUT.glob('texture_*.png')) + list(OUT.glob('texture_*.jpg')):
    old.unlink()
for i, img in enumerate(bpy.data.images):
    if img.type != 'IMAGE':
        continue
    ext = '.jpg' if (img.file_format or '').upper() == 'JPEG' else '.png'
    path = OUT / f'texture_{i}_{img.name}{ext}'
    if img.packed_file:
        path.write_bytes(img.packed_file.data)
        img.unpack(method='REMOVE')
    else:
        img.filepath_raw = str(path); img.file_format = 'PNG'; img.save()
    img.filepath = str(path)
bpy.ops.export_scene.fbx(filepath=str(OUT / 'model.fbx'), object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
                         bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                         path_mode='RELATIVE', embed_textures=False, axis_forward='-Z', axis_up='Y',
                         apply_scale_options='FBX_SCALE_ALL')
bpy.ops.wm.save_as_mainfile(filepath=str(pathlib.Path(rig_path).with_name(unit + '-battle-anime.blend')))
(OUT / 'provenance.json').write_text(json.dumps({
    'model': str(rig_path), 'motions_from': str(old_path), 'extra_motions': extras,
    'clips': clips, 'shared_bones': len(shared), 'unmatched_bones': missing, 'height_ratio': ratio}, indent=2))
print('RETARGETED', unit, clips, 'unmatched', missing, flush=True)
