import bpy,json,pathlib
p=pathlib.Path(r'S:\AI\Game\Unity AHCG\My project\MeshyJobs\20260929_010936_jd-card-duelist_01a0ebff')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(p/'model.glb'))
d={'faces':sum(len(o.data.polygons) for o in bpy.data.objects if o.type=='MESH'),'triangles':sum(sum(len(f.vertices)-2 for f in o.data.polygons) for o in bpy.data.objects if o.type=='MESH'),'textured':len(bpy.data.images)>0}
(p/'geometry-check.json').write_text(json.dumps(d,indent=2));print(d)
