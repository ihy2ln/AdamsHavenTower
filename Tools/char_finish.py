"""Blender (headless): raw Pixal3D character GLB -> watertight, <=50k triangle, print/merch-quality game model.

  blender -b -P Tools/char_finish.py -- <raw.glb> <front_ref.png> <back_view.png|-> <outdir> <Name> [target_tris]

Passes (each prints QA numbers so problems are visible):
  1  normalise (1.8 m tall, feet at z=0, centred) and clean the raw mesh (doubles, loose bits, normals)
  2  watertight base: voxel remesh at ~1.3 mm, drop specks, then close any remaining holes and re-verify
  3  decimate to the triangle budget, re-verify watertight after every step, fix and repeat until clean
  4  smart-UV unwrap, bake colour + normal map from the raw high-poly (4096 px)
  5  project the reference painting from the front (and the generated back view) over the bake, matching the PNG
  6  export GLB / FBX / STL + QA renders + qa.json
"""
import bpy, bmesh, sys, math, json, os
import numpy as np
from pathlib import Path
from mathutils import Vector

args = sys.argv[sys.argv.index('--') + 1:]
raw, front_png, back_png, outdir, name = args[:5]
target = int(args[5]) if len(args) > 5 else 50000
outdir = Path(outdir); outdir.mkdir(parents=True, exist_ok=True)
H = 1.8
import os
ATLAS = int(os.environ.get('CHAR_ATLAS', '4096'))
qa = {'name': name, 'target_tris': target}


def getv(me):
    a = np.empty(len(me.vertices) * 3, np.float32); me.vertices.foreach_get('co', a); return a.reshape(-1, 3)


def setv(me, arr):
    me.vertices.foreach_set('co', np.ascontiguousarray(arr, np.float32).ravel()); me.update()


def stats(obj, label):
    if len(obj.data.polygons) > 1500000:                 # python loops over millions of faces take hours
        print(f'QA[{label}] skipped ({len(obj.data.polygons)} faces)', flush=True); return True
    bm = bmesh.new(); bm.from_mesh(obj.data)
    boundary = sum(1 for e in bm.edges if len(e.link_faces) == 1)
    nonman = sum(1 for e in bm.edges if len(e.link_faces) > 2)
    loose = sum(1 for v in bm.verts if not v.link_edges)
    tris = sum(len(f.verts) - 2 for f in bm.faces)
    # connected components
    seen, comps = set(), 0
    for f in bm.faces:
        if f.index in seen: continue
        comps += 1; stack = [f]; seen.add(f.index)
        while stack:
            cur = stack.pop()
            for e in cur.edges:
                for nf in e.link_faces:
                    if nf.index not in seen: seen.add(nf.index); stack.append(nf)
    zz = [v.co.z for v in bm.verts]
    low = sum(1 for f in bm.faces if f.calc_center_median().z < 0.9)
    print('  faces below 0.9m:', low, flush=True)
    bm.free()
    print(f'QA[{label}] z=[{min(zz):.2f},{max(zz):.2f}]'.replace('QA[' + label + '] z', 'QA[' + label + '] z') + f' tris={tris} boundary_edges={boundary} nonmanifold_edges={nonman} loose_verts={loose} components={comps}',
          flush=True)
    qa[label] = dict(tris=tris, boundary=boundary, nonmanifold=nonman, loose=loose, components=comps)
    return boundary + nonman + loose == 0


