import bpy, bmesh, sys
src, dst = sys.argv[-2], sys.argv[-1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
o=[o for o in bpy.data.objects if o.type=='MESH'][0]
bpy.context.view_layer.objects.active=o; o.select_set(True)
bm=bmesh.new(); bm.from_mesh(o.data)
bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=1e-6)
seen=set();cs=[]
for f in bm.faces:
    if f in seen: continue
    st=[f];seen.add(f);c=[]
    while st:
        x=st.pop();c.append(x)
        for e in x.edges:
            for g in e.link_faces:
                if g not in seen: seen.add(g);st.append(g)
    cs.append(c)
cs.sort(key=len,reverse=True)
bmesh.ops.delete(bm,geom=[f for c in cs[1:] for f in c],context='FACES')
bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
bm.to_mesh(o.data); bm.free()
o.data.uv_layers.clear() if hasattr(o.data.uv_layers,'clear') else None
def check(tag):
    b=bmesh.new(); b.from_mesh(o.data)
    bd=sum(1 for e in b.edges if e.is_boundary); nm=sum(1 for e in b.edges if len(e.link_faces)>2)
    nmv=sum(1 for v in b.verts if not v.is_manifold)
    seen=set();n=0
    for f in b.faces:
        if f in seen: continue
        n+=1;st=[f];seen.add(f)
        while st:
            x=st.pop()
            for e in x.edges:
                for g in e.link_faces:
                    if g not in seen: seen.add(g);st.append(g)
    print('REP',tag,'F',len(b.faces),'boundary',bd,'nm_edges',nm,'nm_verts',nmv,'shells',n,'vol',round(b.calc_volume(signed=True),6)); b.free()
check('pre')
vs=float(sys.argv[-3]) if False else 0.0025
o.data.remesh_voxel_size=vs; o.data.remesh_voxel_adaptivity=0.0
bpy.ops.object.voxel_remesh(); check('voxel')
def tri():
    b=bmesh.new(); b.from_mesh(o.data); bmesh.ops.triangulate(b,faces=b.faces); b.to_mesh(o.data); b.free()
def keep_big(frac):
    b=bmesh.new(); b.from_mesh(o.data)
    seen=set();cs=[]
    for f in b.faces:
        if f in seen: continue
        st=[f];seen.add(f);c=[]
        while st:
            x=st.pop();c.append(x)
            for e in x.edges:
                for g in e.link_faces:
                    if g not in seen: seen.add(g);st.append(g)
        cs.append(c)
    cs.sort(key=len,reverse=True); print('SHELLS',[len(c) for c in cs][:30])
    kill=[f for c in cs if len(c)<len(cs[0])*frac for f in c]
    bmesh.ops.delete(b,geom=kill,context='FACES'); bmesh.ops.delete(b,geom=[v for v in b.verts if not v.link_faces],context='VERTS')
    b.to_mesh(o.data); b.free()
keep_big(0.01); tri(); check('big+tri')
m=o.modifiers.new('d','DECIMATE'); m.decimate_type='COLLAPSE'; m.ratio=30000/len(o.data.polygons)
bpy.ops.object.modifier_apply(modifier='d'); tri(); check('decimated')
o.name='Kaela_fixed'
bpy.ops.wm.save_as_mainfile(filepath=dst)
