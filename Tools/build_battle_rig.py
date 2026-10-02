"""Build a battle-ready rig from a Meshy auto-rig: props, tail chain, a CZN-timed basic attack, FBX export.

blender -b --factory-startup --python Tools/build_battle_rig.py -- Tools/battle_rigs/<id>.json [--no-export]

Meshy does the template humanoid rig (MeshyRig, 24 bones). This script does the specialised part:
  props    weapons / floating props become rigid meshes skinned 100% to one hand bone (named Weapon_*)
  tail     a bone chain from Hips along the tail centreline, tail vertices re-weighted along it,
           and a lagged follow-through baked into every clip
  basic    AH_attack_basic is authored from timed source poses (clip, frame) with blends, an anticipation
           dip and a recovery into the guard pose - the Chaos Zero Nightmare beat: crouch, snap, hold, recover
  clips    Meshy presets are renamed AH_* (Combat_Stance -> AH_battle_guard, Hit_Reaction -> AH_hit_react ...)
Output: Assets/Resources/AdamsHaven/BattleModels/<id>/model.fbx + textures + provenance.json, and
MeshyJobs/anime-battle-v2/<id>/<id>-battle.blend for hand edits.
"""
import bpy, bmesh, sys, json, math, pathlib, heapq
from mathutils import Matrix, Vector, Quaternion
from mathutils.kdtree import KDTree

ROOT = pathlib.Path(__file__).resolve().parent.parent
argv = sys.argv[sys.argv.index('--') + 1:]
spec_path = pathlib.Path(argv[0])
SPEC = json.loads(spec_path.read_text())
EXPORT = '--no-export' not in argv
UID = SPEC['id']
JOB = ROOT / 'MeshyJobs' / 'anime-battle-v2' / SPEC.get('job', UID)
OUT = ROOT / 'Assets' / 'Resources' / 'AdamsHaven' / 'BattleModels' / SPEC.get('out', UID)
FPS = 30
BASE_CLIPS = {'Combat_Stance': 'AH_battle_guard', 'Hit_Reaction': 'AH_hit_react', 'Knock_Down': 'AH_knock_down',
              'Walking': 'AH_walk'}


def log(*a):
    print('BUILD', *a, flush=True)


# ---------------------------------------------------------------- load
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = 24  # Meshy clips land on a 24 fps timeline; spec source frames use it
bpy.ops.import_scene.gltf(filepath=str(JOB / SPEC.get('rig', 'rigged.glb')))
scene.render.fps = FPS  # authored / exported clips are keyed at 30 fps
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name.startswith('Icosphere'):
        bpy.data.objects.remove(o, do_unlink=True)
ARM = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
BODY = next(o for o in bpy.data.objects if o.type == 'MESH')
BODY.name = 'Body'
ARM.animation_data_create()
src_fps = 24.0  # Meshy glTF clips import on the 24 fps timeline


def set_action(act):
    ARM.animation_data.action = act
    if act is not None and hasattr(ARM.animation_data, 'action_slot') and len(act.slots):
        ARM.animation_data.action_slot = act.slots[0]


def rest_pose():
    set_action(None)
    for pb in ARM.pose.bones:
        pb.matrix_basis.identity()
    bpy.context.view_layer.update()


def bone_world(name, tail=False):
    b = ARM.data.bones[name]
    return ARM.matrix_world @ (b.tail_local if tail else b.head_local)


rest_pose()
FORWARD = bone_world('headfront', True) - bone_world('headfront')
FORWARD = Vector((FORWARD.x, FORWARD.y, 0)).normalized()  # character front (Meshy exports face -Y)
log('forward', tuple(round(x, 2) for x in FORWARD))

# ---------------------------------------------------------------- clips
SRC = {}
for a in list(bpy.data.actions):
    a.use_fake_user = True
    SRC[a.name] = a
keep = SPEC.get('keep_clips', {})


