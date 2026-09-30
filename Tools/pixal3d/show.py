import bpy, math, mathutils
OUT='S:/AI/Game/Game Assets/characters/Kaela/pixal3d'
bpy.ops.wm.open_mainfile(filepath=OUT+'/Kaela_final.blend')
sc=bpy.context.scene; sc.render.engine='BLENDER_EEVEE'
o=bpy.data.objects['Kaela_fixed']
w=bpy.data.worlds.new('w'); w.use_nodes=True; w.node_tree.nodes['Background'].inputs[0].default_value=(.75,.78,.85,1); w.node_tree.nodes['Background'].inputs[1].default_value=1.0; sc.world=w
for loc,e in (((0,-2,1.5),300),((2,-1,-1),120)):
    l=bpy.data.objects.new('l',bpy.data.lights.new('l','SUN' if False else 'AREA')); l.data.energy=e; l.data.size=3; l.location=loc
    l.rotation_euler=(mathutils.Vector((0,0,0))-mathutils.Vector(loc)).to_track_quat('-Z','Y').to_euler(); sc.collection.objects.link(l)
cam=bpy.data.objects.new('c',bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera=cam
cam.data.type='ORTHO'; cam.data.ortho_scale=1.05
sc.render.resolution_x=700; sc.render.resolution_y=1000
sc.render.film_transparent=False
ctr=mathutils.Vector((0.025,0.13,0.025))
cam.data.ortho_scale=1.05
for name,ang in (('front',0),('three_quarter',-40),('back',180)):
    a=math.radians(ang); cam.location=ctr+mathutils.Vector((math.sin(a)*3,-math.cos(a)*3,0))
    cam.rotation_euler=(math.pi/2,0,a)
    sc.render.filepath=f'{OUT}/view_{name}.png'; bpy.ops.render.render(write_still=True)
