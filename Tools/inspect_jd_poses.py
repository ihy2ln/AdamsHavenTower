import bpy,pathlib,json
p=pathlib.Path(r'S:\AI\Game\Unity AHCG\My project\MeshyJobs\20260929_010936_jd-card-duelist_01a0ebff')
for label in ['draw','place']:
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(p/label/'motion.fbx'));a=next(o for o in bpy.data.objects if o.type=='ARMATURE');d={}
 for f in [1,15,30,45,60,75,90]:
  bpy.context.scene.frame_set(f);d[f]={b: list(a.matrix_world@a.pose.bones[b].head) for b in ['Pelvis','Head','L_Wrist','R_Wrist']}
 (p/f'{label}-poses.json').write_text(json.dumps(d,indent=2))
 print(label,d)
