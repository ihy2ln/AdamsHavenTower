import bpy,pathlib,json
src=pathlib.Path(r'S:\AI\Harness\Codex\home\worktrees\chibi-3d-tpose\AdamsHavenCardGame\art\character_cards\chibi-shared-3d-v2')
out=pathlib.Path(r'S:\AI\Game\Unity AHCG\My project\Assets\Resources\AdamsHaven\BattleModels')
for folder in sorted(src.iterdir()):
 p=folder/'battle-motion-v1'/'battle.glb'
 if not p.exists(): continue
 slug=folder.name; key={'kaela-stormfang':'kaela','ghislaine-dedoldia':'ghislaine','elara-vellum':'elara','helda-frostmane':'helda','daisy-bonfire-wildheart':'daisy','clarity-lockhart':'clarity','amara-the-alluring-empress':'amara'}.get(slug,slug)
 dest=out/key;dest.mkdir(parents=True,exist_ok=True)
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.ops.import_scene.gltf(filepath=str(p))
 for i,img in enumerate(bpy.data.images):
  if img.type=='IMAGE':
   img.filepath_raw=str(dest/f'texture_{i}.png');img.file_format='PNG';img.save()
 bpy.ops.export_scene.fbx(filepath=str(dest/'model.fbx'),object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,path_mode='COPY',embed_textures=False,axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL')
 (dest/'provenance.json').write_text(json.dumps({'source':str(p),'clips':[a.name for a in bpy.data.actions]},indent=2))
 print('EXPORTED',key,flush=True)