def select_only(obj):
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def repair(obj, drop_small=0):
    bm = bmesh.new(); bm.from_mesh(obj.data)
    zmin = lambda: min((v.co.z for v in bm.verts), default=0)
    z0 = zmin()
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    z1 = zmin()
    bmesh.ops.dissolve_degenerate(bm, edges=bm.edges, dist=1e-7)
    z2 = zmin()
    bm.edges.ensure_lookup_table()
    if drop_small:                                        # remove floating specks
        seen, comps = set(), []
        for f in bm.faces:
            if f in seen: continue
            stack, comp = [f], [f]; seen.add(f)
            while stack:
                cur = stack.pop()
                for e in cur.edges:
                    for nf in e.link_faces:
                        if nf not in seen: seen.add(nf); stack.append(nf); comp.append(nf)
            comps.append(comp)
        big = max(len(c) for c in comps)
        print('COMPS', [(len(c), round(min(v.co.z for f in c for v in f.verts), 2), round(max(v.co.z for f in c for v in f.verts), 2)) for c in sorted(comps, key=len, reverse=True)[:10]], flush=True)
        limit = max(drop_small, big * 0.0005)
        kill = [f for c in comps if len(c) < limit for f in c]
        if kill: bmesh.ops.delete(bm, geom=kill, context='FACES')
    z3 = zmin()
    for _ in range(4):
        boundary = [e for e in bm.edges if len(e.link_faces) == 1]
        if not boundary: break
        bmesh.ops.holes_fill(bm, edges=boundary, sides=64)
        quads = [f for f in bm.faces if len(f.verts) > 3]
        if quads: bmesh.ops.triangulate(bm, faces=quads)
    dead = [v for v in bm.verts if not v.link_edges]
    if dead: bmesh.ops.delete(bm, geom=dead, context='VERTS')      # never call delete() with an empty list: it acts on the selection
    z4 = zmin()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    print(f'REPAIR zmin {z0:.2f} doubles {z1:.2f} degenerate {z2:.2f} specks {z3:.2f} holes {z4:.2f}', flush=True)
    bm.to_mesh(obj.data); bm.free(); obj.data.update()


# ---------------------------------------------------------------- pass 1: import + normalise
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=raw)
hi = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
hi.name = f'{name}_high'
select_only(hi)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
V0 = getv(hi.data)
zs, xs, ys = V0[:, 2], V0[:, 0], V0[:, 1]
k = H / (zs.max() - zs.min())
cx, cy = (xs.max() + xs.min()) / 2, (ys.max() + ys.min()) / 2
turn = -1 if '_mv' in Path(raw).name else 1          # the multi-view model faces +Y; the single-view one faces -Y (the camera)
zmin0 = float(zs.min())
V1 = np.stack([(V0[:, 0] - cx) * k * turn, (V0[:, 1] - cy) * k * turn, (V0[:, 2] - zmin0) * k], 1)
setv(hi.data, V1)
# Orientation check: does the mesh's front silhouette match the reference, or its mirror image (asymmetric props decide)?
def silhouette_iou(mirror):
    ref = bpy.data.images.load(front_png); rw, rh = ref.size
    ra = np.empty(rw * rh * 4, np.float32); ref.pixels.foreach_get(ra)
    ra = ra.reshape(rh, rw, 4)[::-1, :, 3] > 0.1
    ys_, xs_ = np.where(ra); ra = ra[ys_.min():ys_.max() + 1, xs_.min():xs_.max() + 1]
    GW, GH = 200, 300
    ref_small = np.array(ra.astype(np.uint8).repeat(1, 0))[np.linspace(0, ra.shape[0] - 1, GH).astype(int)][:, np.linspace(0, ra.shape[1] - 1, GW).astype(int)] > 0
    px = V1[:, [0, 2]]
    x0_, x1_ = px[:, 0].min(), px[:, 0].max()
    u = (px[:, 0] - x0_) / (x1_ - x0_); u = 1 - u if mirror else u
    grid = np.zeros((GH, GW), bool)
    gx = np.clip((u * (GW - 1)).astype(int), 0, GW - 1); gy = np.clip(((1 - px[:, 1] / H) * (GH - 1)).astype(int), 0, GH - 1)
    grid[gy, gx] = True
    for _ in range(3):
        grid = grid | np.roll(grid, 1, 0) | np.roll(grid, -1, 0) | np.roll(grid, 1, 1) | np.roll(grid, -1, 1)
    iou = (grid & ref_small).sum() / max(1, (grid | ref_small).sum())
    cm = float(np.where(grid)[1].mean() / GW); cr = float(np.where(ref_small)[1].mean() / GW)     # horizontal centre of mass
    return iou, abs(cm - cr)
