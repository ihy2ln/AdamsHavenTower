using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// Runtime glTF 2.0 loader for imported character models (.glb, or .gltf with its .bin/textures beside it or embedded
// as data URIs). Static meshes only: node hierarchy, positions, normals, UVs, indices and the base-colour texture.
// Skins and animations are ignored (the model shows its bind pose); the battle animates it procedurally.
// glTF is right-handed with +Z forward; Z is flipped so the model faces -Z like the project's own battle prefabs.
public static class MediaGltf
{
    public static GameObject Load(string path, out string error)
    {
        error = null;
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            string json; byte[] bin = null;
            if (bytes.Length >= 12 && BitConverter.ToUInt32(bytes, 0) == 0x46546C67)
            {
                int offset = 12;
                json = null;
                while (offset + 8 <= bytes.Length)
                {
                    int length = BitConverter.ToInt32(bytes, offset);
                    uint type = BitConverter.ToUInt32(bytes, offset + 4);
                    if (type == 0x4E4F534A) json = Encoding.UTF8.GetString(bytes, offset + 8, length);
                    else if (type == 0x004E4942) { bin = new byte[length]; Buffer.BlockCopy(bytes, offset + 8, bin, 0, length); }
                    offset += 8 + length;
                }
                if (json == null) { error = "The .glb has no JSON chunk."; return null; }
            }
            else json = Encoding.UTF8.GetString(bytes);
            var loader = new Reader(MediaJson.Parse(json), bin, Path.GetDirectoryName(path));
            return loader.Build(Path.GetFileNameWithoutExtension(path), out error);
        }
        catch (Exception e)
        {
            error = "Could not read the model: " + e.Message;
            return null;
        }
    }

    private sealed class Reader
    {
        private readonly object root;
        private readonly byte[] glbBin;
        private readonly string folder;
        private readonly Dictionary<int, byte[]> buffers = new Dictionary<int, byte[]>();
        private readonly Dictionary<int, Texture2D> textures = new Dictionary<int, Texture2D>();
        private readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();
        private Material fallback;

        public Reader(object root, byte[] glbBin, string folder) { this.root = root; this.glbBin = glbBin; this.folder = folder; }

        public GameObject Build(string name, out string error)
        {
            error = null;
            var extensions = MediaJson.Arr(root, "extensionsRequired");
            if (extensions != null)
                foreach (var ext in extensions)
                    if (ext as string == "KHR_draco_mesh_compression" || ext as string == "EXT_meshopt_compression")
                    { error = "Compressed meshes (" + ext + ") are not supported. Export the model without mesh compression."; return null; }
            var nodes = MediaJson.Arr(root, "nodes");
            var scenes = MediaJson.Arr(root, "scenes");
            var top = new GameObject(name);
            top.SetActive(false);
            List<object> roots = null;
            if (scenes != null && scenes.Count > 0) roots = MediaJson.Arr(scenes[Math.Max(0, MediaJson.Int(root, "scene", 0))], "nodes");
            if (roots == null && nodes != null)
            {
                // No scene: every node that is nobody's child is a root.
                var children = new HashSet<int>();
                foreach (var n in nodes) { var c = MediaJson.Arr(n, "children"); if (c != null) foreach (var i in c) children.Add((int)(double)i); }
                roots = new List<object>();
                for (int i = 0; i < nodes.Count; i++) if (!children.Contains(i)) roots.Add((double)i);
            }
            int meshCount = 0;
            if (roots != null) foreach (var r in roots) meshCount += Node(nodes, (int)(double)r, top.transform, 0);
            if (meshCount == 0)
            {
                // Some exporters leave meshes unreferenced; show them all at the origin.
                var meshes = MediaJson.Arr(root, "meshes");
                if (meshes != null) for (int m = 0; m < meshes.Count; m++) meshCount += Mesh(m, top.transform);
            }
            if (meshCount == 0) { UnityEngine.Object.Destroy(top); error = "The file has no meshes."; return null; }
            return top;
        }

        private int Node(List<object> nodes, int index, Transform parent, int depth)
        {
            if (nodes == null || index < 0 || index >= nodes.Count || depth > 64) return 0;
            var node = nodes[index];
            var go = new GameObject(MediaJson.Str(node, "name") ?? "node" + index);
            go.transform.SetParent(parent, false);
            float[] m = MediaJson.Floats(node, "matrix");
            if (m != null && m.Length == 16)
            {
                var mat = new Matrix4x4();
                for (int c = 0; c < 4; c++) for (int r = 0; r < 4; r++) mat[r, c] = m[c * 4 + r];
                var flip = Matrix4x4.Scale(new Vector3(1, 1, -1));
                mat = flip * mat * flip;
                go.transform.localPosition = mat.GetColumn(3);
                go.transform.localRotation = mat.rotation;
                go.transform.localScale = mat.lossyScale;
            }
            else
            {
                float[] t = MediaJson.Floats(node, "translation"), q = MediaJson.Floats(node, "rotation"), s = MediaJson.Floats(node, "scale");
                if (t != null && t.Length == 3) go.transform.localPosition = new Vector3(t[0], t[1], -t[2]);
                if (q != null && q.Length == 4) go.transform.localRotation = new Quaternion(-q[0], -q[1], q[2], q[3]);
                if (s != null && s.Length == 3) go.transform.localScale = new Vector3(s[0], s[1], s[2]);
            }
            int count = 0;
            int mesh = MediaJson.Int(node, "mesh");
            if (mesh >= 0) count += Mesh(mesh, go.transform);
            var children = MediaJson.Arr(node, "children");
            if (children != null) foreach (var c in children) count += Node(nodes, (int)(double)c, go.transform, depth + 1);
            return count;
        }

        private int Mesh(int index, Transform parent)
        {
            var meshes = MediaJson.Arr(root, "meshes");
            if (meshes == null || index >= meshes.Count) return 0;
            var prims = MediaJson.Arr(meshes[index], "primitives");
            if (prims == null) return 0;
            int made = 0;
            for (int p = 0; p < prims.Count; p++)
            {
                var prim = prims[p];
                int mode = MediaJson.Int(prim, "mode", 4);
                if (mode != 4) continue;                         // triangles only
                var attributes = MediaJson.Obj(prim, "attributes");
                int posAcc = MediaJson.Int(attributes, "POSITION");
                if (posAcc < 0) continue;
                float[] pos = ReadFloats(posAcc, out int posComp);
                if (pos == null || posComp != 3) continue;
                int vertexCount = pos.Length / 3;
                var vertices = new Vector3[vertexCount];
                for (int v = 0; v < vertexCount; v++) vertices[v] = new Vector3(pos[v * 3], pos[v * 3 + 1], -pos[v * 3 + 2]);
                var mesh = new Mesh { name = "mesh" + index + "_" + p };
                if (vertexCount > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.vertices = vertices;
                int nAcc = MediaJson.Int(attributes, "NORMAL");
                if (nAcc >= 0)
                {
                    float[] n = ReadFloats(nAcc, out int nComp);
                    if (n != null && nComp == 3 && n.Length / 3 == vertexCount)
                    {
                        var normals = new Vector3[vertexCount];
                        for (int v = 0; v < vertexCount; v++) normals[v] = new Vector3(n[v * 3], n[v * 3 + 1], -n[v * 3 + 2]);
                        mesh.normals = normals;
                    }
                }
                int uvAcc = MediaJson.Int(attributes, "TEXCOORD_0");
                if (uvAcc >= 0)
                {
                    float[] uv = ReadFloats(uvAcc, out int uvComp);
                    if (uv != null && uvComp == 2 && uv.Length / 2 == vertexCount)
                    {
                        var uvs = new Vector2[vertexCount];
                        for (int v = 0; v < vertexCount; v++) uvs[v] = new Vector2(uv[v * 2], 1f - uv[v * 2 + 1]);
                        mesh.uv = uvs;
                    }
                }
                int idxAcc = MediaJson.Int(prim, "indices");
                int[] tris;
                if (idxAcc >= 0) tris = ReadInts(idxAcc);
                else { tris = new int[vertexCount - vertexCount % 3]; for (int t = 0; t < tris.Length; t++) tris[t] = t; }
                if (tris == null) continue;
                // Flipping one axis mirrors the mesh, so the winding is reversed to keep the faces pointing out.
                for (int t = 0; t + 2 < tris.Length; t += 3) { int a = tris[t + 1]; tris[t + 1] = tris[t + 2]; tris[t + 2] = a; }
                mesh.triangles = tris;
                if (nAcc < 0) mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(parent, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = MaterialFor(MediaJson.Int(prim, "material"));
                made++;
            }
            return made;
        }

        // ---- materials and textures --------------------------------------------------------------------

        private static Shader BaseShader()
        {
            // URP Lit: its main texture is what the battle reads when it re-shades the model in the anime toon style.
            return Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Unlit/Texture");
        }

        private Material MaterialFor(int index)
        {
            Material mat;
            if (index < 0)
            {
                if (fallback == null) fallback = new Material(BaseShader()) { name = "imported default" };
                return fallback;
            }
            if (materials.TryGetValue(index, out mat)) return mat;
            var defs = MediaJson.Arr(root, "materials");
            object def = defs != null && index < defs.Count ? defs[index] : null;
            mat = new Material(BaseShader()) { name = MediaJson.Str(def, "name") ?? "imported " + index };
            var pbr = MediaJson.Obj(def, "pbrMetallicRoughness");
            float[] factor = MediaJson.Floats(pbr, "baseColorFactor");
            Color color = factor != null && factor.Length == 4 ? new Color(factor[0], factor[1], factor[2], factor[3]) : Color.white;
            var texInfo = MediaJson.Obj(pbr, "baseColorTexture");
            Texture2D tex = texInfo != null ? Texture(MediaJson.Int(texInfo, "index")) : null;
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            materials[index] = mat;
            return mat;
        }

        private Texture2D Texture(int index)
        {
            if (index < 0) return null;
            Texture2D tex;
            if (textures.TryGetValue(index, out tex)) return tex;
            textures[index] = null;
            var texDefs = MediaJson.Arr(root, "textures");
            var images = MediaJson.Arr(root, "images");
            if (texDefs == null || images == null || index >= texDefs.Count) return null;
            int source = MediaJson.Int(texDefs[index], "source");
            if (source < 0 || source >= images.Count) return null;
            var image = images[source];
            byte[] data = null;
            int view = MediaJson.Int(image, "bufferView");
            if (view >= 0) data = View(view);
            else
            {
                string uri = MediaJson.Str(image, "uri");
                if (uri != null) data = Uri(uri);
            }
            if (data == null) return null;
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (!tex.LoadImage(data)) { UnityEngine.Object.Destroy(tex); return null; }
            tex.name = "imported texture " + index;
            textures[index] = tex;
            return tex;
        }

        // ---- buffers and accessors ----------------------------------------------------------------------

        private byte[] Uri(string uri)
        {
            int comma = uri.IndexOf(',');
            if (uri.StartsWith("data:", StringComparison.Ordinal) && comma > 0) return Convert.FromBase64String(uri.Substring(comma + 1));
            string file = Path.Combine(folder ?? "", System.Uri.UnescapeDataString(uri));
            return File.Exists(file) ? File.ReadAllBytes(file) : null;
        }

        private byte[] Buffer(int index)
        {
            byte[] data;
            if (buffers.TryGetValue(index, out data)) return data;
            var defs = MediaJson.Arr(root, "buffers");
            string uri = defs != null && index < defs.Count ? MediaJson.Str(defs[index], "uri") : null;
            data = uri == null ? glbBin : Uri(uri);
            buffers[index] = data;
            return data;
        }

        private byte[] View(int index)
        {
            var views = MediaJson.Arr(root, "bufferViews");
            if (views == null || index >= views.Count) return null;
            var v = views[index];
            byte[] buffer = Buffer(MediaJson.Int(v, "buffer", 0));
            if (buffer == null) return null;
            int offset = MediaJson.Int(v, "byteOffset", 0), length = MediaJson.Int(v, "byteLength", 0);
            var copy = new byte[length];
            System.Buffer.BlockCopy(buffer, offset, copy, 0, length);
            return copy;
        }

        private static int Components(string type)
        {
            switch (type) { case "SCALAR": return 1; case "VEC2": return 2; case "VEC3": return 3; case "VEC4": return 4; case "MAT4": return 16; }
            return 0;
        }

        private static int ComponentSize(int componentType)
        {
            switch (componentType) { case 5120: case 5121: return 1; case 5122: case 5123: return 2; case 5125: case 5126: return 4; }
            return 0;
        }

        // Reads an accessor as doubles (normalized integers are mapped to 0..1 / -1..1).
        private double[] Read(int accessor, out int components)
        {
            components = 0;
            var accessors = MediaJson.Arr(root, "accessors");
            if (accessors == null || accessor >= accessors.Count) return null;
            var a = accessors[accessor];
            int count = MediaJson.Int(a, "count", 0), type = MediaJson.Int(a, "componentType", 5126);
            components = Components(MediaJson.Str(a, "type"));
            int size = ComponentSize(type);
            if (components == 0 || size == 0) return null;
            var result = new double[count * components];
            int viewIndex = MediaJson.Int(a, "bufferView");
            if (viewIndex < 0) return result;       // all zeros (sparse accessors are not supported)
            var views = MediaJson.Arr(root, "bufferViews");
            var view = views[viewIndex];
            byte[] buffer = Buffer(MediaJson.Int(view, "buffer", 0));
            if (buffer == null) return null;
            int start = MediaJson.Int(view, "byteOffset", 0) + MediaJson.Int(a, "byteOffset", 0);
            int stride = MediaJson.Int(view, "byteStride", 0);
            if (stride <= 0) stride = size * components;
            bool normalized = (a as Dictionary<string, object>).ContainsKey("normalized") && (bool)((Dictionary<string, object>)a)["normalized"];
            for (int i = 0; i < count; i++)
                for (int c = 0; c < components; c++)
                {
                    int at = start + i * stride + c * size;
                    double value;
                    switch (type)
                    {
                        case 5126: value = BitConverter.ToSingle(buffer, at); break;
                        case 5125: value = BitConverter.ToUInt32(buffer, at); break;
                        case 5123: value = BitConverter.ToUInt16(buffer, at); if (normalized) value /= 65535.0; break;
                        case 5122: value = BitConverter.ToInt16(buffer, at); if (normalized) value = Math.Max(value / 32767.0, -1); break;
                        case 5121: value = buffer[at]; if (normalized) value /= 255.0; break;
                        default: value = (sbyte)buffer[at]; if (normalized) value = Math.Max(value / 127.0, -1); break;
                    }
                    result[i * components + c] = value;
                }
            return result;
        }

        private float[] ReadFloats(int accessor, out int components)
        {
            double[] d = Read(accessor, out components);
            if (d == null) return null;
            var f = new float[d.Length];
            for (int i = 0; i < d.Length; i++) f[i] = (float)d[i];
            return f;
        }

        private int[] ReadInts(int accessor)
        {
            double[] d = Read(accessor, out int components);
            if (d == null || components != 1) return null;
            var r = new int[d.Length];
            for (int i = 0; i < d.Length; i++) r[i] = (int)d[i];
            return r;
        }
    }
}
