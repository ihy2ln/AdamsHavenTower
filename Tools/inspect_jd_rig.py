import bpy,pathlib,json
p=pathlib.Path(r'S:\AI\Game\Unity AHCG\My project\MeshyJobs\20260929_010936_jd-card-duelist_01a0ebff')
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.gltf(filepath=str(p/'rigged.glb'));a=next(o for o in bpy.data.objects if o.type=='ARMATURE');a.animation_data_clear();d={'matrix':list(map(list,a.matrix_world)),'bones':[(b.name,b.parent.name if b.parent else None,list(a.matrix_world@b.head_local),list(a.matrix_world@b.tail_local)) for b in a.data.bones]};(p/'rig-inspect.json').write_text(json.dumps(d,indent=2));print(json.dumps(d))
