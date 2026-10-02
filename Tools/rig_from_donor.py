"""Rig a character Meshy could not auto-rig: fit a donor MeshyRig skeleton to the body, transfer its skin
weights, then hand-key style poses (with two-hand IK on a held weapon).

blender -b --factory-startup --python Tools/rig_from_donor.py -- Tools/battle_rigs/<id>_rig.json

Spec keys:
  donor        Meshy rigged GLB with the same 24-bone skeleton (bone names drive everything)
  body         split body GLB (Tools/split_weapon.py) to rig
  height       target height in metres; the body is scaled, feet to z=0, head centred on x=0
  donor_clips  donor actions to keep (generic reactions), renamed {old: new}
  weapon       {"bone", "grip_back", "tip_dir"}: hand holding the weapon and where the off hand grips
  poses        named key poses (see apply_pose), actions {name: [[frame, pose], ...]}
Output: <job>/rigged.glb (armature + skinned body + actions) and <job>/rig_from_donor.blend.
"""
import bpy, sys, json, math, pathlib
from mathutils import Vector, Matrix, Quaternion
from mathutils.kdtree import KDTree

ROOT = pathlib.Path(__file__).resolve().parent.parent
SPEC = json.loads(pathlib.Path(sys.argv[sys.argv.index('--') + 1]).read_text())
JOB = ROOT / SPEC['job']


def log(*a):
    print('RIG', *a, flush=True)


def import_glb(path):
    before = set(bpy.data.objects)
    acts = set(bpy.data.actions)
    bpy.ops.import_scene.gltf(filepath=str(path))
    new = [o for o in bpy.data.objects if o not in before]
    return new, [a for a in bpy.data.actions if a not in acts]


bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = 24
dnew, dacts = import_glb(ROOT / SPEC['donor'])
ARM = next(o for o in dnew if o.type == 'ARMATURE')
DM = next(o for o in dnew if o.type == 'MESH' and not o.name.startswith('Icosphere'))
for o in dnew:
    if o.type == 'MESH' and o.name.startswith('Icosphere'):
        bpy.data.objects.remove(o, do_unlink=True)
ARM.animation_data_create()
ARM.animation_data.action = None
for pb in ARM.pose.bones:
    pb.matrix_basis.identity()
bpy.context.view_layer.update()
tnew, _ = import_glb(ROOT / SPEC['body'])
BODY = next(o for o in tnew if o.type == 'MESH')
BODY.name = 'Body'
bpy.context.view_layer.objects.active = BODY
bpy.ops.object.select_all(action='DESELECT')
BODY.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def verts(obj):
    return [obj.matrix_world @ v.co for v in obj.data.vertices]


# ---------------------------------------------------------------- normalise the body
tv = verts(BODY)
zmin = min(p.z for p in tv)
zmax = max(p.z for p in tv)
s = SPEC['height'] / (zmax - zmin)
head = [p for p in tv if p.z > zmax - (zmax - zmin) * 0.08]
hx = sum(p.x for p in head) / len(head)
torso = [p for p in tv if abs(p.z - (zmin + (zmax - zmin) * 0.6)) < 0.03 * (zmax - zmin) and abs(p.x - hx) < 0.15 * (zmax - zmin)]
ty = sum(p.y for p in torso) / len(torso)
BODY.data.transform(Matrix.Scale(s, 4) @ Matrix.Translation(Vector((-hx, -ty, -zmin))))
BODY.data.update()


