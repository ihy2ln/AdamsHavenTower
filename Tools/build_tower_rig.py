"""Build a Tower Mode (Fallout Shelter task) chibi from a Meshy auto-rig with Meshy preset clips.

blender -b --factory-startup --python Tools/build_tower_rig.py -- Tools/tower_rigs/<id>.json [--no-export] [--preview]

Meshy did the template part: the 30K remesh, the humanoid auto-rig (MeshyRig, 24 bones) and 18 preset clips.
This script does the specialised part:
  texture  re-unwrap into large islands, rebake Meshy's colour, then flatten it to anime cel colours with the
           character's own 2D-art palette (Tools/anime_cel_texture.py)
  clips    Meshy presets renamed to AH_* tower clips, root drift removed so tasks play in place
  author   AH_task_forge / AH_task_kitchen / AH_task_well (motions Meshy has no preset for), IK-solved arm paths
           over the idle stance, baked to plain FK keys
  tail     bone chain grown along the tail (Ghislaine, Kaela); runtime springs swing it
  jiggle   Breast_L/R on the chest and Butt_L/R on the hips with soft falloff weights; runtime springs drive them
  props    Prop_* held tools skinned 100% to a hand, Set_* station pieces; Unity shows the ones a clip needs
Output: Assets/Resources/AdamsHaven/TowerChibi3D/<id>/model.fbx + texture + provenance.json, and
Game Assets/characters/chibi-meshy-tasks-v1/<id>/<id>-tower.blend for hand edits.
"""
import bpy, bmesh, sys, json, math, pathlib, heapq, subprocess
from mathutils import Matrix, Vector, Quaternion
from mathutils.kdtree import KDTree

ROOT = pathlib.Path(__file__).resolve().parent.parent
argv = sys.argv[sys.argv.index('--') + 1:]
spec_path = pathlib.Path(argv[0])
SPEC = json.loads(spec_path.read_text(encoding='utf-8-sig'))
EXPORT = '--no-export' not in argv
PREVIEW = '--preview' in argv
UID = SPEC['id']
JOB = pathlib.Path(SPEC.get('job', f'S:/AI/Game/Game Assets/characters/chibi-meshy-tasks-v1/{UID}'))
OUT = ROOT / 'Assets' / 'Resources' / 'AdamsHaven' / 'TowerChibi3D' / UID
FPS = 24
SYS_PY = SPEC.get('python', 'python')

CLIPS = {  # Meshy preset -> tower clip (room / life state it serves)
    'Idle_02': 'AH_idle', 'Walking': 'AH_walk', 'Running': 'AH_run', 'Injured_Walk': 'AH_walk_injured',
    'Knock_Down': 'AH_knocked_down', 'Sleep_Normally': 'AH_sleep', 'Chair_Sit_Idle_F': 'AH_sit',
    'Stand_and_Chat': 'AH_chat', 'Alert': 'AH_task_guard',
    'Female_Crouch_Pick_Up_Place_Side': 'AH_task_warehouse', 'Pull_Radish': 'AH_task_farm',
    'Checkout_Gesture': 'AH_task_market', 'Charged_Axe_Chop': 'AH_task_lumber', 'Heavy_Hammer_Swing': 'AH_task_quarry',
    'Stand_and_Drink': 'AH_task_tavern', 'mage_soell_cast': 'AH_task_heart', 'Collect_Object': 'AH_task_haul',
    'Boxing_Practice': 'AH_task_train',
}
KEEP_ROOT = {'AH_knocked_down', 'AH_sleep', 'AH_sit'}  # these clips travel/settle on purpose
# which props each clip shows (Unity reads this from provenance.json)
CLIP_PROPS = {
    'AH_task_forge': ['Prop_Hammer', 'Set_Anvil'], 'AH_task_kitchen': ['Prop_Ladle', 'Set_Pot'],
    'AH_task_well': ['Set_Rope'], 'AH_task_lumber': ['Prop_Axe'], 'AH_task_quarry': ['Prop_Pickaxe'],
    'AH_task_tavern': ['Prop_Mug'],
}


def log(*a):
    print('TOWER', *a, flush=True)


# ---------------------------------------------------------------- load
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.fps = FPS
bpy.ops.import_scene.gltf(filepath=str(JOB / 'meshy_rigged_anim.glb'))
for o in list(bpy.data.objects):
    if o.type == 'MESH' and o.name.startswith('Icosphere'):
        bpy.data.objects.remove(o, do_unlink=True)
ARM = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
BODY = next(o for o in bpy.data.objects if o.type == 'MESH')
BODY.name = 'Body'
ARM.name = 'Armature'
ARM.animation_data_create()
SRC = {a.name: a for a in bpy.data.actions}
for a in SRC.values():
    a.use_fake_user = True


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


def fcurves(act):
    if hasattr(act, 'fcurves'):
        return act.fcurves
    from bpy_extras import anim_utils
    cb = anim_utils.action_get_channelbag_for_slot(act, act.slots[0])
    return cb.fcurves if cb else []


# ---------------------------------------------------------------- weld the UV-seam splits (glTF splits every seam)
bm = bmesh.new()
bm.from_mesh(BODY.data)
n0 = len(bm.verts)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
bm.to_mesh(BODY.data)
bm.free()
BODY.data.update()
log('welded', n0, '->', len(BODY.data.vertices), 'verts')

# ---------------------------------------------------------------- recentre on the hips (the tail widens the bbox)
rest_pose()
hip_mid = (bone_world('LeftUpLeg') + bone_world('RightUpLeg')) / 2
off_world = Vector((hip_mid.x, hip_mid.y, 0.0))
off_arm = ARM.matrix_world.inverted().to_3x3() @ off_world
bpy.context.view_layer.objects.active = ARM
bpy.ops.object.mode_set(mode='EDIT')
for eb in ARM.data.edit_bones:
    eb.head -= off_arm
    eb.tail -= off_arm
bpy.ops.object.mode_set(mode='OBJECT')
BODY.data.transform(Matrix.Translation(-(BODY.matrix_world.inverted().to_3x3() @ off_world)))
BODY.data.update()
rest_pose()
FORWARD = Vector((0, -1, 0))   # Meshy exports face -Y
RIGHT = Vector((-1, 0, 0))     # character's right
UP = Vector((0, 0, 1))
HEIGHT = max((BODY.matrix_world @ v.co).z for v in BODY.data.vertices)
log('recentred by', tuple(round(x, 3) for x in off_world), 'height', round(HEIGHT, 3))