# ---------------------------------------------------------------- mesh helpers
def weld_islands(obj):
    """Loose parts of obj after welding UV-seam splits: list of vertex index sets (largest first)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    # union-find over edges plus coincident vertices
    parent = list(range(len(bm.verts)))

    def find(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    def union(a, b):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[ra] = rb

    for e in bm.edges:
        union(e.verts[0].index, e.verts[1].index)
    kd = KDTree(len(bm.verts))
    for v in bm.verts:
        kd.insert(v.co, v.index)
    kd.balance()
    for v in bm.verts:
        for _, j, _ in kd.find_range(v.co, 1e-4):
            union(v.index, j)
    groups = {}
    for v in bm.verts:
        groups.setdefault(find(v.index), set()).add(v.index)
    bm.free()
    return sorted(groups.values(), key=len, reverse=True)


def separate_verts(obj, verts, name):
    """Split the faces using only `verts` into a new object."""
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='DESELECT')
    bpy.ops.object.mode_set(mode='OBJECT')
    for i in verts:
        obj.data.vertices[i].select = True
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_mode(type='VERT')
    bpy.ops.mesh.separate(type='SELECTED')
    bpy.ops.object.mode_set(mode='OBJECT')
    new = [o for o in bpy.context.selected_objects if o is not obj][0]
    new.name = name
    return new


def skin_rigid(obj, bone):
    """Weight every vertex of obj 100% to one bone and bind it to the armature."""
    obj.vertex_groups.clear()
    vg = obj.vertex_groups.new(name=bone)
    vg.add(list(range(len(obj.data.vertices))), 1.0, 'REPLACE')
    for m in list(obj.modifiers):
        if m.type == 'ARMATURE':
            obj.modifiers.remove(m)
    mod = obj.modifiers.new('Armature', 'ARMATURE')
    mod.object = ARM
    obj.parent = ARM
    obj.matrix_parent_inverse = ARM.matrix_world.inverted()


# ---------------------------------------------------------------- props embedded in the Meshy mesh
for job in SPEC.get('mesh_props', []):
    rest_pose()
    islands = weld_islands(BODY)
    co = [BODY.matrix_world @ v.co for v in BODY.data.vertices]
    taken = []
    for isl in islands[1:]:
        c = sum((co[i] for i in isl), Vector()) / len(isl)
        if len(isl) >= job.get('min_verts', 20):
            taken.append((isl, c))
    log('mesh islands', [(len(i), tuple(round(x, 2) for x in c)) for i, c in taken])
    for k, (isl, c) in enumerate(taken):
        if job['action'] == 'delete':
            sep = separate_verts(BODY, isl, f'Drop_{k}')
            bpy.data.objects.remove(sep, do_unlink=True)
        else:
            # nearest hand wins
            bone = min(job['bones'], key=lambda b: (bone_world(b, True) - c).length)
            sep = separate_verts(BODY, isl, f'Weapon_{job["name"]}_{k}')
            skin_rigid(sep, bone)
            log('prop', sep.name, '->', bone, len(isl), 'verts')


# ---------------------------------------------------------------- carve leftovers out of the body
def seg_dist(p, a, b):
    ab = b - a
    u = max(0.0, min(1.0, (p - a).dot(ab) / max(1e-9, ab.length_squared)))
    return (a + ab * u - p).length


for c in SPEC.get('carve', []):
    """Remove a weapon stub fused into the hand: a vertical cylinder through the palm, sparing the hand/forearm."""
    rest_pose()
    palm = bone_world(c['bone']).lerp(bone_world(c['bone'], True), 0.5)
    keep_bones = [(bone_world(n), bone_world(n, True)) for n in c.get('spare', [c['bone']])]
    doomed = []
    for v in BODY.data.vertices:
        p = BODY.matrix_world @ v.co
        if ((p.x - palm.x) ** 2 + (p.y - palm.y) ** 2) ** 0.5 > c['radius']:
            continue
        if abs(p.z - palm.z) > c.get('height', 1.0):
            continue
        if min(seg_dist(p, a, b) for a, b in keep_bones) < c.get('spare_radius', 0.05):
            continue
        doomed.append(v.index)
    if doomed:
        bm = bmesh.new()
        bm.from_mesh(BODY.data)
        bm.verts.ensure_lookup_table()
        bmesh.ops.delete(bm, geom=[bm.verts[i] for i in doomed], context='VERTS')
        bm.to_mesh(BODY.data)
        bm.free()
    log('carved', len(doomed), 'verts near', c['bone'])


# ---------------------------------------------------------------- external weapon props
def import_prop(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(path))
    new = [o for o in bpy.data.objects if o not in before]
    meshes = [o for o in new if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    for o in new:
        if o is not obj and o.name in bpy.data.objects:
            bpy.data.objects.remove(o, do_unlink=True)
    obj.parent = None
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


def decimate(obj, faces):
    n = len(obj.data.polygons)
    if n <= faces:
        return
    mod = obj.modifiers.new('Decimate', 'DECIMATE')
    mod.ratio = faces / n
    mod.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)
    log('decimated', obj.name, n, '->', len(obj.data.polygons))


def principal_axis(obj):
    pts = [v.co for v in obj.data.vertices]
    c = sum(pts, Vector()) / len(pts)
    # longest bbox axis is good enough for weapons
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    ext = hi - lo
    ax = max(range(3), key=lambda i: ext[i])
    return c, lo, hi, ax


def place_prop(obj, p):
    """Normalise the prop, then put its grip on the hand in the rest pose.

    p: bone, length (m), grip (0..1 along the long axis from the 'butt' end), tip_dir ('forward'|'back'|'up'|'down'|
    'along' = along the bone), roll (deg about the long axis), offset ([x,y,z] m in character space: right, forward, up),
    flip (swap which end is the butt), split ('left'|'right' keep one half of a paired prop along X)."""
    c, lo, hi, ax = principal_axis(obj)
    me = obj.data
    # move long axis to local Y, butt end at y=0
    rot = {0: Matrix.Rotation(math.radians(-90), 4, 'Z'), 1: Matrix.Identity(4), 2: Matrix.Rotation(math.radians(-90), 4, 'X')}[ax]
    me.transform(Matrix.Translation(-Vector((c.x, c.y, c.z))))
    me.transform(rot)
    ys = [v.co.y for v in me.vertices]
    y0, y1 = min(ys), max(ys)
    if p.get('flip'):
        me.transform(Matrix.Rotation(math.pi, 4, 'Z'))
        y0, y1 = -y1, -y0
    me.transform(Matrix.Translation(Vector((0, -y0, 0))))
    s = p['length'] / (y1 - y0)
    me.transform(Matrix.Scale(s, 4))
    me.transform(Matrix.Rotation(math.radians(p.get('roll', 0)), 4, 'Y'))
    grip = Vector((0, p['length'] * p.get('grip', 0.2), 0))
    me.transform(Matrix.Translation(-grip))
    # orient: local +Y -> tip direction in character space
    right = FORWARD.cross(Vector((0, 0, 1))).normalized()
    up = Vector((0, 0, 1))
    hand = ARM.data.bones[p['bone']]
    hand_dir = (bone_world(p['bone'], True) - bone_world(p['bone'])).normalized()
    dirs = {'forward': FORWARD, 'back': -FORWARD, 'up': up, 'down': -up, 'along': hand_dir}
    tip = dirs[p.get('tip_dir', 'forward')]
    q = Vector((0, 1, 0)).rotation_difference(tip)
    palm = bone_world(p['bone']).lerp(bone_world(p['bone'], True), p.get('palm', 0.45))
    off = p.get('offset', [0, 0, 0])
    palm += right * off[0] + FORWARD * off[1] + up * off[2]
    obj.matrix_world = Matrix.Translation(palm) @ q.to_matrix().to_4x4()
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


for p in SPEC.get('props', []):
    rest_pose()
    obj = import_prop(ROOT / p['file'])
    if p.get('split'):
        isl = weld_islands(obj)
        cx = lambda s: sum(obj.data.vertices[i].co.x for i in s) / len(s)
        big = isl[:2] if len(isl) > 1 else isl
        side = max(big, key=cx) if p['split'] == 'max_x' else min(big, key=cx)
        others = set().union(*[s for s in isl if s is not side])
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bm.verts.ensure_lookup_table()
        bmesh.ops.delete(bm, geom=[bm.verts[i] for i in others], context='VERTS')
        bm.to_mesh(obj.data)
        bm.free()
    decimate(obj, p.get('faces', 5000))
    obj.name = 'Weapon_' + p['name']
    place_prop(obj, p)
    skin_rigid(obj, p['bone'])
    log('weapon', obj.name, '->', p['bone'], len(obj.data.polygons), 'faces')


# ---------------------------------------------------------------- tail chain
TAIL = []


def dijkstra(adj, co, seeds, allowed=None):
    dist = {s: 0.0 for s in seeds}
    heap = [(0.0, s) for s in seeds]
    while heap:
        d, i = heapq.heappop(heap)
        if d > dist.get(i, 9e9):
            continue
        for j in adj.get(i, ()):
            if allowed is not None and j not in allowed:
                continue
            nd = d + (co[i] - co[j]).length
            if nd < dist.get(j, 9e9):
                dist[j] = nd
                heapq.heappush(heap, (nd, j))
    return dist


def build_tail(t):
    """Find the tail by growing a geodesic front from its far end: along the tail the front's area stays
    roughly constant; where it spills into the body the per-centimetre vertex count jumps - that is the root."""
    rest_pose()
    me = BODY.data
    co = [BODY.matrix_world @ v.co for v in me.vertices]
    hips = bone_world('Hips')
    tip_dir = Vector(t['tip_dir']).normalized()
    # graph over edges, welded by position so UV seams do not cut the tail
    kd = KDTree(len(co))
    for i, p in enumerate(co):
        kd.insert(p, i)
    kd.balance()
    canon = [min(j for _, j, _ in kd.find_range(p, 1e-4)) for p in co]
    adj = {}
    for e in me.edges:
        a, b = canon[e.vertices[0]], canon[e.vertices[1]]
        if a != b:
            adj.setdefault(a, set()).add(b)
            adj.setdefault(b, set()).add(a)
    low = [i for i in range(len(co)) if co[i].z < hips.z - 0.1]   # tails hang below the hips; hands do not
    seed = canon[max(low, key=lambda i: (co[i] - hips).dot(tip_dir))]
    dist = dijkstra(adj, co, [seed])
    step = 0.01
    bins = {}
    for i, d in dist.items():
        bins.setdefault(int(d / step), []).append(co[i])
    nb = max(bins) + 1
    radius = []
    for k in range(nb):
        pts = bins.get(k, [])
        if not pts:
            radius.append(radius[-1] if radius else 0.0)
            continue
        c = sum(pts, Vector()) / len(pts)
        radius.append(max((p - c).length for p in pts))
    log('tail seed', tuple(round(x, 2) for x in co[seed]), 'front radius cm', [round(r * 100) for r in radius[:160]])
    # the front stays tail-thick along the tail; at the root it wraps the body and its radius jumps
    cut = nb
    lo = int(t.get('min_len', 0.3) / step)
    for k in range(lo, nb - 3):
        ref = sorted(radius[lo // 2:k])
        tube = ref[len(ref) // 4]  # thin tube section; the curl near the tip reads wide
        if sum(radius[k:k + 3]) / 3 > max(t.get('jump', 1.6) * tube, t.get('root_radius', 0.1)):
            cut = k
            break
    limit = cut * step - t.get('root_trim', 0.02)
    inside = {i for i, d in dist.items() if d < limit}
    root = [i for i in inside if any(j not in inside for j in adj.get(i, ()))]
    droot = dijkstra(adj, co, root, allowed=inside)
    L = max(droot.values())
    members = [i for i in range(len(co)) if canon[i] in inside]
    log('tail verts', len(members), 'root at', round(limit, 3), 'm from the far end, length', round(L, 3))
    u_of = {i: droot.get(canon[i], 0.0) / L for i in members}
    # centreline: average position per geodesic slice (u = 0 root .. 1 tip)
    n = t.get('bones', 4)
    slices = [[] for _ in range(n + 1)]
    for i in members:
        slices[min(n, int(round(u_of[i] * n)))].append(co[i])
    pts = []
    for k in range(n + 1):
        s = slices[k] or slices[max(0, k - 1)]
        pts.append(sum(s, Vector()) / len(s))
    # bones
    bpy.context.view_layer.objects.active = ARM
    bpy.ops.object.mode_set(mode='EDIT')
    eb = ARM.data.edit_bones
    inv = ARM.matrix_world.inverted()
    prev = eb['Hips']
    names = []
    for k in range(n):
        b = eb.new(f'Tail{k + 1}')
        b.head = inv @ pts[k]
        b.tail = inv @ pts[k + 1]
        b.parent = prev
        b.use_connect = k > 0
        prev = b
        names.append(b.name)
    bpy.ops.object.mode_set(mode='OBJECT')
    # weights: blend along u between neighbouring tail bones; root blends with Hips
    for g in [vg for vg in BODY.vertex_groups]:
        g.remove(members)
    groups = {nm: (BODY.vertex_groups.get(nm) or BODY.vertex_groups.new(name=nm)) for nm in names + ['Hips']}
    for i in members:
        u = u_of[i] * n  # 0..n
        k = min(n - 1, int(u))
        f = u - k
        if k == 0 and f < 0.5:
            w = f / 0.5
            groups['Hips'].add([i], 1 - w, 'REPLACE')
            groups[names[0]].add([i], w, 'REPLACE')
        else:
            groups[names[k]].add([i], 1 - f * 0.5, 'REPLACE')
            if k + 1 < n:
                groups[names[k + 1]].add([i], f * 0.5, 'REPLACE')
    TAIL.extend(names)


if SPEC.get('tail'):
    build_tail(SPEC['tail'])


# ---------------------------------------------------------------- pose sampling
BONES = [pb.name for pb in ARM.pose.bones]


YAW = {}


def hip_facing(clip, frame):
    """Signed angle (rad, CCW from above) between the rest front and the hip line's front at `frame`."""
    set_action(SRC[clip])
    scene.frame_set(int(frame))
    legs = (ARM.matrix_world @ ARM.pose.bones['LeftUpLeg'].head) - (ARM.matrix_world @ ARM.pose.bones['RightUpLeg'].head)
    face = legs.cross(Vector((0, 0, 1)))
    face.z = 0
    face.normalize()
    return math.atan2(FORWARD.cross(face).z, FORWARD.dot(face))