(iou_plain, cd_plain), (iou_mirror, cd_mirror) = silhouette_iou(False), silhouette_iou(True)
score = (iou_mirror - iou_plain) + 2.0 * (cd_plain - cd_mirror)      # >0 favours mirroring
mirror_x = score > 0.02
print(f'ORIENT centroid gap plain {cd_plain:.3f} mirrored {cd_mirror:.3f} score {score:.3f}', flush=True)
print(f'ORIENT iou plain {iou_plain:.3f} mirrored {iou_mirror:.3f} -> mirror_x={mirror_x}', flush=True)
qa['orientation'] = dict(iou_plain=float(iou_plain), iou_mirror=float(iou_mirror), mirrored=bool(mirror_x))
if mirror_x:
    V1 = V1 * np.array([-1, 1, 1], np.float32); setv(hi.data, V1)
    bpy.ops.object.mode_set(mode='OBJECT'); select_only(hi)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.flip_normals(); bpy.ops.object.mode_set(mode='OBJECT')
NORM = (cx, cy, zmin0, k, turn, -1 if mirror_x else 1)
XMIN, XMAX = float(V1[:, 0].min()), float(V1[:, 0].max())
YMIN, YMAX = float(V1[:, 1].min()), float(V1[:, 1].max())
stats(hi, 'pass1_raw')
base_src = hi.data.copy()          # the closing pass starts from the untouched mesh; hole filling here confuses it
if len(hi.data.polygons) < 800000: repair(hi)           # the raw mesh is only a bake source; skip the slow cleanup when huge
stats(hi, 'pass1_clean')

# ---------------------------------------------------------------- pass 2: watertight base
base = hi.copy(); base.data = base_src; base.name = f'{name}_base'
bpy.context.scene.collection.objects.link(base)
base.data.materials.clear()
select_only(base)
# The raw mesh is made of open, overlapping shells (cloth, stockings, hair cards) with no inside, so an SDF remesh drops
# them. Mesh -> Volume -> Mesh (OpenVDB) thickens every surface into a closed shell and merges the overlaps.
closed_path = os.environ.get('CHAR_CLOSED')
if closed_path and Path(closed_path).exists():
    # watertight surface made by char_close.py (voxel closing outside Blender): bring it into the same normalised frame
    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=closed_path)
    cl = [o for o in bpy.context.scene.objects if o not in before and o.type == 'MESH'][0]
    select_only(cl); bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    cx_, cy_, zmin_, k_, turn_, mir_ = NORM
    C0 = getv(cl.data)
    setv(cl.data, np.stack([(C0[:, 0] - cx_) * k_ * turn_ * mir_, (C0[:, 1] - cy_) * k_ * turn_, (C0[:, 2] - zmin_) * k_], 1))
    if len(cl.data.polygons) > 700000:                   # native decimation first: everything after this runs in Python
        dm0 = cl.modifiers.new('pre', 'DECIMATE'); dm0.ratio = 700000 / len(cl.data.polygons); dm0.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=dm0.name)
    if mir_ < 0:
        select_only(cl); bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.flip_normals(); bpy.ops.object.mode_set(mode='OBJECT')
    base.data = cl.data.copy(); bpy.data.objects.remove(cl, do_unlink=True)
    stats(base, 'pass2_closed_input')
    use_gn = False
else:
    use_gn = True
