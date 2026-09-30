import bpy, bmesh, sys
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=sys.argv[-1])
for o in [o for o in bpy.data.objects if o.type=='MESH']:
    bm=bmesh.new(); bm.from_mesh(o.data)
    bd=[e for e in bm.edges if e.is_boundary]; nm=[e for e in bm.edges if not e.is_manifold]
    # components
    seen=set(); comps=[]
    for f in bm.faces:
        if f in seen: continue
        st=[f]; seen.add(f); n=0
        while st:
            x=st.pop(); n+=1
            for e in x.edges:
                for g in e.link_faces:
                    if g not in seen: seen.add(g); st.append(g)
        comps.append(n)
    bm.normal_update()
    print('OBJ',o.name,'V',len(bm.verts),'F',len(bm.faces),'boundary',len(bd),'nonmanifold',len(nm),'components',sorted(comps,reverse=True)[:10],len(comps),'uv',[u.name for u in o.data.uv_layers],'mats',[m.name for m in o.data.materials],'loc',tuple(o.location),'rot',tuple(o.rotation_euler),'dim',tuple(o.dimensions))