def clip_yaw(clip):
    """Meshy fight clips start side-on (~50 deg off the rest facing), which turns the punch line toward the
    camera instead of the enemy. Turn each clip about the vertical so its first frame faces the rest front."""
    if clip in SPEC.get('no_yaw', []):
        YAW[clip] = 0.0
    if clip not in YAW:
        a = SRC[clip]
        ang = -hip_facing(clip, a.frame_range[0]) + math.radians(SPEC.get('yaw_bias_deg', 0))
        YAW[clip] = ang if abs(math.degrees(ang)) > 12 else 0.0
        log('yaw', clip, round(math.degrees(YAW[clip])))
    return YAW[clip]


def sample(clip, frame):
    """Local (matrix_basis) pose of every bone in `clip` at source frame (24 fps), yaw-corrected."""
    yaw = clip_yaw(clip)
    set_action(SRC[clip])
    scene.frame_set(int(frame), subframe=frame - int(frame))
    out = {}
    for pb in ARM.pose.bones:
        m = pb.matrix_basis
        if pb.name == 'Hips' and yaw:
            rest = pb.bone.matrix_local
            m = rest.inverted() @ Matrix.Rotation(yaw, 4, 'Z') @ rest @ m
        out[pb.name] = (m.to_translation(), m.to_quaternion())
    return out