voxel = H / 700        # finer grids make Blender's volume->mesh silently drop geometry
ng = bpy.data.node_groups.new('close_surfaces', 'GeometryNodeTree')
ng.interface.new_socket('Geometry', in_out='INPUT', socket_type='NodeSocketGeometry')
ng.interface.new_socket('Geometry', in_out='OUTPUT', socket_type='NodeSocketGeometry')
gi, go = ng.nodes.new('NodeGroupInput'), ng.nodes.new('NodeGroupOutput')
m2v = ng.nodes.new('GeometryNodeMeshToVolume'); m2v.inputs['Resolution Mode'].default_value = 'Size'
m2v.inputs['Voxel Size'].default_value = voxel
m2v.inputs['Interior Band Width'].default_value = voxel * 1.6
v2m = ng.nodes.new('GeometryNodeVolumeToMesh'); v2m.inputs['Resolution Mode'].default_value = 'Grid'; v2m.inputs['Threshold'].default_value = 0.1
ng.links.new(gi.outputs[0], m2v.inputs['Mesh']); ng.links.new(m2v.outputs['Volume'], v2m.inputs['Volume'])
ng.links.new(v2m.outputs['Mesh'], go.inputs[0])
if use_gn:
    gn = base.modifiers.new('close', 'NODES'); gn.node_group = ng
    bpy.ops.object.modifier_apply(modifier=gn.name)
repair(base, drop_small=2000)
bm = bmesh.new(); bm.from_mesh(base.data)
for _ in range(14):                                     # relax the voxel staircase so decimation and UV islands stay coherent
    bmesh.ops.smooth_vert(bm, verts=bm.verts, factor=0.55, use_axis_x=True, use_axis_y=True, use_axis_z=True)
bm.to_mesh(base.data); bm.free(); base.data.update()
stats(base, 'pass2_voxel')

# ---------------------------------------------------------------- pass 3: decimate to budget, keep watertight
tris = len(base.data.polygons)
for attempt in range(4):
    select_only(base)
    dm = base.modifiers.new('decimate', 'DECIMATE'); dm.decimate_type = 'COLLAPSE'
    dm.ratio = min(1.0, (target * (0.995 - 0.02 * attempt)) / max(1, len(base.data.polygons) * 1.0)); dm.use_collapse_triangulate = True
    print('DECIMATE ratio', dm.ratio, 'active', bpy.context.view_layer.objects.active.name, 'base verts', len(base.data.vertices), flush=True)
    bpy.ops.object.modifier_apply(modifier=dm.name)
    print('AFTER decimate faces', len(base.data.polygons), flush=True)
    repair(base, drop_small=200)
    ok = stats(base, f'pass3_decimate_{attempt}')
    n = len(base.data.polygons)
    if ok and n <= target: break
    if n > target: continue
lo = base
lo.name = f'{name}_low'
for p in lo.data.polygons: p.use_smooth = True

# ---------------------------------------------------------------- pass 4: unwrap + bake from the raw mesh
select_only(lo)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.002, area_weight=0.5, correct_aspect=True)
bpy.ops.object.mode_set(mode='OBJECT')

sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.samples = 4
sc.cycles.device = 'CPU'   # ComfyUI usually owns the GPU's memory; CPU bakes are slower but never run out of VRAM
sc.view_settings.view_transform = 'Standard'
sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGB'


def new_target_material(obj, image):
    m = bpy.data.materials.new('bake_' + image.name); m.use_nodes = True
    nt = m.node_tree; tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = image; nt.nodes.active = tn
    obj.data.materials.clear(); obj.data.materials.append(m)
    return m


def save(img, path):
    img.save_render(str(path), scene=sc)


color = bpy.data.images.new('bake_color', ATLAS, ATLAS, alpha=False)
new_target_material(lo, color)
bpy.ops.object.select_all(action='DESELECT'); hi.select_set(True); lo.select_set(True)
bpy.context.view_layer.objects.active = lo
bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, use_selected_to_active=True, cage_extrusion=0.012,
                    max_ray_distance=0.04, margin=24, margin_type='EXTEND')