def landmarks(pts, label):
    zt = max(p.z for p in pts)
    upper = [p for p in pts if p.z > 0.55 * zt]          # arms in T-pose; keeps a hanging tail out
    left = max(p.x for p in upper)
    right = min(p.x for p in upper)
    span = min(left, -right)
    arm = [p for p in upper if abs(p.x) > span * 0.6]
    sh_z = sorted(p.z for p in arm)[len(arm) // 2]
    # mid-plane slice: skips front aprons/skirt flaps and tails or hair behind
    waist = sorted(p.y for p in pts if 0.4 * zt < p.z < 0.6 * zt and abs(p.x) < 0.1 * zt)
    yc = waist[len(waist) // 2]
    mid = [p for p in pts if abs(p.y - yc) < 0.035 * zt]
    crotch = None
    for k in range(30, 62):
        z = zt * k / 100
        if any(abs(p.x) < 0.012 * zt and abs(p.z - z) < 0.006 * zt for p in mid):
            crotch = z
            break
    crotch = crotch or zt * 0.46
    band = [p for p in mid if abs(p.z - (sh_z - 0.1 * zt)) < 0.01 * zt and abs(p.x) < span * 0.4]
    shw = max(abs(p.x) for p in band) if band else 0.12 * zt
    thigh = [p for p in mid if abs(p.z - crotch * 0.75) < 0.012 * zt and abs(p.x) < 0.17 * zt]
    hipw = (sum(abs(p.x) for p in thigh) / len(thigh)) if thigh else 0.08 * zt
    L = dict(top=zt, span=span, shoulder_z=sh_z, crotch=crotch, shoulder_w=shw, hip_w=hipw)
    log(label, {k: round(v, 3) for k, v in L.items()})
    return L


D = landmarks(verts(DM), 'donor')
T = landmarks(verts(BODY), 'target')


def zmap(z):
    ks = [(0.0, 0.0), (D['crotch'], T['crotch']), (D['shoulder_z'], T['shoulder_z']), (D['top'], T['top'])]
    for (a0, b0), (a1, b1) in zip(ks, ks[1:]):
        if z <= a1:
            return b0 + (z - a0) * (b1 - b0) / (a1 - a0)
    return T['top'] + (z - D['top']) * (T['top'] - T['shoulder_z']) / (D['top'] - D['shoulder_z'])


def warp(p):
    x = p.x
    ax = abs(x)
    sx = 1 if x >= 0 else -1
    if ax > D['shoulder_w'] and p.z > D['crotch'] + 0.25 * (D['shoulder_z'] - D['crotch']):
        x = sx * (T['shoulder_w'] + (ax - D['shoulder_w']) * (T['span'] - T['shoulder_w']) / (D['span'] - D['shoulder_w']))
    elif p.z < D['crotch']:
        x = x * T['hip_w'] / D['hip_w']
    else:
        x = x * T['shoulder_w'] / D['shoulder_w']
    return Vector((x, p.y * T['top'] / D['top'], zmap(p.z)))


# ---------------------------------------------------------------- fit the skeleton
bpy.context.view_layer.objects.active = ARM
bpy.ops.object.select_all(action='DESELECT')
ARM.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for eb in ARM.data.edit_bones:
    h, t = warp(eb.head), warp(eb.tail)
    eb.head, eb.tail = h, t
bpy.ops.object.mode_set(mode='OBJECT')
# warp the donor mesh too so its weights land on the matching surfaces
for v in DM.data.vertices:
    v.co = DM.matrix_world.inverted() @ warp(DM.matrix_world @ v.co)
DM.data.update()

# ---------------------------------------------------------------- weights: nearest donor surface
BODY.vertex_groups.clear()
for g in DM.vertex_groups:
    BODY.vertex_groups.new(name=g.name)
dt = BODY.modifiers.new('DT', 'DATA_TRANSFER')
dt.object = DM
dt.use_vert_data = True
dt.data_types_verts = {'VGROUP_WEIGHTS'}
dt.vert_mapping = 'POLYINTERP_NEAREST'
dt.layers_vgroup_select_src = 'ALL'
dt.layers_vgroup_select_dst = 'NAME'
bpy.context.view_layer.objects.active = BODY
bpy.ops.object.modifier_apply(modifier=dt.name)
bpy.ops.object.vertex_group_normalize_all(lock_active=False)
bpy.data.objects.remove(DM, do_unlink=True)
mod = BODY.modifiers.new('Armature', 'ARMATURE')
mod.object = ARM
BODY.parent = ARM
log('weights transferred', len(BODY.vertex_groups), 'groups')

# ---------------------------------------------------------------- donor clips
keep = SPEC.get('donor_clips', {})
for a in dacts:
    if a.name in keep:
        a.name = keep[a.name]
        a.use_fake_user = True
    else:
        bpy.data.actions.remove(a)

# ---------------------------------------------------------------- pose authoring
bpy.context.view_layer.update()
FWD = (ARM.matrix_world @ ARM.data.bones['headfront'].tail_local) - (ARM.matrix_world @ ARM.data.bones['headfront'].head_local)
FWD = Vector((FWD.x, FWD.y, 0)).normalized()
UP = Vector((0, 0, 1))
RIGHT = FWD.cross(UP).normalized()


def cs(v):
    """[right, forward, up] character-space components -> world vector."""
    return (RIGHT * v[0] + FWD * v[1] + UP * v[2]).normalized()


def world(pb):
    return ARM.matrix_world @ pb.matrix


def set_world(pb, m):
    pb.matrix = ARM.matrix_world.inverted() @ m
    bpy.context.view_layer.update()


def aim(name, d):
    pb = ARM.pose.bones[name]
    m = world(pb)
    cur = m.col[1].xyz.normalized()
    q = cur.rotation_difference(d)
    head = m.translation.copy()
    r = (q.to_matrix() @ m.to_3x3()).to_4x4()
    r.translation = head
    set_world(pb, r)


def turn(name, axis, deg):
    pb = ARM.pose.bones[name]
    m = world(pb)
    head = m.translation.copy()
    r = (Matrix.Rotation(math.radians(deg), 3, axis) @ m.to_3x3()).to_4x4()
    r.translation = head
    set_world(pb, r)


W = SPEC.get('weapon')
rest_hand = None
if W:
    hb = ARM.data.bones[W['bone']]
    hand_rest = (ARM.matrix_world @ hb.matrix_local).to_3x3()
    BLADE_LOCAL = hand_rest.inverted() @ cs(W.get('tip_vec', [0, 1, 0]))


def blade_world():
    return (world(ARM.pose.bones[W['bone']]).to_3x3() @ BLADE_LOCAL).normalized()


def two_bone_ik(upper, fore, target, pole_dir):
    S = world(ARM.pose.bones[upper]).translation
    a = ARM.data.bones[upper].length
    b = ARM.data.bones[fore].length
    d = min((target - S).length, (a + b) * 0.999)
    u = (target - S).normalized()
    cosA = max(-1, min(1, (a * a + d * d - b * b) / (2 * a * d)))
    v = pole_dir - u * pole_dir.dot(u)
    v.normalize()
    E = S + a * (u * cosA + v * math.sqrt(1 - cosA * cosA))
    aim(upper, (E - S).normalized())
    aim(fore, (target - E).normalized())


def apply_pose(p):
    for pb in ARM.pose.bones:
        pb.matrix_basis.identity()
    bpy.context.view_layer.update()
    hips = ARM.pose.bones['Hips']
    m = world(hips)
    off = p.get('hips', [0, 0, 0])
    m = Matrix.Translation(RIGHT * off[0] + FWD * off[1] + UP * off[2]) @ m
    piv = m.translation.copy()
    rot = Matrix.Rotation(math.radians(p.get('yaw', 0)), 3, UP) @ Matrix.Rotation(math.radians(p.get('lean', 0)), 3, RIGHT)
    m = (rot @ m.to_3x3()).to_4x4()
    m.translation = piv
    set_world(hips, m)
    for name in ['Spine02', 'Spine01', 'Spine']:
        if 'spine_twist' in p:
            turn(name, UP, p['spine_twist'] / 3)
        if 'spine_lean' in p:
            turn(name, RIGHT, p['spine_lean'] / 3)
    for name, d in p.get('aim', {}).items():   # parents first: order in the spec matters
        aim(name, cs(d))
    if W and 'blade' in p:
        hand = ARM.pose.bones[W['bone']]
        q = blade_world().rotation_difference(cs(p['blade']))
        m = world(hand)
        head = m.translation.copy()
        r = (q.to_matrix() @ m.to_3x3()).to_4x4()
        r.translation = head
        set_world(hand, r)
    if W and W.get('off_hand'):
        oh = W['off_hand']
        hand = world(ARM.pose.bones[W['bone']])
        palm = hand.translation + hand.col[1].xyz.normalized() * ARM.data.bones[W['bone']].length * 0.45
        target = palm - blade_world() * W.get('grip_back', 0.11)
        two_bone_ik(oh['upper'], oh['fore'], target, cs(p.get('off_pole', oh.get('pole', [0.3, -0.4, -1]))))
        ohand = ARM.pose.bones[oh['hand']]
        r = world(ARM.pose.bones[W['bone']]).to_3x3().to_4x4()
        r.translation = world(ohand).translation
        set_world(ohand, r)
    for name, d in p.get('aim_after', {}).items():
        aim(name, cs(d))


POSES = SPEC['poses']
for act_name, keys in SPEC['actions'].items():
    act = bpy.data.actions.new(act_name)
    act.use_fake_user = True
    ARM.animation_data.action = act
    for frame, pose_name in keys:
        apply_pose(POSES[pose_name])
        for pb in ARM.pose.bones:
            pb.rotation_mode = 'QUATERNION'
            pb.keyframe_insert('location', frame=frame)
            pb.keyframe_insert('rotation_quaternion', frame=frame)
    log('action', act_name, [k[1] for k in keys])
ARM.animation_data.action = None
for pb in ARM.pose.bones:
    pb.matrix_basis.identity()

JOB.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(JOB / 'rig_from_donor.blend'))
bpy.ops.object.select_all(action='DESELECT')
ARM.select_set(True)
BODY.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(JOB / 'rigged.glb'), export_format='GLB', use_selection=True,
                          export_animation_mode='ACTIONS', export_image_format='JPEG', export_jpeg_quality=90)
log('exported', JOB / 'rigged.glb')
