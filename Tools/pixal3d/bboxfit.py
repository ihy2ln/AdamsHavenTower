import bpy,sys
bpy.ops.wm.open_mainfile(filepath=sys.argv[-1])
o=bpy.data.objects['Kaela_fixed']
import numpy as np
v=np.array([tuple(x.co) for x in o.data.vertices]); print('BBOX min',v.min(0),'max',v.max(0))
