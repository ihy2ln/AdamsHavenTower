import bpy,pathlib
from mathutils import Vector
p=pathlib.Path(r'S:\AI\Game\Unity AHCG\My project\MeshyJobs\20260929_010936_jd-card-duelist_01a0ebff')
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(p/'jd-game.glb'))
points=[o.matrix_world@Vector(c) for o in bpy.data.objects if o.type=='MESH' for c in o.bound_box]; lo=Vector([min(v[i] for v in points) for i in range(3)]);hi=Vector([max(v[i] for v in points) for i in range(3)]);center=(lo+hi)/2;h=hi.z-lo.z
bpy.ops.object.camera_add(location=center+Vector((h*.35,-h*2,h*.15)));cam=bpy.context.object;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=h*1.2;bpy.context.scene.camera=cam
for pos,power,size in [((h,-h,h*2),600,4),((-h,-h,h),350,3)]:
 bpy.ops.object.light_add(type='AREA',location=center+Vector(pos));o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(center-o.location).to_track_quat('-Z','Y').to_euler()
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=16;s.render.resolution_x=512;s.render.resolution_y=512;s.render.resolution_percentage=100;s.render.film_transparent=True;s.render.filepath=str(p/'game-mesh-preview.png');bpy.ops.render.render(write_still=True)
