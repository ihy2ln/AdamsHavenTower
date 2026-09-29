import bpy,pathlib,json,math
from mathutils import Vector,Matrix,Quaternion
P=pathlib.Path(r'S:\AI\Game\Unity AHCG\My project\MeshyJobs\20260929_010936_jd-card-duelist_01a0ebff')
OUT=pathlib.Path(r'S:\AI\Game\Unity AHCG\My project\Assets\Resources\AdamsHaven\BattleModels\jd');OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(P/'rigged.glb'))
target=next(o for o in bpy.data.objects if o.type=='ARMATURE');target.name='JD_Rig';target.animation_data_clear()
for b in target.pose.bones:b.matrix_basis=Matrix.Identity(4);b.rotation_mode='QUATERNION'
for a in list(bpy.data.actions):bpy.data.actions.remove(a)
original_objects=set(bpy.data.objects)
# Meshy spine names run from Spine02 at the waist to Spine at the shoulders.
mapping={'Hips':'Pelvis','Spine02':'Spine1','Spine01':'Spine2','Spine':'Spine3','neck':'Neck','Head':'Head','LeftShoulder':'L_Collar','LeftArm':'L_Shoulder','LeftForeArm':'L_Elbow','LeftHand':'L_Wrist','RightShoulder':'R_Collar','RightArm':'R_Shoulder','RightForeArm':'R_Elbow','RightHand':'R_Wrist'}
ends={'LeftShoulder':('LeftArm','L_Shoulder'),'LeftArm':('LeftForeArm','L_Elbow'),'LeftForeArm':('LeftHand','L_Wrist'),'RightShoulder':('RightArm','R_Shoulder'),'RightArm':('RightForeArm','R_Elbow'),'RightForeArm':('RightHand','R_Wrist')}
rest={b.name:(target.matrix_world@b.matrix_local).to_quaternion() for b in target.data.bones}
rest_heads={b.name:target.matrix_world@b.head_local for b in target.data.bones}
clips={};pose_frames={}
for label,name in [('draw','AH_draw_card'),('place','AH_place_card')]:
 before=set(bpy.data.objects);bpy.ops.import_scene.fbx(filepath=str(P/label/'motion.fbx'));source=next(o for o in set(bpy.data.objects)-before if o.type=='ARMATURE')
 source_rest={b.name:(source.matrix_world@b.matrix_local).to_quaternion() for b in source.data.bones}
 corrections={}
 for tn,(tc,sc) in ends.items():
  sn=mapping[tn]; td=rest_heads[tc]-rest_heads[tn];sd=(source.matrix_world@source.data.bones[sc].head_local)-(source.matrix_world@source.data.bones[sn].head_local)
  corrections[tn]=td.normalized().rotation_difference(sd.normalized())
 target.animation_data_create();action=bpy.data.actions.new(name);target.animation_data.action=action;clips[name]=action
 frames=[]
 for f in range(1,91):
  bpy.context.scene.frame_set(f);bpy.context.view_layer.update()
  for pb in target.pose.bones:
   pb.matrix_basis=Matrix.Identity(4)
   if pb.name not in mapping or pb.name=='Hips':continue
   sn=mapping[pb.name];sq=(source.matrix_world@source.pose.bones[sn].matrix).to_quaternion();delta=sq@source_rest[sn].inverted();desired=delta@corrections.get(pb.name,Quaternion())@rest[pb.name]
   # Anchor each joint to the parent's current pose; only transfer orientation.
   anchor=(pb.parent.matrix@pb.parent.bone.matrix_local.inverted()@pb.bone.matrix_local).translation if pb.parent else pb.bone.matrix_local.translation
   localrot=target.matrix_world.to_quaternion().inverted()@desired
   pb.matrix=Matrix.Translation(anchor)@localrot.to_matrix().to_4x4()
   bpy.context.view_layer.update()
  # Key all transforms so action switching cannot leave stale bones.
  for pb in target.pose.bones:
   pb.keyframe_insert('rotation_quaternion',frame=f,group=pb.name);pb.keyframe_insert('location',frame=f,group=pb.name);pb.keyframe_insert('scale',frame=f,group=pb.name)
  if f in [1,20,40,60,90]:frames.append({b:list(target.matrix_world@target.pose.bones[b].head) for b in ['LeftHand','RightHand','Head']})
 pose_frames[name]=frames
 for o in list(set(bpy.data.objects)-before):bpy.data.objects.remove(o,do_unlink=True)
# Use the draw's first pose as the ready stance, with subtle breathing.
target.animation_data.action=clips['AH_draw_card'];bpy.context.scene.frame_set(1);bpy.context.view_layer.update();idle_pose={b.name:b.matrix_basis.copy() for b in target.pose.bones}
idle=bpy.data.actions.new('AH_battle_guard');target.animation_data.action=idle;clips[idle.name]=idle
for f in range(1,74):
 for b in target.pose.bones:
  b.matrix_basis=idle_pose[b.name]
  if b.name=='Spine01':b.rotation_quaternion=b.rotation_quaternion@Quaternion((1,0,0),math.sin((f-1)/72*math.tau)*.009)
  b.keyframe_insert('rotation_quaternion',frame=f,group=b.name);b.keyframe_insert('location',frame=f,group=b.name);b.keyframe_insert('scale',frame=f,group=b.name)
