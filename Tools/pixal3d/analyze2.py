import bpy, bmesh, sys
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=sys.argv[-1])
o=[o for o in bpy.data.objects if o.type=='MESH'][0]
bm=bmesh.new(); bm.from_mesh(o.data)
bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=1e-6)
def rep(tag):
    bd=[e for e in bm.edges if e.is_boundary]; nm=[e for e in bm.edges if not e.is_manifold and not e.is_boundary]
    seen=set();comps=[]
    for f in bm.faces:
        if f in seen: continue
        st=[f];seen.add(f);n=0
        while st:
            x=st.pop();n+=1
            for e in x.edges:
                for g in e.link_faces:
                    if g not in seen: seen.add(g);st.append(g)
        comps.append(n)
    print('REP',tag,'V',len(bm.verts),'F',len(bm.faces),'boundary',len(bd),'nonmanifold_multi',len(nm),'comps',sorted(comps,reverse=True)[:8],len(comps),'vol',bm.calc_volume(signed=True))
rep('welded')
