"""Render Tower-mode motion frames (idle / walk_in_place / task / knocked_down) from a battle rig .blend.

blender -b <MeshyJobs/anime-battle-v2/<job>/<id>-battle.blend> --python Tools/bake_tower_motion.py -- <out_dir> [clip ...]

TowerChibiAnimator plays 24 frames at 12 fps on a camera-facing quad, so each clip is rendered as 24 frames,
front three-quarter view, transparent background, 2x the 124x186 atlas cell (Tools/pack_tower_atlas.py
downsamples and packs). Held Weapon_* props are hidden: the Tower shows the civilian look.
idle and task are posed procedurally on the MeshyRig skeleton (arms down + breathing; hands working at a
bench); walk_in_place and knocked_down sample AH_walk / AH_knock_down. knocked_down is rendered 1.35x wider
because the animator stretches the downed quad from 0.85 to 1.15 units.
"""
import bpy, sys, math, os, pathlib
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index('--') + 1:]
OUT = pathlib.Path(argv[0])
CLIPS = argv[1:] or ['idle', 'walk_in_place', 'task', 'knocked_down']
OUT.mkdir(parents=True, exist_ok=True)
FRAMES = 24
CELL_W, CELL_H = 124 * 2, 186 * 2
DOWNED_STRETCH = 1.15 / 0.85
YAW_DEG, PITCH_DEG = 12.0, 8.0     # camera: a little to the character's left and above, like the cutaway

scene = bpy.context.scene
ARM = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
BODY = bpy.data.objects['Body']
for o in bpy.data.objects:
    if o.type == 'MESH' and o.name.startswith('Weapon_'):
        o.hide_render = True
    if o.type in {'CAMERA', 'LIGHT'}:
        bpy.data.objects.remove(o, do_unlink=True)
ARM.animation_data_create()
ARM.animation_data.action = None


def reset():
    for pb in ARM.pose.bones:
        pb.matrix_basis.identity()
    bpy.context.view_layer.update()


def bone_head(name, tail=False):
    b = ARM.data.bones[name]
    return ARM.matrix_world @ (b.tail_local if tail else b.head_local)


reset()
FWD = bone_head('headfront', True) - bone_head('headfront')
FWD = Vector((FWD.x, FWD.y, 0)).normalized()
UP = Vector((0, 0, 1))
RIGHT = FWD.cross(UP).normalized()
BONES = set(b.name for b in ARM.data.bones)
TAIL = sorted(n for n in BONES if n.startswith('Tail'))


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
    q = m.col[1].xyz.normalized().rotation_difference(d)
    r = (q.to_matrix() @ m.to_3x3()).to_4x4()
    r.translation = m.translation.copy()
    set_world(pb, r)


def turn(name, axis, deg):
    pb = ARM.pose.bones[name]
    m = world(pb)
    r = (Matrix.Rotation(math.radians(deg), 3, axis) @ m.to_3x3()).to_4x4()
    r.translation = m.translation.copy()
    set_world(pb, r)


def lift(name, offset):
    pb = ARM.pose.bones[name]
    m = world(pb)
    m.translation = m.translation + offset
    set_world(pb, m)


def body_points():
    dg = bpy.context.evaluated_depsgraph_get()
    ev = BODY.evaluated_get(dg)
    m = ev.to_mesh()
    pts = [BODY.matrix_world @ m.vertices[i].co for i in range(0, len(m.vertices), 7)]
    ev.to_mesh_clear()
    return pts


HEIGHT = max(p.z for p in body_points()) - min(p.z for p in body_points())
SPINE = [n for n in ('Spine02', 'Spine01', 'Spine') if n in BONES]


def sway_tail(phase, amp):
    for k, name in enumerate(TAIL):
        turn(name, UP, amp * math.sin(2 * math.pi * phase - 0.7 * k))


def arms_down(phase):
    b = math.sin(2 * math.pi * phase)
    for side, sx in (('Right', 1), ('Left', -1)):
        aim(side + 'Arm', cs([0.30 * sx, 0.06 + 0.012 * b, -1]))
        aim(side + 'ForeArm', cs([0.16 * sx, 0.20 + 0.02 * b, -1]))


def pose_idle(phase):
    """Relaxed stand: arms at the sides, slow breathing, tail swish."""
    reset()
    b = math.sin(2 * math.pi * phase)
    lift('Hips', UP * (0.003 * HEIGHT * b))
    for name in SPINE:
        turn(name, RIGHT, 0.6 * b)
    arms_down(phase)
    if 'Head' in BONES:
        turn('Head', RIGHT, -1.2 * b)
        turn('Head', UP, 2.0 * math.sin(2 * math.pi * phase + 1.0))
    sway_tail(phase, 7)


def pose_task(phase):
    """Working at a bench: lean in, hands alternately pressing and lifting."""
    reset()
    s = math.sin(2 * math.pi * phase)
    lift('Hips', UP * (-0.006 * HEIGHT * abs(s)))
    for name in SPINE:
        turn(name, RIGHT, -4.0)
    for side, sx, sign in (('Right', 1, 1), ('Left', -1, -1)):
        aim(side + 'Arm', cs([0.28 * sx, 0.55, -0.85]))
        aim(side + 'ForeArm', cs([-0.25 * sx, 1.0, 0.05 + 0.45 * s * sign]))
    if 'Head' in BONES:
        turn('Head', RIGHT, -10.0)
    sway_tail(phase, 5)