save(color, outdir / f'{name}_bake_color.png')
normal = bpy.data.images.new('bake_normal', ATLAS, ATLAS, alpha=False); normal.colorspace_settings.name = 'Non-Color'
new_target_material(lo, normal)
bpy.ops.object.select_all(action='DESELECT'); hi.select_set(True); lo.select_set(True)
bpy.context.view_layer.objects.active = lo
print('normal bake ->', bpy.ops.object.bake(type='NORMAL', normal_space='TANGENT', use_selected_to_active=True, cage_extrusion=0.012, max_ray_distance=0.04, margin=24, margin_type='EXTEND'), np.array(normal.pixels[:20000]).mean(), flush=True)
normal.colorspace_settings.name = 'sRGB'
save(normal, outdir / f'{name}_normal.png')
print('BAKED colour+normal', flush=True)

# ---------------------------------------------------------------- pass 5: project the reference painting(s)
def image_alpha_bbox(path, gray=None):
    img = bpy.data.images.load(path)
    w, h = img.size
    px = np.empty(w * h * 4, np.float32); img.pixels.foreach_get(px)
    px = px.reshape(h, w, 4)[::-1]                     # row 0 = top
    if gray is None:
        a = px[:, :, 3] > 0.1
    else:                                              # flat grey background: anything clearly different is the figure
        a = np.abs(px[:, :, :3] - gray).max(2) > 0.08
    ys, xs = np.where(a)
    return img, w, h, (xs.min(), xs.max(), ys.min(), ys.max())


def projection_bake(img, w, h, bbox, mirror, facing_sign, label, axis='Y'):
    x0, x1, y0, y1 = map(float, bbox)
    m = bpy.data.materials.new('proj_' + label); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear(); N = nt.nodes.new; L = nt.links.new
    tex = N('ShaderNodeTexImage'); tex.image = img; tex.extension = 'CLIP'; tex.interpolation = 'Cubic'
    geo = N('ShaderNodeNewGeometry'); sep = N('ShaderNodeSeparateXYZ'); L(geo.outputs['Position'], sep.inputs[0])

    def M(op, a, b=None):
        n = N('ShaderNodeMath'); n.operation = op
        for i, v in enumerate((a, b)):
            if v is None: continue
            if isinstance(v, (int, float)): n.inputs[i].default_value = v
            else: L(v, n.inputs[i])
        return n.outputs[0]
    if axis == 'Y':                                                               # front / back: horizontal = X
        tx = M('DIVIDE', M('SUBTRACT', sep.outputs['X'], XMIN), XMAX - XMIN)
    else:                                                                         # side views: horizontal = depth (Y)
        tx = M('DIVIDE', M('SUBTRACT', sep.outputs['Y'], YMIN), YMAX - YMIN)
    if mirror: tx = M('SUBTRACT', 1.0, tx)
    u = M('ADD', M('MULTIPLY', tx, (x1 - x0) / w), x0 / w)
    v = M('ADD', M('MULTIPLY', M('DIVIDE', sep.outputs['Z'], H), (y1 - y0) / h), 1.0 - y1 / h)
    comb = N('ShaderNodeCombineXYZ'); L(u, comb.inputs['X']); L(v, comb.inputs['Y']); L(comb.outputs[0], tex.inputs['Vector'])
    nsep = N('ShaderNodeSeparateXYZ'); L(geo.outputs['Normal'], nsep.inputs[0])
    facing = M('MULTIPLY', nsep.outputs['X' if axis == 'X' else 'Y'], 1.0 * facing_sign)   # front (sign -1) weights -Y normals; back (+1) weights +Y
    ramp = N('ShaderNodeMapRange'); ramp.clamp = True
    ramp.inputs['From Min'].default_value = 0.35; ramp.inputs['From Max'].default_value = 0.8
    L(facing, ramp.inputs['Value'])
    weight = M('MULTIPLY', ramp.outputs['Result'], tex.outputs['Alpha'])
    emit = N('ShaderNodeEmission'); out = N('ShaderNodeOutputMaterial'); L(emit.outputs[0], out.inputs['Surface'])
    lo.data.materials.clear(); lo.data.materials.append(m)
    res = {}
    for kind, src in (('color', tex.outputs['Color']), ('weight', None)):
        for l in list(emit.inputs['Color'].links): nt.links.remove(l)
        if src is None:
            comb2 = N('ShaderNodeCombineColor')
            for i in range(3): L(weight, comb2.inputs[i])
            src = comb2.outputs[0]
        L(src, emit.inputs['Color'])
        target_img = bpy.data.images.new(f'{label}_{kind}', ATLAS, ATLAS, alpha=False)
        tn = nt.nodes.new('ShaderNodeTexImage'); tn.image = target_img; nt.nodes.active = tn
        select_only(lo)
        bpy.ops.object.bake(type='EMIT', margin=24, margin_type='EXTEND')
        res[kind] = target_img
    return res