def blend(a, b, w):
    out = {}
    for k in a:
        la, qa = a[k]
        lb, qb = b[k]
        if qa.dot(qb) < 0:
            qb = -qb
        out[k] = (la.lerp(lb, w), qa.slerp(qb, w))
    return out


def ease(x, kind):
    x = max(0.0, min(1.0, x))
    if kind == 'in':      # slow start, snap at the end (wind-ups into contact)
        return x * x * x
    if kind == 'out':     # snap start, settle (recoveries)
        return 1 - (1 - x) ** 3
    if kind == 'hold':
        return 0.0
    return x * x * (3 - 2 * x)


def author(name, timeline, extras, in_place):
    """timeline: [{t, clip, frame, ease}] key poses; between keys of the same clip the source frame is
    interpolated (re-timed motion); between different clips the two poses are blended."""
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    set_action(None)
    end = timeline[-1]['t']
    nfr = int(round(end * FPS))
    frames = []
    for f in range(nfr + 1):
        t = f / FPS
        k = max(i for i, key in enumerate(timeline) if key['t'] <= t + 1e-6)
        a = timeline[k]
        b = timeline[min(k + 1, len(timeline) - 1)]
        span = max(1e-6, b['t'] - a['t'])
        x = ease((t - a['t']) / span, b.get('ease', 'smooth')) if b is not a else 0.0
        if a['clip'] == b['clip']:
            pose = sample(a['clip'], a['frame'] + (b['frame'] - a['frame']) * x)
        else:
            pose = blend(sample(a['clip'], a['frame']), sample(b['clip'], b['frame']), x)
        frames.append((f, pose))
    # in-place: keep hips over its start position (the battle code moves the whole unit)
    h0 = frames[0][1]['Hips'][0].copy()
    set_action(act)
    for f, pose in frames:
        t = f / FPS
        for bn, (loc, q) in pose.items():
            pb = ARM.pose.bones[bn]
            if bn == 'Hips':
                loc = loc.copy()
                if in_place:
                    loc.x = h0.x + (loc.x - h0.x) * in_place
                    loc.z = h0.z + (loc.z - h0.z) * in_place  # Meshy hips: local Y is up, X/Z horizontal
                for e in extras:  # additive offsets (anticipation dips, lunges) in bone-local units
                    w = bump(t, e)
                    if w and e.get('bone', 'Hips') == 'Hips' and 'loc' in e:
                        loc += Vector(e['loc']) * w
            for e in extras:
                if e.get('bone') == bn and 'rot' in e:
                    w = bump(t, e)
                    if w:
                        q = q @ Quaternion(Vector(e['axis']), math.radians(e['rot']) * w)
            pb.rotation_mode = 'QUATERNION'
            pb.location = loc
            pb.rotation_quaternion = q
            pb.keyframe_insert('location', frame=f)
            pb.keyframe_insert('rotation_quaternion', frame=f)
    log('authored', name, round(end, 2), 's', nfr + 1, 'frames')
    return act


