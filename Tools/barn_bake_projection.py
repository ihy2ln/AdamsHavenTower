"""Blender (headless): re-colour a Trellis barn atlas by projecting the source painting from the front.

  blender -b -P Tools/barn_bake_projection.py -- <in.fbx> <trellis_base.png> <painting.png> <out.png> <bays> <x0> <x1> <y0> <y1>

Front-facing texels take the painting's colours (orthographic projection along +Y, painting bbox x0..x1,y0..y1 in
canvas pixels fitted to the mesh footprint); side, roof-underside and floor texels keep the Trellis colours.
The result is baked into the mesh's existing UV atlas with Cycles (emission only).
"""
import bpy, sys
from pathlib import Path

fbx, base_png, paint_png, out_png, bays, x0, x1, y0, y1 = sys.argv[-9:]
bays = int(bays); x0, x1, y0, y1 = map(float, (x0, x1, y0, y1))

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
mesh = [o for o in bpy.context.scene.objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = mesh
zs = [v.co.z for v in mesh.data.vertices]
xs = [v.co.x for v in mesh.data.vertices]
tw, th = max(xs) - min(xs), max(zs) - min(zs)
W, H = 2824.0, 1024.0

mat = bpy.data.materials.new('bake'); mat.use_nodes = True
nt = mat.node_tree; nt.nodes.clear()
N = nt.nodes.new; L = nt.links.new

base = N('ShaderNodeTexImage'); base.image = bpy.data.images.load(base_png)
paint = N('ShaderNodeTexImage'); paint.image = bpy.data.images.load(paint_png)
paint.extension = 'CLIP'; paint.interpolation = 'Linear'
paint.image.alpha_mode = 'STRAIGHT'

geo = N('ShaderNodeNewGeometry')
sep = N('ShaderNodeSeparateXYZ'); L(geo.outputs['Position'], sep.inputs[0])
# painting uv from world position
def math_(op, a, b=None, clamp=False):
    n = N('ShaderNodeMath'); n.operation = op; n.use_clamp = clamp
    if isinstance(a, (int, float)): n.inputs[0].default_value = a
    else: L(a, n.inputs[0])
    if b is not None:
        if isinstance(b, (int, float)): n.inputs[1].default_value = b
        else: L(b, n.inputs[1])
    return n.outputs[0]
u = math_('MULTIPLY', math_('ADD', math_('DIVIDE', sep.outputs['X'], tw), 0.5), (x1 - x0) / W)
u = math_('ADD', u, x0 / W)
v = math_('MULTIPLY', math_('DIVIDE', sep.outputs['Z'], th), (y1 - y0) / H)   # up from bbox bottom
v = math_('ADD', v, 1.0 - y1 / H)
comb = N('ShaderNodeCombineXYZ'); L(u, comb.inputs['X']); L(v, comb.inputs['Y'])
L(comb.outputs[0], paint.inputs['Vector'])

# frontness: -normal.y (front faces -Y) -> ramp 0.55..0.9
nsep = N('ShaderNodeSeparateXYZ'); L(geo.outputs['Normal'], nsep.inputs[0])
front = math_('MULTIPLY', nsep.outputs['Y'], -1.0)
ramp = N('ShaderNodeMapRange'); ramp.clamp = True
ramp.inputs['From Min'].default_value = 0.55; ramp.inputs['From Max'].default_value = 0.9
L(front, ramp.inputs['Value'])
factor = math_('MULTIPLY', ramp.outputs['Result'], paint.outputs['Alpha'])

emit = N('ShaderNodeEmission')
out = N('ShaderNodeOutputMaterial'); L(emit.outputs[0], out.inputs['Surface'])
target = bpy.data.images.new('baked', 2048, 2048, alpha=False)
tnode = N('ShaderNodeTexImage'); tnode.image = target
nt.nodes.active = tnode
mesh.data.materials.clear(); mesh.data.materials.append(mat)

sc = bpy.context.scene
sc.render.engine = 'CYCLES'; sc.cycles.samples = 1; sc.cycles.device = 'GPU'
try:
    prefs = bpy.context.preferences.addons['cycles'].preferences
    prefs.compute_device_type = 'OPTIX'; prefs.get_devices()
    for d in prefs.devices: d.use = True
except Exception as e:
    print('GPU setup failed, using CPU', e); sc.cycles.device = 'CPU'
sc.cycles.bake_type = 'EMIT'
sc.render.bake.margin = 8; sc.render.bake.margin_type = 'EXTEND'
sc.view_settings.view_transform = 'Standard'
sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGB'

def bake(link_from, path):
    for l in list(emit.inputs['Color'].links): nt.links.remove(l)
    L(link_from, emit.inputs['Color'])
    nt.nodes.active = tnode
    bpy.ops.object.bake(type='EMIT')
    target.save_render(path, scene=sc)
    print('BAKED', path)

comb2 = N('ShaderNodeCombineColor')
for i in range(3): L(factor, comb2.inputs[i])
bake(paint.outputs['Color'], out_png)                                  # painting projected onto the atlas
bake(comb2.outputs[0], str(Path(out_png).with_name(Path(out_png).stem + '_factor.png')))  # projection weight