def pose_limp(prog):
    """Knocked out: knees give, arms fall open, head lolls."""
    reset()
    for side, sx in (('Right', 1), ('Left', -1)):
        aim(side + 'Arm', cs([(0.30 + 0.35 * prog) * sx, 0.10, -1]))
        aim(side + 'ForeArm', cs([(0.20 + 0.45 * prog) * sx, 0.25, -1]))
    for name in SPINE:
        turn(name, RIGHT, -3.0 * prog)
    if 'Head' in BONES:
        turn('Head', FWD, -18.0 * prog)
        turn('Head', RIGHT, -8.0 * prog)
    for side in ('Left', 'Right'):
        if side + 'Leg' in BONES:
            turn(side + 'UpLeg', RIGHT, -6.0 * prog)
            turn(side + 'Leg', RIGHT, 10.0 * prog)


def play(action, t):
    ARM.animation_data.action = bpy.data.actions[action]
    if hasattr(ARM.animation_data, 'action_slot') and ARM.animation_data.action.slots:
        ARM.animation_data.action_slot = ARM.animation_data.action.slots[0]
    scene.frame_set(int(t), subframe=t - int(t))


def unplay():
    ARM.animation_data.action = None
    reset()


# --- camera + render setup ------------------------------------------------------------------------------------
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'TEXTURE'
scene.display.shading.show_cavity = False
scene.display.shading.show_object_outline = False
scene.display.render_aa = '16'
scene.view_settings.view_transform = 'Standard'
scene.render.film_transparent = True
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'

cam_data = bpy.data.cameras.new('TowerCam')
cam_data.type = 'ORTHO'
cam = bpy.data.objects.new('TowerCam', cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
view = (Matrix.Rotation(math.radians(-YAW_DEG), 3, UP) @ FWD)
view = (view * math.cos(math.radians(PITCH_DEG)) + UP * math.sin(math.radians(PITCH_DEG))).normalized()

# Fit once on the idle pose so every clip of this character shares one scale and one ground line:
# the figure fills 90% of the cell height with the feet 4% above the bottom edge.
pose_idle(0)
pts = body_points()
mid = sum(pts, Vector()) / len(pts)
cam.rotation_euler = (-view).to_track_quat('-Z', 'Y').to_euler()
cam.location = mid + view * (HEIGHT * 4)
bpy.context.view_layer.update()
inv = cam.matrix_world.inverted()
cp = [inv @ p for p in pts]
ymin, ymax = min(c.y for c in cp), max(c.y for c in cp)
xmid = (min(c.x for c in cp) + max(c.x for c in cp)) / 2
FRAME_H = (ymax - ymin) / 0.90
cam_data.ortho_scale = FRAME_H
cam.location += cam.matrix_world.to_3x3() @ Vector((xmid, ymin + FRAME_H * 0.46, 0))
reset()


def frame_camera(width_scale):
    scene.render.resolution_x = int(round(CELL_W * width_scale))
    scene.render.resolution_y = CELL_H
    cam_data.ortho_scale = FRAME_H * max(1.0, CELL_W * width_scale / CELL_H)


def shoot(clip, i):
    scene.render.filepath = str(OUT / f'{clip}_{i:02d}.png')
    bpy.ops.render.render(write_still=True)


for clip in CLIPS:
    frame_camera(DOWNED_STRETCH if clip == 'knocked_down' else 1.0)
    if clip == 'idle':
        for i in range(FRAMES):
            pose_idle(i / FRAMES)
            shoot(clip, i)
    elif clip == 'task':
        for i in range(FRAMES):
            pose_task(i / FRAMES)
            shoot(clip, i)
    elif clip == 'walk_in_place':
        act = bpy.data.actions['AH_walk']
        f0, f1 = act.frame_range
        cycles = max(1, round(2.0 / ((f1 - f0) / scene.render.fps)))
        base = ARM.location.copy()
        play('AH_walk', f0)
        hips0 = world(ARM.pose.bones['Hips']).translation.copy()
        for i in range(FRAMES):
            t = f0 + ((i / FRAMES) * cycles % 1.0) * (f1 - f0)
            play('AH_walk', t)
            drift = world(ARM.pose.bones['Hips']).translation - hips0
            ARM.location = base - Vector((drift.x, drift.y, 0))
            bpy.context.view_layer.update()
            shoot(clip, i)
        ARM.location = base
        unplay()
    elif clip == 'knocked_down':
        # Like the old chibi atlases: a limp topple in the screen plane (head to screen-left) that ends lying
        # flat on the floor line, centred. Plays once; the animator holds the last frame.
        base_mw = ARM.matrix_world.copy()
        cam_rot = cam.matrix_world.to_3x3()
        inv = cam.matrix_world.inverted()
        cam_data.ortho_scale = FRAME_H / 0.85
        ground = -0.5 * FRAME_H / 0.85 + 0.04 * FRAME_H / 0.85
        pivot = (bone_head('LeftFoot') + bone_head('RightFoot')) / 2
        for i in range(FRAMES):
            if i < 3:
                prog = 0.0
            elif i < 16:
                prog = ((i - 2) / 13) ** 2
            else:
                prog = {16: 0.95, 17: 1.0}.get(i, 1.0)
            pose_limp(prog)
            m = Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(90 * prog), 4, view) @ Matrix.Translation(-pivot) @ base_mw
            ARM.matrix_world = m
            bpy.context.view_layer.update()
            cp = [inv @ p for p in body_points()]
            cx = sum(c.x for c in cp) / len(cp)
            dy = (ground - min(c.y for c in cp)) * prog
            ARM.matrix_world = Matrix.Translation(cam_rot @ Vector((-cx, dy, 0))) @ m
            bpy.context.view_layer.update()
            shoot(clip, i)
        ARM.matrix_world = base_mw
        reset()
    print('BAKED', clip, flush=True)
