import bpy,json,pathlib
p=pathlib.Path(r'S:\AI\Game\Unity AHCG\My project\MeshyJobs\20260929_010936_jd-card-duelist_01a0ebff')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(p/'model.glb'))
for o in bpy.data.objects:
 if o.type!='MESH': continue
 bpy.context.view_layer.objects.active=o
 mod=o.modifiers.new('Game mesh reduction','DECIMATE');mod.ratio=min(1,60000/len(o.data.polygons));bpy.ops.object.modifier_apply(modifier=mod.name)
 for f in o.data.polygons:f.use_smooth=True
bpy.ops.export_scene.gltf(filepath=str(p/'jd-game.glb'),export_format='GLB')
d={'faces':sum(len(o.data.polygons) for o in bpy.data.objects if o.type=='MESH'),'max_allowed':300000,'source':'model.glb','method':'Blender collapse decimation; original retained'}
(p/'rig-geometry-check.json').write_text(json.dumps(d,indent=2));print(d)