# Create visible game cards. Bind props to extra deform bones so they animate in every exported action.
# Their exact contact placement will be tuned against the rendered poses.
bpy.context.view_layer.objects.active=target;target.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
for side in ['Left','Right']:
 hand=target.data.edit_bones[side+'Hand'];bone=target.data.edit_bones.new('Card'+side);bone.head=hand.head.copy();bone.tail=hand.head+Vector((0,1,0));bone.parent=hand
bpy.ops.object.mode_set(mode='OBJECT')
gold=bpy.data.materials.new('JD card gold');gold.diffuse_color=(.8,.57,.16,1);gold.use_nodes=True;gold.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.8,.57,.16,1)
blue=bpy.data.materials.new('JD card enamel');blue.diffuse_color=(.025,.09,.22,1);blue.use_nodes=True;blue.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.025,.09,.22,1)
tex=blue.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(r'S:\AI\Game\Unity AHCG\My project\Assets\Resources\AdamsHaven\FullCards\jd.png');blue.node_tree.links.new(tex.outputs['Color'],blue.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
# Make each prop in armature-local coordinates at its rest hand, then skin rigidly.
for side in ['Left','Right']:
 hand=target.data.bones[side+'Hand'];center=(target.matrix_world@hand.head_local)+((target.matrix_world@hand.tail_local)-(target.matrix_world@hand.head_local)).normalized()*.045
 for inset in [False,True]:
  for selected in bpy.context.selected_objects:selected.select_set(False)
  if inset:
   mesh=bpy.data.meshes.new('Card artwork plane');mesh.from_pydata([(-.5,0,-.5),(.5,0,-.5),(.5,0,.5),(-.5,0,.5)],[],[(0,1,2,3)]);mesh.update();uv=mesh.uv_layers.new(name='UVMap');coords=[(0,0),(1,0),(1,1),(0,1)]
   for poly in mesh.polygons:
    for loop in poly.loop_indices:uv.data[loop].uv=coords[mesh.loops[loop].vertex_index]
   obj=bpy.data.objects.new('CardArtwork',mesh);bpy.context.collection.objects.link(obj);bpy.context.view_layer.objects.active=obj;obj.select_set(True);obj.location=center+Vector((0,-.025,0))
  else:
   bpy.ops.mesh.primitive_cube_add(size=1,location=center+Vector((0,-.025,0)));obj=bpy.context.object
  obj.name=('Deck' if side=='Left' else 'PlayingCard')+('_Face' if inset else '_Border')
  obj.dimensions=(.062 if inset else .073,.006 if inset else (.027 if side=='Left' else .009),.098 if inset else .112)
  if inset:obj.location.y-=.014 if side=='Left' else .006
  bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
  obj.data.materials.append(blue if inset else gold)
  obj.parent=target;obj.matrix_parent_inverse=target.matrix_world.inverted()
  group=obj.vertex_groups.new(name='Card'+side);group.add(list(range(len(obj.data.vertices))),1,'REPLACE');mod=obj.modifiers.new('Follow hand','ARMATURE');mod.object=target
# Key new prop bones, keeping the right card hidden in idle and showing it through draw/play.
for name,action in clips.items():
 target.animation_data.action=action
 last=73 if name=='AH_battle_guard' else 90
 for f in range(1,last+1):
  bpy.context.scene.frame_set(f)
  for side in ['Left','Right']:
   b=target.pose.bones['Card'+side];b.matrix_basis=Matrix.Identity(4)
   if side=='Right':
    visible=name!='AH_battle_guard' and (18<=f<=76 if name=='AH_draw_card' else f<=67)
    b.scale=Vector((1,1,1)) if visible else Vector((.001,.001,.001))
   b.keyframe_insert('scale',frame=f,group=b.name)
# Retain only target actions and export external textures with the rig.
for a in list(bpy.data.actions):
 if a.name not in clips:bpy.data.actions.remove(a)
 else:a.use_fake_user=True
for i,img in enumerate(bpy.data.images):
 if img.type=='IMAGE':img.filepath_raw=str(OUT/f'jd_texture_{i}.png');img.file_format='PNG';img.save()
target.animation_data.action=idle;bpy.context.scene.frame_set(1)
for o in bpy.context.selected_objects:o.select_set(False)
for o in bpy.data.objects:
 if o.type in {'ARMATURE','MESH'}:o.select_set(True)
bpy.context.view_layer.objects.active=target
bpy.ops.wm.save_as_mainfile(filepath=str(P/'jd-card-actions.blend'))
bpy.ops.export_scene.fbx(filepath=str(OUT/'model.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,path_mode='COPY',axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL')
(P/'retarget-poses.json').write_text(json.dumps(pose_frames,indent=2));print('EXPORTED JD',list(clips))
