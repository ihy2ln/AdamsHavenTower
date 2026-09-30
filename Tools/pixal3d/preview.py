import bpy, sys, math
glb, out = sys.argv[-2], sys.argv[-1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
objs=[o for o in bpy.data.objects if o.type=='MESH']
print('FACES', sum(len(o.data.polygons) for o in objs), 'TRIS', sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objs), 'dims', [tuple(o.dimensions) for o in objs])
o=objs[0]; d=max(o.dimensions)
sc=bpy.context.scene; sc.render.engine='BLENDER_EEVEE'
import mathutils
cam=bpy.data.objects.new('c',bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera=cam
cam.data.type='ORTHO'; cam.data.ortho_scale=d*1.15
w=bpy.data.worlds.new('w'); w.use_nodes=True; w.node_tree.nodes['Background'].inputs[1].default_value=1.5; sc.world=w
sc.render.resolution_x=768; sc.render.resolution_y=1024
cs=[(o.matrix_world @ mathutils.Vector(c)) for c in o.bound_box]; ctr=sum(cs,mathutils.Vector())/8
for name,ang in (('front',0),('side',90)):
    a=math.radians(ang); cam.location=ctr+mathutils.Vector((math.sin(a)*-d*2, -math.cos(a)*d*2, 0))
    cam.rotation_euler=(math.pi/2,0,a)
    sc.render.filepath=f'{out}_{name}.png'; bpy.ops.render.render(write_still=True)
