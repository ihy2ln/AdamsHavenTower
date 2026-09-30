import bpy
OUT='S:/AI/Game/Game Assets/characters/Kaela/pixal3d'
bpy.ops.wm.open_mainfile(filepath=OUT+'/Kaela_final.blend')
o=bpy.data.objects['Kaela_fixed']; bpy.ops.object.select_all(action='DESELECT'); o.select_set(True); bpy.context.view_layer.objects.active=o
bpy.ops.export_scene.gltf(filepath=OUT+'/Kaela_anime_30k.glb',use_selection=True,export_format='GLB')
o.scale=(1000,1000,1000); bpy.ops.object.transform_apply(scale=True)   # mm for printing
bpy.ops.wm.stl_export(filepath=OUT+'/Kaela_printable_30k.stl',export_selected_objects=True)
print('EXPORTED',[round(x,1) for x in o.dimensions])