def bump(t, e):
    """Smooth 0->1->0 envelope between t0 and t1 peaking at tp."""
    t0, tp, t1 = e['t']
    if t <= t0 or t >= t1:
        return 0.0
    x = (t - t0) / (tp - t0) if t < tp else (t1 - t) / (t1 - tp)
    return x * x * (3 - 2 * x)


def resample(src_name, dst_name):
    """Copy a Meshy clip onto the 30 fps timeline under its AH_ name (keeps tail bones keyable)."""
    a = SRC[src_name]
    f0, f1 = a.frame_range
    dur = (f1 - f0) / src_fps
    tl = [{'t': 0.0, 'clip': src_name, 'frame': f0}, {'t': dur, 'clip': src_name, 'frame': f1, 'ease': 'linear'}]
    return author(dst_name, tl, [], 0)


# ---------------------------------------------------------------- build clips
made = []
for src, dst in {**BASE_CLIPS, **keep}.items():
    if src in SRC and dst not in SPEC.get('author', {}):
        made.append(resample(src, dst).name)
for name, clip in SPEC.get('author', {}).items():
    made.append(author(name, clip['timeline'], clip.get('extras', []), clip.get('in_place', 0.0)).name)


# ---------------------------------------------------------------- tail follow-through
def bake_tail(act):
    """Lagged sway: each tail bone rotates against the hips' angular motion plus a slow idle swish."""
    set_action(act)
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    t = SPEC['tail']
    hip_q = []
    for f in range(f0, f1 + 1):
        scene.frame_set(f)
        hip_q.append((ARM.matrix_world @ ARM.pose.bones['Hips'].matrix).to_quaternion())
    for k, bn in enumerate(TAIL):
        pb = ARM.pose.bones[bn]
        pb.rotation_mode = 'QUATERNION'
        lag = (k + 1) * t.get('lag_frames', 3)
        for i, f in enumerate(range(f0, f1 + 1)):
            j = max(0, i - lag)
            d = hip_q[j].inverted() @ hip_q[i]          # how far the hips turned since `lag` frames ago
            ang = d.angle if d.angle < math.pi else d.angle - 2 * math.pi
            swing = -ang * t.get('follow', 0.35) * (1 + 0.3 * k)
            sway = math.sin((f / FPS) * 2 * math.pi / t.get('period', 1.6) - k * 0.7) * math.radians(t.get('sway_deg', 6))
            q = Quaternion((0, 0, 1), swing + sway) @ Quaternion((1, 0, 0), sway * 0.4)
            pb.rotation_quaternion = q
            pb.keyframe_insert('rotation_quaternion', frame=f)


