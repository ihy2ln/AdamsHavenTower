import bpy, numpy as np
bpy.ops.wm.open_mainfile(filepath='S:/AI/Game/Game Assets/characters/Kaela/pixal3d/repaired.blend')
o=bpy.data.objects['Kaela_fixed']; me=o.data
v=np.array([tuple(x.co) for x in me.vertices],dtype=np.float32); t=np.array([tuple(p.vertices) for p in me.polygons],dtype=np.int32)
np.savez('S:/AI/Game/Unity AHCG/My project/Tools/pixal3d/tex/mesh.npz',v=v,t=t)