# ---------------------------------------------------------------- texture: re-unwrap, rebake, anime cel pass
def texture_pass():
    me = BODY.data
    mat = BODY.active_material
    nt = mat.node_tree
    bsdf0 = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    link = bsdf0.inputs['Base Color'].links[0]
    src_tex = link.from_node
    while src_tex.type != 'TEX_IMAGE':   # glTF may route colour through a mix / multiply node
        src_tex = next(l.from_node for i in src_tex.inputs for l in i.links if l.from_node.type in ('TEX_IMAGE', 'MIX', 'MIX_RGB'))
    old_uv = me.uv_layers[0].name
    # new large-island layout
    new_uv = me.uv_layers.new(name='UVGame')
    me.uv_layers.active = new_uv
    bpy.ops.object.select_all(action='DESELECT')
    BODY.select_set(True)
    bpy.context.view_layer.objects.active = BODY
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(70), island_margin=0.004, area_weight=0.6, correct_aspect=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    size = SPEC.get('atlas', 2048)
    raw = bpy.data.images.new('albedo_raw', size, size, alpha=False)
    mask = bpy.data.images.new('uv_mask', size, size, alpha=False)
    # emission bake: read the Meshy colour through its own UVs, write through UVGame
    uvn = nt.nodes.new('ShaderNodeUVMap'); uvn.uv_map = old_uv
    nt.links.new(uvn.outputs['UV'], src_tex.inputs['Vector'])
    emit = nt.nodes.new('ShaderNodeEmission')
    out = next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL')
    prev_surface = out.inputs['Surface'].links[0].from_socket if out.inputs['Surface'].links else None
    nt.links.new(src_tex.outputs['Color'], emit.inputs['Color'])
    nt.links.new(emit.outputs['Emission'], out.inputs['Surface'])
    target = nt.nodes.new('ShaderNodeTexImage')
    for n in nt.nodes:
        n.select = False
    target.select = True
    nt.nodes.active = target
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 1
    scene.cycles.device = 'CPU'
    rest_pose()
    target.image = raw
    bpy.ops.object.bake(type='EMIT', margin=12, use_clear=True)
    # coverage mask: white emission, no margin
    nt.links.remove(emit.inputs['Color'].links[0])
    emit.inputs['Color'].default_value = (1, 1, 1, 1)
    target.image = mask
    bpy.ops.object.bake(type='EMIT', margin=0, use_clear=True)
    raw.filepath_raw = str(JOB / 'albedo_raw.png'); raw.file_format = 'PNG'; raw.save()
    mask.filepath_raw = str(JOB / 'uv_mask.png'); mask.file_format = 'PNG'; mask.save()
    # anime cel colours from the 2D art palette (system Python: numpy/scipy)
    cel_path = JOB / 'albedo_cel.png'
    cmd = [SYS_PY, str(ROOT / 'Tools' / 'anime_cel_texture.py'), str(JOB / 'albedo_raw.png'), str(JOB / 'source.webp'),
           str(cel_path), '--mask', str(JOB / 'uv_mask.png'), '--colors', str(SPEC.get('colors', 28))]
    r = subprocess.run(cmd, capture_output=True, text=True)
    log('cel pass', r.stdout.strip(), r.stderr.strip()[-400:])
    cel = bpy.data.images.load(str(cel_path))
    cel.name = f'{UID}_albedo'
    # final material: one cel texture through UVGame (the AnimeToon shader replaces it in Unity)
    for n in list(nt.nodes):
        if n not in (out,):
            nt.nodes.remove(n)
    bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    tex = nt.nodes.new('ShaderNodeTexImage'); tex.image = cel
    nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value = 1.0
    bsdf.inputs['Metallic'].default_value = 0.0
    nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    mat.name = f'{UID}_toon'
    me.uv_layers.remove(me.uv_layers[old_uv])
    me.uv_layers.active = me.uv_layers['UVGame']
    me.uv_layers['UVGame'].active_render = True
    for img in list(bpy.data.images):
        if img not in (cel,) and img.users == 0:
            bpy.data.images.remove(img)


if SPEC.get('texture', True):
    texture_pass()


# ---------------------------------------------------------------- weights helpers
def dominant_groups():
    names = {g.index: g.name for g in BODY.vertex_groups}
    dom = []
    for v in BODY.data.vertices:
        best = max(v.groups, key=lambda g: g.weight, default=None)
        dom.append(names[best.group] if best else None)
    return dom


def give_weight(group_name, weights):
    """weights: {vertex index: w}. Takes w from the vertex's other groups (proportionally) and gives it to group."""
    vg = BODY.vertex_groups.get(group_name) or BODY.vertex_groups.new(name=group_name)
    for i, w in weights.items():
        if w <= 0.002:
            continue
        v = BODY.data.vertices[i]
        for g in v.groups:
            g.weight *= (1.0 - w)
        vg.add([i], w, 'ADD')


def add_bone(name, head, tail, parent):
    bpy.context.view_layer.objects.active = ARM
    bpy.ops.object.mode_set(mode='EDIT')
    inv = ARM.matrix_world.inverted()
    eb = ARM.data.edit_bones.new(name)
    eb.head = inv @ head
    eb.tail = inv @ tail
    eb.parent = ARM.data.edit_bones[parent]
    eb.use_deform = True
    bpy.ops.object.mode_set(mode='OBJECT')


# ---------------------------------------------------------------- jiggle bones (chest / hips)
JIGGLE = []


def build_jiggle():
    rest_pose()
    co = [BODY.matrix_world @ v.co for v in BODY.data.vertices]
    dom = dominant_groups()
    cx = 0.0
    sh_l, sh_r = bone_world('LeftArm'), bone_world('RightArm')
    half = abs(sh_l.x - sh_r.x) / 2
    torso = {'Spine02', 'Spine01', 'Spine', 'neck', 'LeftShoulder', 'RightShoulder', 'Hips'}
    pelvis = {'Hips', 'LeftUpLeg', 'RightUpLeg', 'Spine02'}
    js = SPEC.get('jiggle', {})
    jobs = []
    if js.get('chest', 1.0) > 0:
        z0 = bone_world('Spine02').z + 0.25 * (sh_l.z - bone_world('Spine02').z)
        z1 = sh_l.z - 0.02
        for side, name, sgn in (('L', 'Breast_L', 1), ('R', 'Breast_R', -1)):
            cand = [i for i, p in enumerate(co) if z0 < p.z < z1 and 0.05 * half < sgn * (p.x - cx) < 0.85 * half
                    and dom[i] in torso]
            if not cand:
                continue
            tip = min(cand, key=lambda i: co[i].y)           # most forward point
            jobs.append((name, 'Spine01', co[tip], FORWARD, cand, js.get('chest_radius', 0.55) * half))
    if js.get('hips', 1.0) > 0:
        z0 = bone_world('LeftUpLeg').z - 0.16 * HEIGHT * 0.5
        z1 = bone_world('Hips').z + 0.03
        for side, name, sgn in (('L', 'Butt_L', 1), ('R', 'Butt_R', -1)):
            cand = [i for i, p in enumerate(co) if z0 < p.z < z1 and 0.03 * half < sgn * (p.x - cx) < 0.9 * half
                    and dom[i] in pelvis]
            if not cand:
                continue
            tip = max(cand, key=lambda i: co[i].y)           # most rearward point
            jobs.append((name, 'Hips', co[tip], -FORWARD, cand, js.get('hips_radius', 0.6) * half))
    for name, parent, tip, out_dir, cand, radius in jobs:
        centre = tip - out_dir * radius * 0.55
        add_bone(name, centre, centre + out_dir * radius * 0.8, parent)
        weights = {}
        for i in cand:
            d = (co[i] - centre)
            along = d.dot(out_dir)
            if along < -0.15 * radius:          # only the protruding front / back half
                continue
            r = d.length / radius
            if r >= 1:
                continue
            w = (1 - r) ** 1.4 * min(1.0, (along + 0.15 * radius) / (0.5 * radius))
            weights[i] = 0.85 * max(0.0, min(1.0, w))
        give_weight(name, weights)
        JIGGLE.append({'bone': name, 'parent': parent, 'radius': round(radius, 4), 'verts': len(weights)})
        log('jiggle', name, 'verts', len(weights), 'radius', round(radius, 3))