if TAIL:
    for n in made:
        bake_tail(bpy.data.actions[n])

# drop the raw Meshy clips; keep only AH_* in the export
for a in list(bpy.data.actions):
    if not a.name.startswith('AH_'):
        bpy.data.actions.remove(a)
set_action(bpy.data.actions.get('AH_battle_guard'))
log('clips', sorted(a.name for a in bpy.data.actions))

# ---------------------------------------------------------------- save / export
JOB.mkdir(parents=True, exist_ok=True)
# the same prop file imported twice (paired gauntlets, two kunai) brings duplicate images: share them
import hashlib
seen = {}
for img in list(bpy.data.images):
    if img.packed_file is None:
        continue
    key = hashlib.md5(img.packed_file.data).hexdigest()
    if key in seen:
        img.user_remap(seen[key])
        bpy.data.images.remove(img)
    else:
        seen[key] = img
for img in bpy.data.images:
    if img.size[0] > SPEC.get('max_tex', 2048):
        img.scale(SPEC.get('max_tex', 2048), int(img.size[1] * SPEC.get('max_tex', 2048) / img.size[0]))
bpy.ops.wm.save_as_mainfile(filepath=str(JOB / f'{UID}-battle.blend'))
if EXPORT:
    OUT.mkdir(parents=True, exist_ok=True)
    old = OUT / 'model.fbx'
    backup = JOB / 'old-model.fbx'
    if old.exists() and not backup.exists():
        backup.write_bytes(old.read_bytes())
    for f in list(OUT.glob('texture_*')):
        if f.suffix in ('.png', '.jpg'):
            f.unlink()
    normals = {n.image.name for m in bpy.data.materials if m.node_tree for n in m.node_tree.nodes
               if n.type == 'TEX_IMAGE' and n.image and any(l.to_node.type == 'NORMAL_MAP' for o in n.outputs for l in o.links)}
    for i, img in enumerate(bpy.data.images):
        if img.type != 'IMAGE':
            continue
        is_normal = img.name in normals
        path = OUT / f'texture_{i}_{bpy.path.clean_name(img.name)}.{"png" if is_normal else "jpg"}'
        img.filepath_raw = str(path)
        img.file_format = 'PNG' if is_normal else 'JPEG'
        img.save(quality=90)
        img.filepath = str(path)
    for meta in list(OUT.glob('texture_*.meta')):  # metas of textures that no longer exist
        if not meta.with_suffix('').exists():
            meta.unlink()
    bpy.ops.export_scene.fbx(filepath=str(OUT / 'model.fbx'), object_types={'ARMATURE', 'MESH'}, add_leaf_bones=False,
                             bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                             bake_anim_force_startend_keying=True, path_mode='RELATIVE', embed_textures=False,
                             axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL')
    contacts = {k: v.get('contact') for k, v in SPEC.get('author', {}).items()}
    (OUT / 'provenance.json').write_text(json.dumps({
        'model': str(JOB / SPEC.get('rig', 'rigged.glb')), 'spec': str(spec_path), 'pipeline': 'meshy-autorig+build_battle_rig',
        'clips': sorted(a.name for a in bpy.data.actions), 'contacts': contacts, 'tail_bones': TAIL,
        'props': [o.name for o in bpy.data.objects if o.name.startswith('Weapon_')]}, indent=2))
    log('exported', OUT / 'model.fbx')
