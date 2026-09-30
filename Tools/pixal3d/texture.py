import bpy, bmesh, sys, os, numpy as np
from mathutils import Vector
D = 'S:/AI/Game/Unity AHCG/My project/Tools/pixal3d'
OUT = 'S:/AI/Game/Game Assets/characters/Kaela/pixal3d'
GLB = OUT + '/Kaela_pixal3d_30k.glb'
RES = 4096
bpy.ops.wm.open_mainfile(filepath=OUT + '/repaired.blend')
fx = bpy.data.objects['Kaela_fixed']
before = set(bpy.data.objects)
bpy.ops.import_scene.gltf(filepath=GLB)
src = [o for o in bpy.data.objects if o not in before and o.type == 'MESH'][0]
src.name = 'Kaela_src'
sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = 8
def sel(active, others=()):
    bpy.ops.object.select_all(action='DESELECT')
    for o in others: o.select_set(True)
    active.select_set(True); bpy.context.view_layer.objects.active = active

sel(fx, [fx]); bpy.ops.object.shade_smooth()
# --- UVs on the fixed mesh
sel(fx, [fx]); bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.004); bpy.ops.object.mode_set(mode='OBJECT')
fx.data.uv_layers[0].name = 'UVMap'

# --- Front projection UV layer (fit mesh bbox to the silhouette mask bbox)
mx0, mx1, my0, my1 = 5, 1023, 13, 1492     # mask bbox px (1024x1536 image)
W, H = 1024, 1536
co = np.array([tuple(v.co) for v in fx.data.vertices])
mn, mx = co.min(0), co.max(0)
import json
sx, sz, tx, tz, k, kx, kz = json.load(open(D + '/tex/cam.json'))
front = fx.data.uv_layers.new(name='Front')
for poly in fx.data.polygons:
    for li in poly.loop_indices:
        c = fx.data.vertices[fx.data.loops[li].vertex_index].co
        yy = (c.y - mn[1]) / (mx[1] - mn[1]); den = 1 + k * yy
        u = ((c.x - mn[0]) / (mx[0] - mn[0]) * sx + tx + kx * yy) / den
        v = ((mx[2] - c.z) / (mx[2] - mn[2]) * sz + tz + kz * yy) / den
        front.data[li].uv = (u, 1 - v)
fx.data.uv_layers.active_index = 0

def img(name, path=None, w=RES, h=RES, cs='sRGB'):
    if path: i = bpy.data.images.load(path); i.name = name; i.colorspace_settings.name = cs; return i
    i = bpy.data.images.new(name, w, h, alpha=False); i.colorspace_settings.name = cs; return i
def mat_for(obj, name):
    m = bpy.data.materials.new(name); m.use_nodes = True; obj.data.materials.clear(); obj.data.materials.append(m)
    m.node_tree.nodes.clear(); return m, m.node_tree.nodes, m.node_tree.links

# --- Bake 1: Pixal texture (source, original UVs) -> fixed UVMap
base = img('kaela_pixal_bake')
m, n, l = mat_for(fx, 'bake1')
t = n.new('ShaderNodeTexImage'); t.image = base; n.active = t
o_ = n.new('ShaderNodeOutputMaterial')
b = n.new('ShaderNodeBsdfDiffuse'); l.new(b.outputs[0], o_.inputs[0])
fx.data.uv_layers['UVMap'].active = True
sel(fx, [src, fx])
bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, use_selected_to_active=True, cage_extrusion=0.006, max_ray_distance=0.03, margin=24, margin_type='EXTEND')
base.pack() if False else None

# --- Bake 2: composite (Pixal base + projected anime art on front-facing surface) -> final
anime = img('kaela_anime', D + '/tex/anime.png')
mask = img('kaela_mask', D + '/tex/mask.png', cs='Non-Color')
final = img('kaela_anime_final')
m, n, l = mat_for(fx, 'Kaela_Anime')
uv0 = n.new('ShaderNodeUVMap'); uv0.uv_map = 'UVMap'
uvf = n.new('ShaderNodeUVMap'); uvf.uv_map = 'Front'
tb = n.new('ShaderNodeTexImage'); tb.image = base; tb.extension = 'EXTEND'
ta = n.new('ShaderNodeTexImage'); ta.image = anime; ta.extension = 'EXTEND'
tm = n.new('ShaderNodeTexImage'); tm.image = mask; tm.extension = 'EXTEND'
l.new(uv0.outputs[0], tb.inputs[0]); l.new(uvf.outputs[0], ta.inputs[0]); l.new(uvf.outputs[0], tm.inputs[0])
geo = n.new('ShaderNodeNewGeometry')
dot = n.new('ShaderNodeVectorMath'); dot.operation = 'DOT_PRODUCT'; dot.inputs[1].default_value = (0, -1, 0)
l.new(geo.outputs['Normal'], dot.inputs[0])
rng = n.new('ShaderNodeMapRange'); rng.inputs[1].default_value = 0.6; rng.inputs[2].default_value = 0.9; rng.interpolation_type = 'SMOOTHSTEP'
l.new(dot.outputs['Value'], rng.inputs[0])
mul = n.new('ShaderNodeMath'); mul.operation = 'MULTIPLY'
l.new(rng.outputs[0], mul.inputs[0]); l.new(tm.outputs['Color'], mul.inputs[1])
mix = n.new('ShaderNodeMix'); mix.data_type = 'RGBA'
l.new(mul.outputs[0], mix.inputs[0]); l.new(tb.outputs['Color'], mix.inputs[6]); l.new(ta.outputs['Color'], mix.inputs[7])
em = n.new('ShaderNodeEmission'); l.new(mix.outputs[2], em.inputs[0])
o_ = n.new('ShaderNodeOutputMaterial'); l.new(em.outputs[0], o_.inputs[0])
tf = n.new('ShaderNodeTexImage'); tf.image = final; n.active = tf      # bake target (unconnected)
fx.data.uv_layers['UVMap'].active = True
sel(fx, [fx])
bpy.ops.object.bake(type='EMIT', margin=24, margin_type='EXTEND')

# --- Final material: plain image on UVMap
for f_, nm in ((base, 'kaela_pixal_bake.png'), (final, 'kaela_anime_final.png')):
    f_.filepath_raw = OUT + '/' + nm; f_.file_format = 'PNG'; f_.save()
m, n, l = mat_for(fx, 'Kaela_Anime')
t = n.new('ShaderNodeTexImage'); t.image = final
bs = n.new('ShaderNodeBsdfPrincipled'); bs.inputs['Roughness'].default_value = 0.8
o_ = n.new('ShaderNodeOutputMaterial')
l.new(t.outputs[0], bs.inputs['Base Color']); l.new(bs.outputs[0], o_.inputs[0])
bpy.data.objects.remove(src)
final.pack()
bpy.ops.wm.save_as_mainfile(filepath=OUT + '/Kaela_final.blend')
print('DONE')