layers = []
img_f, wf, hf, bbox_f = image_alpha_bbox(front_png)
layers.append(projection_bake(img_f, wf, hf, bbox_f, False, -1, 'front'))
if back_png != '-':
    img_b, wb, hb, bbox_b = image_alpha_bbox(back_png)             # back_png carries a keyed alpha (char_final.py)
    layers.append(projection_bake(img_b, wb, hb, bbox_b, True, +1, 'back'))
# side views: the character faces -Y, so its left side faces +X (front = image right) and its right side faces -X
for side, sign, mirror_ in (('left', +1, True), ('right', -1, False)):
    path_ = os.environ.get('CHAR_SIDE_' + side.upper())
    if path_ and Path(path_).exists():
        img_s, ws, hs, bbox_s = image_alpha_bbox(path_)
        layers.append(projection_bake(img_s, ws, hs, bbox_s, mirror_, sign, side, axis='X'))

def to_np(img):
    a = np.empty(ATLAS * ATLAS * 4, np.float32); img.pixels.foreach_get(a)
    return a.reshape(ATLAS, ATLAS, 4)[:, :, :3]


def srgb(a):                                            # bake buffers hold linear values; convert for numpy blending
    return np.where(a <= 0.0031308, a * 12.92, 1.055 * np.power(np.clip(a, 1e-6, 1), 1 / 2.4) - 0.055)


base_c = srgb(to_np(color))
final = base_c.copy()
for i, L_ in enumerate(layers):
    P, F = srgb(to_np(L_['color'])), to_np(L_['weight'])[:, :, :1]
    if i == 0:                                          # colour-correct the unpainted texels toward the painting's palette
        sel = F[..., 0] > 0.9
        if sel.sum() > 5000:
            X = np.concatenate([base_c[sel], np.ones((int(sel.sum()), 1), np.float32)], 1)
            Mx, *_ = np.linalg.lstsq(X, P[sel], rcond=None)
            flat = np.concatenate([base_c.reshape(-1, 3), np.ones((ATLAS * ATLAS, 1), np.float32)], 1)
            corr = (flat @ Mx).reshape(base_c.shape)
            lum = lambda a: (a * np.array([0.299, 0.587, 0.114], np.float32)).sum(2, keepdims=True)
            final = np.clip(corr * (lum(base_c) / (lum(corr) + 0.02)).clip(0.4, 2.5), 0, 1)
    final = F * P + (1 - F) * final
final_img = bpy.data.images.new(f'{name}_albedo', ATLAS, ATLAS, alpha=False)
rgba = np.concatenate([np.where(final <= 0.04045, final / 12.92, ((final + 0.055) / 1.055) ** 2.4), np.ones((ATLAS, ATLAS, 1), np.float32)], 2)
final_img.pixels.foreach_set(rgba.ravel()); final_img.update()
save(final_img, outdir / f'{name}_albedo.png')
print('COMPOSED albedo', flush=True)

