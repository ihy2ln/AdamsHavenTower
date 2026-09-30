import bpy, bmesh, sys
src, dst = sys.argv[-2], sys.argv[-1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
o=[o for o in bpy.data.objects if o.type=='MESH'][0]
bm=bmesh.new(); bm.from_mesh(o.data)
bm.faces.ensure_lookup_table()
def comps():
    seen=set();out=[]
    for f in bm.faces:
        if f in seen: continue
        st=[f];seen.add(f);c=[]
        while st:
            x=st.pop();c.append(x)
            for e in x.edges:
                for g in e.link_faces:
                    if g not in seen: seen.add(g);st.append(g)
        out.append(c)
    return out
def rep(tag):
    bd=sum(1 for e in bm.edges if e.is_boundary); nm=sum(1 for e in bm.edges if len(e.link_faces)>2)
    cs=comps(); print('REP',tag,'V',len(bm.verts),'F',len(bm.faces),'boundary',bd,'nonmanifold',nm,'comps',len(cs),'vol',round(bm.calc_volume(signed=True),6))
bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=1e-6); rep('weld')
cs=sorted(comps(),key=len,reverse=True); big=len(cs[0])
bmesh.ops.delete(bm,geom=[f for c in cs[1:] for f in c],context='FACES'); 
bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS'); rep('main only')
for it in range(6):
    bad=[e for e in bm.edges if len(e.link_faces)>2]
    if not bad: break
    # delete faces on non-manifold edges, then clean
    fs={f for e in bad for f in e.link_faces}
    bmesh.ops.delete(bm,geom=list(fs),context='FACES')
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
    cs=sorted(comps(),key=len,reverse=True)
    bmesh.ops.delete(bm,geom=[f for c in cs if len(c)<big*0.02 for f in c],context='FACES')
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
    bmesh.ops.holes_fill(bm,edges=[e for e in bm.edges if e.is_boundary],sides=64)
    bmesh.ops.triangulate(bm,faces=bm.faces)
    rep(f'iter{it}')
bmesh.ops.recalc_face_normals(bm,faces=bm.faces)
rep('final')
me=bpy.data.meshes.new('Kaela_fixed'); bm.to_mesh(me)
ob=bpy.data.objects.new('Kaela_fixed',me); bpy.context.scene.collection.objects.link(ob)
bpy.data.objects.remove(o)
bpy.ops.wm.save_as_mainfile(filepath=dst)
