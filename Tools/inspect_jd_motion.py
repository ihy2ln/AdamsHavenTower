import bpy,json,pathlib
p=pathlib.Path(r'S:\AI\Game\Unity AHCG\My project\MeshyJobs\20260929_010936_jd-card-duelist_01a0ebff')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(p/'draw/motion.fbx'))
d={'fps':bpy.context.scene.render.fps,'objects':[(o.name,o.type,list(o.scale)) for o in bpy.data.objects],'actions':[(a.name,list(a.frame_range)) for a in bpy.data.actions]}
for a in bpy.data.objects:
 if a.type=='ARMATURE': d['bones']=[(b.name,list(b.head_local),list(b.tail_local)) for b in a.data.bones]
(p/'motion-inspect.json').write_text(json.dumps(d,indent=2))
print(json.dumps(d))