# ---------------------------------------------------------------- tail chain (from build_battle_rig)
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
    rest_pose()
    me = BODY.data
    co = [BODY.matrix_world @ v.co for v in me.vertices]
    hips = bone_world('Hips')
    tip_dir = Vector(t['tip_dir']).normalized()
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
    low = [i for i in range(len(co)) if co[i].z < hips.z - 0.1 and (co[i] - hips).dot(Vector((0, 1, 0))) > t.get('min_back', -1.0)]
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
    log('tail seed', tuple(round(x, 2) for x in co[seed]), 'front radius cm', [round(r * 100) for r in radius[:120]])
    cut = nb
    lo = int(t.get('min_len', 0.3) / step)
    for k in range(lo, nb - 3):
        ref = sorted(radius[lo // 2:k])
        tube = ref[len(ref) // 4]
        if sum(radius[k:k + 3]) / 3 > max(t.get('jump', 1.6) * tube, t.get('root_radius', 0.1)):
            cut = k
            break
    if t.get('neck_search'):  # curled tips read wide; the real root is the narrowest ring just before the body
        win = range(max(lo, cut - int(t['neck_search'])), cut)
        if len(win):
            cut = min(win, key=lambda k: sum(radius[k - 1:k + 2]))
    limit = cut * step - t.get('root_trim', 0.02)
    inside = {i for i, d in dist.items() if d < limit}
    root = [i for i in inside if any(j not in inside for j in adj.get(i, ()))]
    droot = dijkstra(adj, co, root, allowed=inside)
    L = max(droot.values())
    members = [i for i in range(len(co)) if canon[i] in inside]
    log('tail verts', len(members), 'root at', round(limit, 3), 'm from the far end, length', round(L, 3))
    u_of = {i: droot.get(canon[i], 0.0) / L for i in members}
    n = t.get('bones', 4)
    slices = [[] for _ in range(n + 1)]
    for i in members:
        slices[min(n, int(round(u_of[i] * n)))].append(co[i])
    pts = []
    for k in range(n + 1):
        s = slices[k] or slices[max(0, k - 1)]
        pts.append(sum(s, Vector()) / len(s))
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
    for g in [vg for vg in BODY.vertex_groups]:
        g.remove(members)
    groups = {nm: (BODY.vertex_groups.get(nm) or BODY.vertex_groups.new(name=nm)) for nm in names + ['Hips']}
    for i in members:
        u = u_of[i] * n
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


def seg_dist(p, a, b):
    ab = b - a
    u = max(0.0, min(1.0, (p - a).dot(ab) / max(1e-9, ab.length_squared)))
    return (a + ab * u - p).length


def fix_far_weights():
    """Meshy's auto-weights glue long hair (and skirt edges) to the nearest arm / leg. A vertex far outside a limb's
    own capsule is not part of that limb: hand its weight to the torso so hair stays put when an arm lifts."""
    rest_pose()
    co = [BODY.matrix_world @ v.co for v in BODY.data.vertices]
    names = {g.index: g.name for g in BODY.vertex_groups}
    limbs = {}
    for s in ('Left', 'Right'):
        limbs[f'{s}Arm'] = ('Spine', bone_world(f'{s}Arm'), bone_world(f'{s}Arm', True))
        limbs[f'{s}ForeArm'] = ('Spine', bone_world(f'{s}ForeArm'), bone_world(f'{s}ForeArm', True))
        limbs[f'{s}Hand'] = ('Spine', bone_world(f'{s}Hand'), bone_world(f'{s}Hand_End', True))
        if SPEC.get('fix_legs'):   # only where long hair reaches the thighs; thick thighs would lose real skin
            limbs[f'{s}UpLeg'] = ('Hips', bone_world(f'{s}UpLeg'), bone_world(f'{s}UpLeg', True))
    # capsule reach from bone length (vertex statistics are useless here: hair can outnumber the arm skin 10:1)
    reach = {'Arm': 0.45, 'ForeArm': 0.45, 'Hand': 0.9, 'UpLeg': 0.5}
    k_scale = SPEC.get('limb_scale', 1.0)
    radius = {}
    for n, (_, a, b) in limbs.items():
        kind = next(s for s in ('ForeArm', 'UpLeg', 'Hand', 'Arm') if n.endswith(s))
        radius[n] = reach[kind] * (b - a).length * k_scale
    moved = {k: 0 for k in limbs}
    to_groups = {t: (BODY.vertex_groups.get(t) or BODY.vertex_groups.new(name=t)) for t in ('Spine', 'Hips')}
    tail_set = set(TAIL)
    for i, v in enumerate(BODY.data.vertices):
        if any(names[g.group] in tail_set for g in v.groups):
            continue
        entries = [(g.group, g.weight) for g in v.groups]   # snapshot: adding to a group invalidates v.groups
        for gi, w in entries:
            n = names.get(gi)
            if n not in limbs or w <= 0:
                continue
            side = 'Left' if n.startswith('Left') else 'Right'
            chain = [f'{side}{s}' for s in (('Arm', 'ForeArm', 'Hand') if 'Leg' not in n else ('UpLeg',))]
            if 'Leg' in n:
                chain_ok = seg_dist(co[i], bone_world(f'{side}UpLeg'), bone_world(f'{side}Leg', True)) <= radius[n]
            else:
                chain_ok = any(seg_dist(co[i], limbs[c][1], limbs[c][2]) <= radius[c] for c in chain)
            if not chain_ok:
                BODY.vertex_groups[n].remove([i])
                to_groups[limbs[n][0]].add([i], w, 'ADD')
                moved[n] += 1
    log('far weights moved', {k: v for k, v in moved.items() if v}, 'radii', {k: round(v, 3) for k, v in radius.items()})


def srgb_lab(c):
    c = [((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c]
    X = (0.4124 * c[0] + 0.3576 * c[1] + 0.1805 * c[2]) / 0.95047
    Y = 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]
    Z = (0.0193 * c[0] + 0.1192 * c[1] + 0.9505 * c[2]) / 1.08883
    f = [v ** (1 / 3) if v > 0.008856 else 7.787 * v + 16 / 116 for v in (X, Y, Z)]
    return Vector((116 * f[1] - 16, 500 * (f[0] - f[1]), 200 * (f[1] - f[2])))


def vertex_colours():
    """Average cel-albedo colour per vertex (sampled through UVGame)."""
    import numpy as np
    me = BODY.data
    img = bpy.data.images.get(f'{UID}_albedo')
    if img is None or 'UVGame' not in me.uv_layers:
        return None
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, img.channels)[:, :, :3]
    uv = me.uv_layers['UVGame'].data
    acc = np.zeros((len(me.vertices), 3), np.float32)
    cnt = np.zeros(len(me.vertices), np.float32)
    for loop in me.loops:
        u, v = uv[loop.index].uv
        acc[loop.vertex_index] += px[min(h - 1, max(0, int(v * h))), min(w - 1, max(0, int(u * w)))]
        cnt[loop.vertex_index] += 1
    return acc / np.maximum(cnt, 1)[:, None]


def fix_hair_weights():
    """Meshy's auto-weights tie long hair / braids to the nearest arm (and thigh), so they stretch whenever the arm
    moves - even in idle, where the arms drop from the A-pose. Hair is one palette colour, the arm skin another:
    find the hair colour on the crown and hand every hair-coloured vertex from limb bones to Head / Spine."""
    rest_pose()
    cols = vertex_colours()
    if cols is None:
        log('hair fix skipped (no albedo)')
        return
    co = [BODY.matrix_world @ v.co for v in BODY.data.vertices]
    top = max(p.z for p in co)
    neck = bone_world('neck').z
    span = top - neck
    crown = [i for i, p in enumerate(co) if p.z > neck + 0.75 * span and abs(p.x) < 0.25 * span]
    labs = [srgb_lab(cols[i]) for i in range(len(co))]
    crown_labs = sorted(crown, key=lambda i: labs[i].x)
    hair = labs[crown_labs[len(crown_labs) // 2]] if crown_labs else None
    if hair is None:
        return
    thr = SPEC.get('hair_threshold', 30.0)
    hairy = [(labs[i] - hair).length <= thr for i in range(len(co))]
    # tight limb capsules: wrists, bracelets, gloves and the arm skin itself are never hair
    caps = []
    for s in ('Left', 'Right'):
        for a, b in ((f'{s}Arm', f'{s}Arm'), (f'{s}ForeArm', f'{s}ForeArm'), (f'{s}Hand', f'{s}Hand_End')):
            h0, h1 = bone_world(a), bone_world(b, True)
            caps.append((h0, h1, SPEC.get('arm_core', 0.4) * (h1 - h0).length))
    core = [any(seg_dist(co[i], a, b) < r for a, b, r in caps) for i in range(len(co))]
    skin = [any(seg_dist(co[i], a, b) < r * SPEC.get('skin_core', 0.45) for a, b, r in caps) for i in range(len(co))]
    # hair region: flood from the crown through hair-coloured vertices (a bracelet can't be reached: skin separates
    # it from the hair), then two rings for strand shading outside the arm capsules
    adj = [[] for _ in co]
    for e in BODY.data.edges:
        a, b = e.vertices
        adj[a].append(b); adj[b].append(a)
    region = set(i for i in crown if hairy[i])
    front = list(region)
    while front:
        nxt = []
        for i in front:
            for j in adj[i]:
                if j not in region and hairy[j] and not skin[j]:
                    region.add(j); nxt.append(j)
        front = nxt
    for _ in range(SPEC.get('hair_dilate', 2)):
        ring = {j for i in region for j in adj[i] if j not in region and not core[j]}
        region |= ring
    # braid beads, ribbons and hair ties: small non-hair islands mostly surrounded by hair belong to the hair
    for _ in range(SPEC.get('enclose_passes', 4)):
        border = {j for i in region for j in adj[i] if j not in region and not core[j]}
        enclosed = set()
        for j in border:
            seen, front = {j}, [j]
            for _ in range(SPEC.get('enclose_rings', 3)):
                front = [k for i in front for k in adj[i] if k not in seen]
                seen.update(front)
            if sum(1 for k in seen if k in region) > 0.62 * len(seen):
                enclosed.add(j)
        if not enclosed:
            break
        region |= enclosed
    names = {g.index: g.name for g in BODY.vertex_groups}
    dom = dominant_groups()
    # each arm's own surface: flood from the hand through arm-dominated vertices that are not hair. Sleeves and
    # bracelets are reached; a braid (and its beads) hanging beside the arm is not, so its arm weights are wrong.
    arm_region = {}
    for s in ('Left', 'Right'):
        bones = {f'{s}Arm', f'{s}ForeArm', f'{s}Hand', f'{s}Hand_End'}
        segs = [(bone_world(f'{s}ForeArm'), bone_world(f'{s}ForeArm', True)), (bone_world(f'{s}Hand'), bone_world(f'{s}Hand_End', True)),
                (bone_world(f'{s}Arm'), bone_world(f'{s}Arm', True))]
        thin = [0.18 * (b - a).length for a, b in segs]
        seeds = [i for i in range(len(co)) if dom[i] in bones and i not in region
                 and any(seg_dist(co[i], a, b) < r for (a, b), r in zip(segs, thin))]
        reg = set(seeds)
        front = list(seeds)
        while front:
            nxt = []
            for i in front:
                for j in adj[i]:
                    if j not in reg and dom[j] in bones and j not in region and (core[j] or s not in SPEC.get('arm_flood_core', [])):
                        reg.add(j); nxt.append(j)
            front = nxt
        arm_region[s] = (bones, reg)
    is_leg = lambda n: n.endswith(('UpLeg', 'Leg'))
    head_g = BODY.vertex_groups.get('Head') or BODY.vertex_groups.new(name='Head')
    spine_g = BODY.vertex_groups.get('Spine') or BODY.vertex_groups.new(name='Spine')
    tail_set = set(TAIL)
    moved = 0
    for i, v in enumerate(BODY.data.vertices):
        if skin[i]:
            continue
        entries = [(g.group, g.weight) for g in v.groups]
        if any(names.get(gi) in tail_set for gi, _ in entries):
            continue
        drop = []
        for gi, w in entries:
            n = names.get(gi, '')
            for s, (bones, reg) in arm_region.items():
                if n in bones and i not in reg and (i in region or dom[i] in bones):
                    drop.append((n, w))
            if is_leg(n) and i in region:
                drop.append((n, w))
        take = sum(w for _, w in drop)
        if take <= 0:
            continue
        for n, _ in drop:
            BODY.vertex_groups[n].remove([i])
        k = max(0.0, min(1.0, (co[i].z - (neck - 0.18 * (top - neck))) / (0.3 * (top - neck))))
        k = k * k * (3 - 2 * k)
        if k > 0:
            head_g.add([i], take * k, 'ADD')
        if k < 1:
            spine_g.add([i], take * (1 - k), 'ADD')
        moved += 1
    log('hair fix: colour', tuple(round(x) for x in hair), 'region', len(region), 'arm regions',
        {s: len(r) for s, (_, r) in arm_region.items()}, 'moved', moved, 'verts off limb bones')
    for b in SPEC.get('braids', []):
        build_braid(b, region, adj, co)


def build_braid(b, region, adj, co):
    """A braid hanging from the head becomes its own spring chain (Braid<Side>1..N, parented to Head): seeded at the
    lowest hair vertex on its side, grown up the hair region geodesically for `length` metres."""
    sgn = 1 if b['side'] == 'L' else -1
    cand = [i for i in region if sgn * co[i].x > b.get('min_x', 0.05)]
    if not cand:
        log('braid', b['side'], 'no candidates'); return
    tip = min(cand, key=lambda i: co[i].z)
    dist = {tip: 0.0}
    heap = [(0.0, tip)]
    L = b.get('length', 0.4)
    while heap:
        d, i = heapq.heappop(heap)
        if d > dist.get(i, 9e9) or d > L:
            continue
        for j in adj[i]:
            if j not in region:
                continue
            nd = d + (co[i] - co[j]).length
            if nd < dist.get(j, 9e9) and nd <= L:
                dist[j] = nd
                heapq.heappush(heap, (nd, j))
    members = list(dist)
    n = b.get('bones', 4)
    slices = [[] for _ in range(n + 1)]
    for i in members:
        slices[min(n, int(round((1 - dist[i] / L) * n)))].append(co[i])   # 0 = root (top) .. n = tip
    pts = []
    for k in range(n + 1):
        s = slices[k] or (slices[k - 1] if k else slices[1])
        pts.append(sum(s, Vector()) / len(s))
    prefix = f"Braid{b['side']}"
    bpy.context.view_layer.objects.active = ARM
    bpy.ops.object.mode_set(mode='EDIT')
    eb = ARM.data.edit_bones
    inv = ARM.matrix_world.inverted()
    prev = eb[b.get('parent', 'Head')]
    names = []
    for k in range(n):
        bone = eb.new(f'{prefix}{k + 1}')
        bone.head = inv @ pts[k]
        bone.tail = inv @ pts[k + 1]
        bone.parent = prev
        bone.use_connect = k > 0
        prev = bone
        names.append(bone.name)
    bpy.ops.object.mode_set(mode='OBJECT')
    for g in list(BODY.vertex_groups):
        g.remove(members)
    groups = {nm: (BODY.vertex_groups.get(nm) or BODY.vertex_groups.new(name=nm)) for nm in names + [b.get('parent', 'Head')]}
    for i in members:
        u = (1 - dist[i] / L) * n
        k = min(n - 1, int(u))
        f = u - k
        if k == 0 and f < 0.5:      # blend into the head at the root
            w = f / 0.5
            groups[b.get('parent', 'Head')].add([i], 1 - w, 'REPLACE')
            groups[names[0]].add([i], w, 'REPLACE')
        else:
            groups[names[k]].add([i], 1 - f * 0.5, 'REPLACE')
            if k + 1 < n:
                groups[names[k + 1]].add([i], f * 0.5, 'REPLACE')
    BRAIDS.append({'chain': names, 'verts': len(members)})
    log('braid', prefix, 'verts', len(members), 'tip', tuple(round(x, 2) for x in co[tip]))


BRAIDS = []


if SPEC.get('tail'):
    build_tail(SPEC['tail'])
if SPEC.get('fix_far_weights', False):
    fix_far_weights()
if SPEC.get('hair_fix', True):
    fix_hair_weights()
build_jiggle()


# ---------------------------------------------------------------- Meshy clips -> AH_* (rename, remove root drift)
def detrend_root(act):
    """Hips local X/Z are horizontal on MeshyRig: remove the straight-line drift so the clip plays in place."""
    for fc in fcurves(act):
        if fc.data_path == 'pose.bones["Hips"].location' and fc.array_index in (0, 2) and len(fc.keyframe_points) > 1:
            kp = fc.keyframe_points
            f0, v0 = kp[0].co.x, kp[0].co.y
            f1, v1 = kp[-1].co.x, kp[-1].co.y
            for k in kp:
                drift = (v1 - v0) * (k.co.x - f0) / max(1e-6, f1 - f0)
                k.co.y -= drift
                k.handle_left.y -= drift
                k.handle_right.y -= drift
            fc.update()


MADE = []
for src, dst in CLIPS.items():
    act = SRC.get(src)
    if act is None:
        log('missing clip', src)
        continue
    act.name = dst
    if dst not in KEEP_ROOT:
        detrend_root(act)
    MADE.append(dst)


# ---------------------------------------------------------------- authored task clips (IK paths over the idle stance)
def smooth(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


def ease_in(x):
    x = max(0.0, min(1.0, x))
    return x * x * x


def ease_out(x):
    x = max(0.0, min(1.0, x))
    return 1 - (1 - x) ** 3


def path(keys, t):
    """keys: [(t, Vector, ease)] -> position at t (looping keys must repeat the first point at the end)."""
    for k in range(len(keys) - 1):
        t0, p0, _ = keys[k]
        t1, p1, e = keys[k + 1]
        if t0 <= t <= t1:
            x = (t - t0) / max(1e-6, t1 - t0)
            x = {'in': ease_in, 'out': ease_out, 'linear': lambda v: v}.get(e, smooth)(x)
            return p0.lerp(p1, x)
    return keys[-1][1].copy()


def world_axis_rot(pb, axis_world, deg):
    """Basis rotation turning bone pb about a world axis (rest orientation) by deg."""
    rest = (ARM.matrix_world @ pb.bone.matrix_local).to_3x3().normalized()
    axis_local = (rest.inverted() @ axis_world).normalized()
    return Quaternion(axis_local, math.radians(deg))


def world_offset_loc(pb, off_world):
    rest = (ARM.matrix_world @ pb.bone.matrix_local).to_3x3().normalized()
    return rest.inverted() @ off_world


def find_pole_angle(fore, target, pole):
    """Pick the IK pole angle that keeps the arm closest to its rest pose when the target sits on the rest wrist."""
    con = fore.constraints['IK']
    best, best_err = 0.0, 9e9
    for deg in range(-180, 181, 15):
        con.pole_angle = math.radians(deg)
        bpy.context.view_layer.update()
        err = 0.0
        for n in (fore.parent.name, fore.name):
            pb = ARM.pose.bones[n]
            err += (pb.matrix.to_quaternion().rotation_difference(
                (pb.bone.matrix_local).to_quaternion())).angle
        if err < best_err:
            best, best_err = deg, err
    con.pole_angle = math.radians(best)
    return best


def rest_hand_axes(side):
    """Rest-pose hand frame in world space: n (palm side), d (along the fingers), g (handle axis through the fist)."""
    h0, h1 = bone_world(f'{side}Hand'), bone_world(f'{side}Hand', True)
    d = (h1 - h0).normalized()
    g = (FORWARD - d * FORWARD.dot(d)).normalized()
    return d.cross(g), d, g


def aimed_hand_local(side, g_des):
    """LOCAL basis for the hand bone that turns its handle axis to g_des (palm side kept as close to rest as possible)."""
    hand = ARM.pose.bones[f'{side}Hand']
    n0, d0, g0 = rest_hand_axes(side)
    g_des = g_des.normalized()
    n_des = (n0 - g_des * n0.dot(g_des)).normalized()
    d_des = g_des.cross(n_des)
    q = Matrix((n_des, d_des, g_des)).transposed() @ Matrix((n0, d0, g0)).transposed().inverted()
    r0 = (ARM.matrix_world @ hand.bone.matrix_local).to_3x3()
    head = ARM.matrix_world @ hand.matrix.to_translation()
    m_pose = ARM.matrix_world.inverted() @ (Matrix.Translation(head) @ (q @ r0).to_4x4())
    return ARM.convert_space(pose_bone=hand, matrix=m_pose, from_space='POSE', to_space='LOCAL')


def scalar_path(keys, t):
    for k in range(len(keys) - 1):
        t0, v0 = keys[k]
        t1, v1 = keys[k + 1]
        if t0 <= t <= t1:
            return v0 + (v1 - v0) * smooth((t - t0) / max(1e-6, t1 - t0))
    return keys[-1][1]


def author_ik(name, seconds, base_clip, base_frame, hands, extras, poles_out=0.3, aims=None):
    """hands: {'Right': f(t)->world pos, 'Left': ...}; aims: {'Right': f(t)->world handle axis};
    extras: f(t)-> list of (bone, ('rot', axis, deg) | ('loc', vec))."""
    aims = aims or {}
    rest_pose()
    base = SRC.get(base_clip) or bpy.data.actions.get(base_clip)
    set_action(base)
    scene.frame_set(int(base_frame))
    base_pose = {pb.name: pb.matrix_basis.copy() for pb in ARM.pose.bones}
    set_action(None)
    empties = {}
    for side in hands:
        fore = ARM.pose.bones[f'{side}ForeArm']
        tgt = bpy.data.objects.new(f'ik_{side}', None); scene.collection.objects.link(tgt)
        pole = bpy.data.objects.new(f'pole_{side}', None); scene.collection.objects.link(pole)
        sh = bone_world(f'{side}Arm')
        outward = RIGHT if side == 'Right' else -RIGHT
        pole.location = sh + outward * poles_out - FORWARD * 0.25 - UP * 0.35
        tgt.location = bone_world(f'{side}Hand')
        con = fore.constraints.new('IK')
        con.name = 'IK'
        con.target = tgt
        con.pole_target = pole
        con.chain_count = 2
        con.use_tail = True
        empties[side] = (tgt, pole)
    for pb in ARM.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    for side in hands:
        log('pole', side, find_pole_angle(ARM.pose.bones[f'{side}ForeArm'], *empties[side]))
    nfr = int(round(seconds * FPS))
    frames = []
    arm_bones = [f'{s}{b}' for s in hands for b in ('Arm', 'ForeArm')]
    for f in range(nfr + 1):
        t = f / FPS
        for pb in ARM.pose.bones:
            pb.rotation_mode = 'QUATERNION'
            pb.matrix_basis = base_pose.get(pb.name, Matrix.Identity(4)).copy()
        for bone, op in extras(t):
            pb = ARM.pose.bones[bone]
            if op[0] == 'rot':
                pb.rotation_quaternion = pb.rotation_quaternion @ world_axis_rot(pb, op[1], op[2])
            else:
                pb.location = pb.location + world_offset_loc(pb, op[1])
        for side, fn in hands.items():
            empties[side][0].location = fn(t)
        bpy.context.view_layer.update()
        pose = {}
        for pb in ARM.pose.bones:
            if pb.name in arm_bones:
                m = ARM.convert_space(pose_bone=pb, matrix=pb.matrix, from_space='POSE', to_space='LOCAL')
            else:
                m = pb.matrix_basis.copy()
            pose[pb.name] = (m.to_translation(), m.to_quaternion())
        for side, aim in aims.items():
            m = aimed_hand_local(side, aim(t))
            pose[f'{side}Hand'] = (m.to_translation(), m.to_quaternion())
        frames.append((f, pose))
    for side in hands:
        fore = ARM.pose.bones[f'{side}ForeArm']
        fore.constraints.remove(fore.constraints['IK'])
        for e in empties[side]:
            bpy.data.objects.remove(e, do_unlink=True)
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    set_action(act)
    for f, pose in frames:
        for bn, (loc, q) in pose.items():
            pb = ARM.pose.bones[bn]
            pb.rotation_mode = 'QUATERNION'
            pb.location = loc
            pb.rotation_quaternion = q
            pb.keyframe_insert('location', frame=f)
            pb.keyframe_insert('rotation_quaternion', frame=f)
    set_action(None)
    MADE.append(name)
    log('authored', name, seconds, 's', nfr + 1, 'frames')


rest_pose()
SR, SL = bone_world('RightArm'), bone_world('LeftArm')
SM = (SR + SL) / 2
ARM_LEN = (bone_world('RightArm') - bone_world('RightHand')).length
LEFT = -RIGHT
breath = lambda t, per: math.sin(2 * math.pi * t / per)

# forge: right hand hammers a workpiece on the anvil twice per loop, left hand holds the tongs
W = SM + RIGHT * 0.05 * ARM_LEN + FORWARD * 0.62 * ARM_LEN - UP * 0.80 * ARM_LEN
TOP = SR + FORWARD * 0.28 * ARM_LEN + UP * 0.22 * ARM_LEN + RIGHT * 0.05 * ARM_LEN
strike = [(0.0, W + UP * 0.03, 'smooth'), (0.42, TOP, 'out'), (0.58, TOP + UP * 0.03 - FORWARD * 0.03, 'smooth'),
          (0.70, W, 'in'), (0.78, W + UP * 0.045, 'out'), (1.0, W + UP * 0.03, 'smooth')]
TONGS = W + LEFT * 0.30 * ARM_LEN + UP * 0.02


def forge_extras(t):
    c = t % 1.0
    hit = math.exp(-((c - 0.72) / 0.06) ** 2)
    return [('Spine02', ('rot', Vector((1, 0, 0)), 9 + 3 * hit)),
            ('Spine01', ('rot', Vector((1, 0, 0)), 2 * hit)), ('Hips', ('loc', -UP * 0.012 * hit)),
            ('neck', ('rot', Vector((1, 0, 0)), 10))]


# kitchen: right hand stirs circles in the pot, left hand steadies the rim
POT = SM + FORWARD * 0.62 * ARM_LEN - UP * 0.86 * ARM_LEN
STIR = POT + RIGHT * 0.06 * ARM_LEN


def stir(t):
    a = 2 * math.pi * t / 1.2
    return STIR + RIGHT * math.cos(a) * 0.13 * ARM_LEN + FORWARD * math.sin(a) * 0.11 * ARM_LEN + UP * 0.03 * math.sin(2 * a)


RIM = POT + LEFT * 0.30 * ARM_LEN + UP * 0.03


def kitchen_extras(t):
    a = 2 * math.pi * t / 1.2
    return [('Spine02', ('rot', Vector((1, 0, 0)), 7)), ('Spine01', ('rot', UP, 3 * math.cos(a))),
            ('neck', ('rot', Vector((1, 0, 0)), 14)), ('Hips', ('loc', RIGHT * 0.006 * math.cos(a)))]


# well: hand-over-hand rope pull; each hand pulls down then reaches back up while the other pulls
ROPE = SM + FORWARD * SPEC.get('rope_forward', 0.66) * ARM_LEN
R_TOP, R_BOT = UP * 0.55 * ARM_LEN, -UP * 0.35 * ARM_LEN


def rope_hand(side, phase):
    sgn = 1 if side == 'Right' else -1
    def fn(t):
        c = ((t / 2.0) + phase) % 1.0
        if c < 0.5:
            z = R_TOP.lerp(R_BOT, smooth(c / 0.5))
            out = 0.0
        else:
            x = (c - 0.5) / 0.5
            z = R_BOT.lerp(R_TOP, smooth(x))
            out = math.sin(math.pi * x)
        return ROPE + z + RIGHT * sgn * (0.06 * ARM_LEN + 0.16 * ARM_LEN * out) - FORWARD * 0.10 * ARM_LEN * out
    return fn


def well_extras(t):
    c = (t / 2.0) % 0.5 / 0.5
    pull = math.sin(math.pi * c)
    return [('Spine02', ('rot', Vector((1, 0, 0)), -4 * pull + 2)), ('Hips', ('loc', -UP * 0.02 * pull)),
            ('neck', ('rot', Vector((1, 0, 0)), -6))]


HAMMER_ANGLE = [(0.0, -10), (0.42, 95), (0.58, 110), (0.70, -35), (0.78, -18), (1.0, -10)]  # handle pitch, deg above forward


def hammer_aim(t):
    a = math.radians(scalar_path(HAMMER_ANGLE, t % 1.0))
    return FORWARD * math.cos(a) + UP * math.sin(a)


def ladle_aim(t):
    a = 2 * math.pi * t / 1.2
    return -UP * 0.9 + FORWARD * 0.25 + RIGHT * 0.12 * math.cos(a)


base_idle = 'AH_idle' if bpy.data.actions.get('AH_idle') else 'Idle_02'
author_ik('AH_task_forge', 2.0, base_idle, 0, {'Right': lambda t: path(strike, t % 1.0), 'Left': lambda t: TONGS + UP * 0.01 * breath(t, 1.0)},
          forge_extras, aims={'Right': hammer_aim, 'Left': lambda t: FORWARD * 0.6 - UP * 0.8})
author_ik('AH_task_kitchen', 2.4, base_idle, 0, {'Right': stir, 'Left': lambda t: RIM + UP * 0.006 * breath(t, 1.2)},
          kitchen_extras, aims={'Right': ladle_aim, 'Left': lambda t: FORWARD})
author_ik('AH_task_well', 2.0, base_idle, 0, {'Right': rope_hand('Right', 0.0), 'Left': rope_hand('Left', 0.5)},
          well_extras, aims={'Right': lambda t: UP, 'Left': lambda t: UP})


# ---------------------------------------------------------------- props (held tools + station pieces)
def flat_mat(name, rgb):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    if not m.node_tree:
        m.use_nodes = True
    b = m.node_tree.nodes.get('Principled BSDF') or m.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
    b.inputs['Base Color'].default_value = (*rgb, 1)
    b.inputs['Roughness'].default_value = 1.0
    m.diffuse_color = (*rgb, 1)   # viewport / Workbench preview colour
    return m


WOOD, IRON, STEEL, COPPER, ROPEC, CLAY = (flat_mat('prop_wood', (0.42, 0.25, 0.12)), flat_mat('prop_iron', (0.18, 0.18, 0.2)),
                                          flat_mat('prop_steel', (0.62, 0.64, 0.68)), flat_mat('prop_copper', (0.72, 0.38, 0.18)),
                                          flat_mat('prop_rope', (0.78, 0.66, 0.42)), flat_mat('prop_clay', (0.55, 0.3, 0.22)))


def prim(kind, mat, **kw):
    before = set(bpy.data.objects)
    getattr(bpy.ops.mesh, f'primitive_{kind}_add')(**kw)
    o = next(o for o in bpy.data.objects if o not in before)
    o.data.materials.append(mat)
    return o


def join(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return o


def hand_frame(side):
    """Grip point and axes at the rest pose: d = along the fingers, g = handle axis (forward), n = completes."""
    h0, h1 = bone_world(f'{side}Hand'), bone_world(f'{side}Hand', True)
    d = (h1 - h0).normalized()
    g = (FORWARD - d * FORWARD.dot(d)).normalized()
    n = d.cross(g)
    grip = h0.lerp(bone_world(f'{side}Hand_End', True), 0.45)
    return grip, d, g, n


def place_tool(obj, side, grip_at=0.25, roll=0.0, length=None):
    """Tool modelled along +Z with its handle butt at z=0: put the handle through the fist, head ahead (forward)."""
    grip, d, g, n = hand_frame(side)
    zs = [v.co.z for v in obj.data.vertices]
    L = max(zs) - min(zs)
    obj.data.transform(Matrix.Translation(Vector((0, 0, -min(zs) - grip_at * L))))
    basis = Matrix((n, d, g)).transposed()   # local X->n, Y->d, Z->g (handle forward)
    rot = basis.to_4x4() @ Matrix.Rotation(math.radians(roll), 4, 'Z')
    obj.matrix_world = Matrix.Translation(grip) @ rot
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def skin_rigid(obj, bone):
    obj.vertex_groups.clear()
    vg = obj.vertex_groups.new(name=bone)
    vg.add(list(range(len(obj.data.vertices))), 1.0, 'REPLACE')
    mod = obj.modifiers.new('Armature', 'ARMATURE')
    mod.object = ARM
    obj.parent = ARM
    obj.matrix_parent_inverse = ARM.matrix_world.inverted()


S = HEIGHT / 1.5  # props scale with the character
PROPS = []


def tool(name, parts, side='Right', grip_at=0.2, roll=0.0):
    o = join(parts, name)
    place_tool(o, side, grip_at, roll)
    skin_rigid(o, f'{side}Hand')
    PROPS.append(name)


rest_pose()
tool('Prop_Hammer', [prim('cylinder', WOOD, radius=0.011 * S, depth=0.22 * S, location=(0, 0, 0.11 * S), vertices=8),
                     prim('cube', IRON, size=1, location=(0, 0, 0.22 * S), scale=(0.035 * S, 0.085 * S, 0.035 * S))])
tool('Prop_Pickaxe', [prim('cylinder', WOOD, radius=0.011 * S, depth=0.30 * S, location=(0, 0, 0.15 * S), vertices=8),
                      prim('cone', IRON, radius1=0.016 * S, depth=0.13 * S, location=(0, 0.065 * S, 0.30 * S), rotation=(math.radians(-90), 0, 0), vertices=6),
                      prim('cone', IRON, radius1=0.016 * S, depth=0.13 * S, location=(0, -0.065 * S, 0.30 * S), rotation=(math.radians(90), 0, 0), vertices=6)])
tool('Prop_Axe', [prim('cylinder', WOOD, radius=0.012 * S, depth=0.34 * S, location=(0, 0, 0.17 * S), vertices=8),
                  prim('cube', STEEL, size=1, location=(0, 0.045 * S, 0.30 * S), scale=(0.012 * S, 0.075 * S, 0.07 * S))])
tool('Prop_Ladle', [prim('cylinder', WOOD, radius=0.007 * S, depth=0.26 * S, location=(0, 0, 0.13 * S), vertices=8),
                    prim('uv_sphere', COPPER, radius=0.035 * S, location=(0, 0, 0.27 * S), segments=12, ring_count=6)], grip_at=0.15)
tool('Prop_Mug', [prim('cylinder', WOOD, radius=0.032 * S, depth=0.075 * S, location=(0, 0, 0.0375 * S), vertices=12),
                  prim('torus', IRON, major_radius=0.026 * S, minor_radius=0.006 * S, location=(0.034 * S, 0, 0.04 * S), rotation=(math.radians(90), 0, 0), major_segments=10, minor_segments=5)], grip_at=0.5)


def station(name, parts):
    o = join(parts, name)
    o.parent = ARM
    o.matrix_parent_inverse = ARM.matrix_world.inverted()
    PROPS.append(name)


FIRE = flat_mat('prop_fire', (1.0, 0.45, 0.08))
STONE = flat_mat('prop_stone', (0.45, 0.43, 0.42))
anvil_top = W.z - 0.035 * S
top_h, base_h = 0.065 * S, 0.06 * S
waist_h = max(0.02, anvil_top - top_h - base_h)
station('Set_Anvil', [
    prim('cube', IRON, size=1, location=(W.x, W.y, anvil_top - top_h / 2), scale=(0.26 * S, 0.11 * S, top_h)),
    prim('cone', IRON, radius1=0.045 * S, depth=0.12 * S, location=(W.x - 0.19 * S, W.y, anvil_top - 0.03 * S), rotation=(0, math.radians(-90), 0), vertices=8),
    prim('cube', IRON, size=1, location=(W.x, W.y, base_h + waist_h / 2), scale=(0.09 * S, 0.07 * S, waist_h)),
    prim('cube', STONE, size=1, location=(W.x, W.y, base_h / 2), scale=(0.2 * S, 0.15 * S, base_h)),
    prim('cube', COPPER, size=1, location=(W.x + 0.02 * S, W.y, anvil_top + 0.01 * S), scale=(0.09 * S, 0.03 * S, 0.02 * S))])  # glowing bar
pot_top = POT.z - 0.03 * S
pot_h = 0.13 * S
leg_h = max(0.02, pot_top - pot_h)
legs = [prim('cylinder', IRON, radius=0.008 * S, depth=leg_h, vertices=6,
             location=(POT.x + 0.1 * S * math.cos(a), POT.y + 0.1 * S * math.sin(a), leg_h / 2))
        for a in (0.3, 0.3 + 2.094, 0.3 + 4.189)]
station('Set_Pot', [prim('cylinder', IRON, radius=0.13 * S, depth=pot_h, location=(POT.x, POT.y, pot_top - pot_h / 2), vertices=16),
                    prim('torus', IRON, major_radius=0.13 * S, minor_radius=0.012 * S, location=(POT.x, POT.y, pot_top), major_segments=16, minor_segments=5),
                    prim('cylinder', COPPER, radius=0.115 * S, depth=0.01 * S, location=(POT.x, POT.y, pot_top - 0.012 * S), vertices=16),  # stew
                    prim('cone', FIRE, radius1=0.07 * S, depth=0.12 * S, location=(POT.x, POT.y, 0.06 * S), vertices=8)] + legs)
rope_top = SM.z + 0.9 * ARM_LEN
station('Set_Rope', [prim('cylinder', ROPEC, radius=0.008 * S, depth=rope_top, location=(ROPE.x, ROPE.y, rope_top / 2), vertices=6),
                     prim('cylinder', WOOD, radius=0.07 * S, depth=0.09 * S, location=(ROPE.x, ROPE.y, 0.045 * S), vertices=10),
                     prim('torus', WOOD, major_radius=0.06 * S, minor_radius=0.012 * S, location=(ROPE.x, ROPE.y, rope_top), rotation=(0, math.radians(90), 0), major_segments=12, minor_segments=5)])
def posed_body_points(clip, frac):
    """World-space vertices of the deformed body at a point in a clip (for fitting furniture under it)."""
    act = bpy.data.actions.get(clip)
    set_action(act)
    f0, f1 = act.frame_range
    scene.frame_set(int(f0 + (f1 - f0) * frac))
    deps = bpy.context.evaluated_depsgraph_get()
    ev = BODY.evaluated_get(deps)
    me = ev.to_mesh()
    pts = [ev.matrix_world @ v.co for v in me.vertices]
    ev.to_mesh_clear()
    hips = ARM.matrix_world @ ARM.pose.bones['Hips'].head
    head = ARM.matrix_world @ ARM.pose.bones['Head'].head
    set_action(None)
    return pts, hips, head


# chair under the seated pose: seat top at the lowest point of the body around the hips
pts, hips, _ = posed_body_points('AH_sit', 0.3)
near = [p for p in pts if (Vector((p.x, p.y)) - Vector((hips.x, hips.y))).length < 0.16 * S]
seat = (min(p.z for p in near) if near else hips.z - 0.1 * S) - 0.005
seat_w, seat_d, seat_t = 0.34 * S, 0.3 * S, 0.04 * S
cx, cy = hips.x, hips.y + 0.03 * S
chair = [prim('cube', WOOD, size=1, location=(cx, cy, seat - seat_t / 2), scale=(seat_w, seat_d, seat_t)),
         prim('cube', WOOD, size=1, location=(cx, cy + seat_d / 2 + 0.015 * S, seat + 0.22 * S), scale=(seat_w, 0.03 * S, 0.44 * S))]
for dx in (-1, 1):
    for dy in (-1, 1):
        lh = max(0.02, seat - seat_t)
        chair.append(prim('cube', WOOD, size=1, location=(cx + dx * (seat_w / 2 - 0.02 * S), cy + dy * (seat_d / 2 - 0.02 * S), lh / 2), scale=(0.035 * S, 0.035 * S, lh)))
station('Set_Chair', chair)
# futon + pillow under the sleeping pose
pts, hips, head = posed_body_points('AH_sleep', 0.5)
xs, ys = [p.x for p in pts], [p.y for p in pts]
CLOTH = flat_mat('prop_cloth', (0.36, 0.42, 0.62))
PILLOW = flat_mat('prop_pillow', (0.92, 0.9, 0.84))
station('Set_Bed', [prim('cube', CLOTH, size=1, location=((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, 0.005 * S),
                         scale=(max(xs) - min(xs) + 0.16 * S, max(ys) - min(ys) + 0.16 * S, 0.03 * S)),
                    prim('cube', PILLOW, size=1, location=(head.x, head.y, 0.03 * S), scale=(0.22 * S, 0.14 * S, 0.05 * S))])
CLIP_PROPS['AH_sit'] = ['Set_Chair']
CLIP_PROPS['AH_sleep'] = ['Set_Bed']
log('props', PROPS)


# ---------------------------------------------------------------- preview (Workbench, textured, a few frames per clip)
def preview(clips, outdir, frames=4):
    outdir.mkdir(parents=True, exist_ok=True)
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'FLAT'
    scene.display.shading.color_type = 'TEXTURE'
    scene.render.resolution_x = scene.render.resolution_y = 384
    scene.render.film_transparent = False
    scene.world = scene.world or bpy.data.worlds.new('w')
    cam_data = bpy.data.cameras.new('cam'); cam_data.type = 'ORTHO'; cam_data.ortho_scale = HEIGHT * 1.15
    cam = bpy.data.objects.new('cam', cam_data); scene.collection.objects.link(cam)
    yaw = math.radians(25)
    cam.location = Vector((math.sin(yaw) * -4, -math.cos(yaw) * 4, HEIGHT * 0.5))
    cam.rotation_euler = (math.radians(90), 0, -yaw)
    scene.camera = cam
    for clip in clips:
        act = bpy.data.actions.get(clip)
        if act is None:
            continue
        set_action(act)
        f0, f1 = act.frame_range
        props = set(CLIP_PROPS.get(clip, []))
        for p in PROPS:
            bpy.data.objects[p].hide_render = p not in props
        for k in range(frames):
            scene.frame_set(int(f0 + (f1 - f0) * k / frames))
            scene.render.filepath = str(outdir / f'{clip}_{k}.png')
            bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam, do_unlink=True)
    for p in PROPS:
        bpy.data.objects[p].hide_render = False
    log('preview', outdir)


# ---------------------------------------------------------------- save / export
for a in list(bpy.data.actions):
    if not a.name.startswith('AH_'):
        bpy.data.actions.remove(a)
set_action(bpy.data.actions.get('AH_idle'))
log('clips', sorted(a.name for a in bpy.data.actions))
JOB.mkdir(parents=True, exist_ok=True)
if PREVIEW:
    preview(SPEC.get('preview_clips', ['AH_idle', 'AH_task_forge', 'AH_task_kitchen', 'AH_task_well', 'AH_task_quarry', 'AH_task_lumber']), JOB / 'preview')
bpy.ops.wm.save_as_mainfile(filepath=str(JOB / f'{UID}-tower.blend'))
if EXPORT:
    OUT.mkdir(parents=True, exist_ok=True)
    for f in list(OUT.glob('texture_*')):
        if f.suffix in ('.png', '.jpg'):
            f.unlink()
    for i, img in enumerate(bpy.data.images):
        if img.type != 'IMAGE' or not img.has_data and not img.filepath:
            continue
        path = OUT / f'texture_{i}_{bpy.path.clean_name(img.name)}.png'
        src = pathlib.Path(bpy.path.abspath(img.filepath)) if img.filepath else None
        if src and src.exists():   # file-backed (the cel albedo): copy; its pixels may never have been loaded
            path.write_bytes(src.read_bytes())
        else:
            img.filepath_raw = str(path)
            img.file_format = 'PNG'
            img.save()
        img.filepath = str(path)
    bpy.ops.export_scene.fbx(filepath=str(OUT / 'model.fbx'), object_types={'ARMATURE', 'MESH', 'EMPTY'}, add_leaf_bones=False,
                             bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
                             bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, path_mode='RELATIVE',
                             embed_textures=False, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_ALL')
    (OUT / 'provenance.json').write_text(json.dumps({
        'model': str(JOB / 'meshy_rigged_anim.glb'), 'spec': str(spec_path), 'pipeline': 'meshy-remesh30k+autorig+presets+build_tower_rig',
        'clips': sorted(a.name for a in bpy.data.actions), 'clip_props': CLIP_PROPS, 'props': PROPS,
        'tail_bones': TAIL, 'braids': BRAIDS, 'jiggle': JIGGLE, 'jiggle_strength': SPEC.get('jiggle', {}).get('strength', 1.0),
        'height': round(HEIGHT, 4), 'fps': FPS}, indent=2))
    log('exported', OUT / 'model.fbx')