# ---------------------------------------------------------------- pass 6: export + QA
m = bpy.data.materials.new(f'{name}_anime'); m.use_nodes = True
nt = m.node_tree; bsdf = nt.nodes['Principled BSDF']
t1 = nt.nodes.new('ShaderNodeTexImage'); t1.image = final_img; nt.links.new(t1.outputs['Color'], bsdf.inputs['Base Color'])
t2 = nt.nodes.new('ShaderNodeTexImage'); t2.image = bpy.data.images.load(str(outdir / f'{name}_normal.png')); t2.image.colorspace_settings.name = 'Non-Color'
nm = nt.nodes.new('ShaderNodeNormalMap'); nt.links.new(t2.outputs['Color'], nm.inputs['Color']); nt.links.new(nm.outputs['Normal'], bsdf.inputs['Normal'])
bsdf.inputs['Roughness'].default_value = 0.8; bsdf.inputs['Metallic'].default_value = 0.0
lo.data.materials.clear(); lo.data.materials.append(m)
final_img.pack(); t2.image.pack()
final_ok = stats(lo, 'final')
qa['final_ok_watertight'] = bool(final_ok)
qa['final_within_budget'] = len(lo.data.polygons) <= target
# separate parts in the reference silhouette (body, floating book, staff...): the mesh may not have more components than that
def ref_components():
    ref = bpy.data.images.load(front_png); rw, rh = ref.size
    ra = np.empty(rw * rh * 4, np.float32); ref.pixels.foreach_get(ra)
    mk = ra.reshape(rh, rw, 4)[::-1, :, 3] > 0.1
    mk = mk[::4, ::4]
    lab = np.zeros(mk.shape, np.int32); n = 0; sizes = []
    for y0, x0 in zip(*np.where(mk)):
        if lab[y0, x0]: continue
        n += 1; stack = [(y0, x0)]; lab[y0, x0] = n; cnt = 0
        while stack:
            y, x = stack.pop(); cnt += 1
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1), (1, -1), (-1, 1)):
                yy, xx = y + dy, x + dx
                if 0 <= yy < mk.shape[0] and 0 <= xx < mk.shape[1] and mk[yy, xx] and not lab[yy, xx]:
                    lab[yy, xx] = n; stack.append((yy, xx))
        sizes.append(cnt)
    return sum(1 for c in sizes if c >= 0.002 * mk.sum())
REF_CC = ref_components()
qa['ref_components'] = REF_CC
qa['mesh_components'] = qa['final']['components']
qa['pass'] = bool(final_ok and len(lo.data.polygons) <= target and len(lo.data.polygons) >= target * 0.8 and max(qa['orientation']['iou_plain'], qa['orientation']['iou_mirror']) >= 0.6 and qa['final']['components'] <= max(1, REF_CC))

bpy.data.objects.remove(hi, do_unlink=True)
select_only(lo)
bpy.ops.export_scene.gltf(filepath=str(outdir / f'{name}_game.glb'), export_format='GLB', use_selection=True)
bpy.ops.wm.stl_export(filepath=str(outdir / f'{name}_print.stl'), export_selected_objects=True)
bpy.ops.wm.save_as_mainfile(filepath=str(outdir / f'{name}_final.blend'))

sc.render.engine = 'BLENDER_WORKBENCH'
sc.display.shading.light = 'FLAT'; sc.display.shading.color_type = 'TEXTURE'
sc.render.resolution_x, sc.render.resolution_y = 1000, 1500
cam = bpy.data.cameras.new('c'); co = bpy.data.objects.new('c', cam); sc.collection.objects.link(co)
cam.type = 'ORTHO'; cam.ortho_scale = 2.3; sc.camera = co
ctr = Vector((0, 0, 0.9))
for label, az in (('front', 0), ('three_quarter', 40), ('side', 90), ('back', 180)):
    a = math.radians(az)
    co.location = ctr + Vector((6 * math.sin(a), -6 * math.cos(a), 0))
    co.rotation_euler = (ctr - co.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = str(outdir / f'{name}_qa_{label}.png'); bpy.ops.render.render(write_still=True)
(outdir / f'{name}_qa.json').write_text(json.dumps(qa, indent=2))
print('FINISHED', json.dumps(qa))
