using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // Renders the layered expedition map (TowerOverworld) with its own camera into a RenderTexture that the
    // expedition UI shows in a RawImage. Layers, back to front: splat ground with worn roads, scattered
    // canopy/undergrowth/rocks and place props (y-sorted with the party), preview marks, drifting fog.
    // World space: one unit per cell; cell (x, y) has its centre at (x + 0.5, height - y - 0.5) from the root.
    public sealed class TowerMapView : MonoBehaviour
    {
        private static readonly Vector3 Origin = new Vector3(20000, 20000, 0);
        private const int Px = 4;                   // mask pixels per cell
        private const float WalkSpeed = 4.2f, FollowGap = 0.95f;

        private TowerRules rules;
        private TowerOverworld map;
        private string mapKey = "";
        private Transform root;
        private Camera cam;
        private RenderTexture rt;
        private Material groundMat, fogMat, stampMat;
        private Texture2D w0, w1, w2, roadTex, seenTex, atlas;
        private readonly List<GameObject> built = new List<GameObject>();
        private readonly List<SpriteRenderer> figures = new List<SpriteRenderer>(), shadows = new List<SpriteRenderer>();
        private readonly Dictionary<string, SpriteRenderer> props = new Dictionary<string, SpriteRenderer>();
        private readonly Dictionary<string, SpriteRenderer> tells = new Dictionary<string, SpriteRenderer>();
        private readonly List<SpriteRenderer> marks = new List<SpriteRenderer>();
        private float[] seenCells, roadCells;       // what is drawn (may lag the rules while walking)
        private static Sprite dot, glow;

        // walking
        private List<Vector2> walkPts;
        private List<int> walkCellAt;               // index into walkCells reached at each point
        private List<Vector2Int> walkCells;
        private float walkDist, walkLen;
        private int walkCellsDone;
        private Action walkDone;
        public bool Walking { get { return walkPts != null; } }

        public RenderTexture Texture { get { return rt; } }
        public TowerOverworld Map { get { return map; } }

        // ---------------------------------------------------------------- setup

        public void Bind(TowerRules r)
        {
            rules = r;
            var run = r.Run;
            var m = r.Overworld;
            if (m == null) return;
            string key = run.biome + ":" + run.gridSeed + ":" + run.shift;
            if (key != mapKey || map == null) { Clear(); map = m; mapKey = key; Build(); }
            Sync();
        }

        public void SetActive(bool on) { if (root != null) root.gameObject.SetActive(on); }

        public void Resize(int width, int height)
        {
            width = Mathf.Clamp(width, 64, 2048); height = Mathf.Clamp(height, 64, 2048);
            if (rt != null && rt.width == width && rt.height == height) return;
            if (rt != null) { cam.targetTexture = null; rt.Release(); Destroy(rt); }
            rt = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32) { name = "Expedition map" };
            rt.Create();
            if (cam != null) { cam.targetTexture = rt; cam.aspect = width / (float)height; Clamp(); }
        }

        private void OnDestroy()
        {
            Clear();
            if (rt != null) { rt.Release(); Destroy(rt); }
        }

        private void Clear()
        {
            foreach (var go in built) if (go != null) Destroy(go);
            built.Clear(); figures.Clear(); shadows.Clear(); props.Clear(); tells.Clear(); marks.Clear();
            foreach (var t in new[] { w0, w1, w2, roadTex, seenTex, atlas }) if (t != null) Destroy(t);
            if (root != null) Destroy(root.gameObject);
            root = null; walkPts = null; map = null;
        }

        private void Build()
        {
            root = new GameObject("Expedition map world").transform;
            root.position = Origin;
            var camGo = new GameObject("Expedition map camera");
            camGo.transform.SetParent(root, false);
            cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.06f, 0.08f);
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 50;
            cam.orthographicSize = 8.5f;
            cam.transform.localPosition = new Vector3(map.width / 2f, map.height / 2f, -10);
            if (rt != null) { cam.targetTexture = rt; cam.aspect = rt.width / (float)rt.height; }
            else Resize(1024, 640);
            cam.depth = -50;

            BuildGround();
            BuildStamps();
            BuildProps();
            BuildParty();
            BuildFog();
            seenCells = null; roadCells = null;
        }

        // ---------------------------------------------------------------- ground

        private void BuildGround()
        {
            int W = map.width * Px, H = map.height * Px;
            var weights = new float[TowerMapArt.Grounds.Length][];
            for (int g = 0; g < weights.Length; g++) weights[g] = new float[W * H];
            for (int py = 0; py < H; py++)
                for (int px = 0; px < W; px++)
                {
                    int cx = px / Px, cy = map.height - 1 - py / Px;    // texture rows run bottom to top
                    weights[TowerMapArt.GroundOf(map.At(cx, cy))][py * W + px] = 1;
                }
            for (int g = 0; g < weights.Length; g++) { Blur(weights[g], W, H, 3); Blur(weights[g], W, H, 2); }
            w0 = Pack(weights, 0, W, H); w1 = Pack(weights, 4, W, H); w2 = Pack(weights, 8, W, H);
            roadTex = MaskTexture(W, H); seenTex = MaskTexture(W, H);

            groundMat = new Material(Shader.Find("AdamsHaven/MapGround"));
            groundMat.SetTexture("_W0", w0); groundMat.SetTexture("_W1", w1); groundMat.SetTexture("_W2", w2);
            groundMat.SetTexture("_Road", roadTex);
            for (int g = 0; g < TowerMapArt.Grounds.Length; g++) groundMat.SetTexture("_G" + g, TowerMapArt.Ground(g));
            groundMat.SetVector("_Cells", new Vector4(map.width, map.height, 6, 0));
            var quad = Quad("Ground", groundMat, -100);
            quad.transform.localPosition = new Vector3(map.width / 2f, map.height / 2f, 1);
            quad.transform.localScale = new Vector3(map.width, map.height, 1);
        }

        private static Texture2D Pack(float[][] weights, int first, int W, int H)
        {
            var px = new Color32[W * H];
            for (int i = 0; i < px.Length; i++)
            {
                float total = 0;
                for (int g = 0; g < weights.Length; g++) total += weights[g][i];
                total = Mathf.Max(total, 1e-4f);
                byte Ch(int g) { return g < weights.Length ? (byte)Mathf.Clamp(weights[g][i] / total * 255f, 0, 255) : (byte)0; }
                px[i] = new Color32(Ch(first), Ch(first + 1), Ch(first + 2), Ch(first + 3));
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(px); tex.Apply();
            return tex;
        }

        private static Texture2D MaskTexture(int W, int H)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels32(new Color32[W * H]); tex.Apply();
            return tex;
        }

        // Separable box blur, in place.
        private static void Blur(float[] a, int W, int H, int r)
        {
            var tmp = new float[a.Length];
            for (int y = 0; y < H; y++)
            {
                float sum = 0; int row = y * W;
                for (int x = -r; x <= r; x++) sum += a[row + Mathf.Clamp(x, 0, W - 1)];
                for (int x = 0; x < W; x++)
                {
                    tmp[row + x] = sum / (2 * r + 1);
                    sum += a[row + Mathf.Min(W - 1, x + r + 1)] - a[row + Mathf.Max(0, x - r)];
                }
            }
            for (int x = 0; x < W; x++)
            {
                float sum = 0;
                for (int y = -r; y <= r; y++) sum += tmp[Mathf.Clamp(y, 0, H - 1) * W + x];
                for (int y = 0; y < H; y++)
                {
                    a[y * W + x] = sum / (2 * r + 1);
                    sum += tmp[Mathf.Min(H - 1, y + r + 1) * W + x] - tmp[Mathf.Max(0, y - r) * W + x];
                }
            }
        }

        // Per-cell values (0..1) -> soft mask texture.
        private void UploadMask(Texture2D tex, float[] cells, int blur)
        {
            int W = map.width * Px, H = map.height * Px;
            var a = new float[W * H];
            for (int py = 0; py < H; py++)
                for (int px = 0; px < W; px++)
                    a[py * W + px] = cells[map.Index(px / Px, map.height - 1 - py / Px)];
            Blur(a, W, H, blur); Blur(a, W, H, blur);
            var c = new Color32[W * H];
            for (int i = 0; i < c.Length; i++) { byte v = (byte)Mathf.Clamp(a[i] * 255f, 0, 255); c[i] = new Color32(v, v, v, 255); }
            tex.SetPixels32(c); tex.Apply();
        }

        // ---------------------------------------------------------------- stamps

        private struct Stamp { public int tex; public Vector2 foot; public float w; public Color tint; public bool fade, cuttable; }

        private static string[] Canopy(string biome)
        {
            switch (biome)
            {
                case "lakes": return new[] { "canopy_willow", "canopy_oak", "canopy_pine", "canopy_willow" };
                case "mountains": return new[] { "canopy_pine", "canopy_pine", "canopy_pine", "canopy_birch" };
                case "ruins": return new[] { "canopy_oak", "canopy_silverwood", "canopy_pine", "canopy_oak" };
                case "heart": return new[] { "canopy_silverwood", "canopy_silverwood", "canopy_oak", "canopy_dead" };
                default: return new[] { "canopy_oak", "canopy_pine", "canopy_birch", "canopy_oak" };
            }
        }

        private void BuildStamps()
        {
            // One atlas for every stamp family this map uses.
            var families = new List<string>(Canopy(map.biome));
            foreach (var f in new[] { "canopy_dead", "bush", "rock", "reed", "canopy_willow", "canopy_pine" }) families.Add(f);
            var famTex = new Dictionary<string, List<int>>();
            var all = new List<Texture2D>();
            foreach (var f in families)
            {
                if (famTex.ContainsKey(f)) continue;
                var ids = new List<int>();
                foreach (var t in TowerMapArt.Stamps(f)) { ids.Add(all.Count); all.Add(t); }
                famTex[f] = ids;
            }
            atlas = new Texture2D(8, 8, TextureFormat.RGBA32, true);
            var rects = atlas.PackTextures(all.ToArray(), 2, 4096, false);
            atlas.wrapMode = TextureWrapMode.Clamp;
            stampMat = new Material(Shader.Find("AdamsHaven/MapStamp"));
            stampMat.mainTexture = atlas;
            stampMat.SetTexture("_Road", roadTex);
            stampMat.SetVector("_Map", new Vector4(Origin.x, Origin.y, map.width, map.height));

            var rng = new TowerRng(map.seed * 31u + 5u);
            var canopy = Canopy(map.biome);
            var rows = new List<Stamp>[map.height];
            for (int i = 0; i < rows.Length; i++) rows[i] = new List<Stamp>();
            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                {
                    var t = map.At(x, y);
                    bool nearPlace = false;
                    foreach (var p in map.pois) if ((p.x - x) * (p.x - x) + (p.y - y) * (p.y - y) <= 2) { nearPlace = true; break; }
                    if (nearPlace || t == TowerTerrain.Road || t == TowerTerrain.Bridge || t == TowerTerrain.Water || t == TowerTerrain.Ford) continue;
                    int tries = t == TowerTerrain.DenseForest ? 2 : 1;
                    for (int k = 0; k < tries; k++)
                    {
                        string fam = null; float size = 1, chance = 0; bool fade = true;
                        switch (t)
                        {
                            case TowerTerrain.DenseForest: fam = canopy[rng.Next(canopy.Length)]; size = 2.1f; chance = k == 0 ? 0.95f : 0.35f; break;
                            case TowerTerrain.Forest:
                                if (rng.Value() < 0.2f) { fam = "bush"; size = 0.9f; chance = 0.6f; fade = false; }
                                else { fam = canopy[rng.Next(canopy.Length)]; size = 2.2f; chance = 0.78f; }
                                break;
                            case TowerTerrain.Clearing: fam = "bush"; size = 0.8f; chance = 0.05f; fade = false; break;
                            case TowerTerrain.Hill:
                                if (rng.Value() < 0.5f) { fam = "rock"; size = 1.0f; chance = 0.2f; fade = false; }
                                else { fam = "canopy_pine"; size = 1.8f; chance = 0.2f; }
                                break;
                            case TowerTerrain.Rock: fam = "rock"; size = 1.6f; chance = 0.85f; fade = false; break;
                            case TowerTerrain.Marsh:
                                if (rng.Value() < 0.7f) { fam = "reed"; size = 0.9f; chance = 0.45f; fade = false; }
                                else { fam = "canopy_willow"; size = 2f; chance = 0.3f; }
                                break;
                            case TowerTerrain.Blight: fam = "canopy_dead"; size = 1.8f; chance = 0.3f; break;
                            case TowerTerrain.RuinGround: fam = rng.Value() < 0.5f ? "rock" : canopy[0]; size = 1.2f; chance = 0.12f; fade = fam != "rock"; break;
                        }
                        if (fam == null || rng.Value() >= chance) continue;
                        var ids = famTex[fam];
                        float shade = t == TowerTerrain.DenseForest ? rng.Range(0.78f, 0.92f) : rng.Range(0.9f, 1.05f);
                        var foot = Cell(x, y) + new Vector2(rng.Range(-0.4f, 0.4f), rng.Range(-0.4f, 0.4f));
                        rows[y].Add(new Stamp { tex = ids[rng.Next(ids.Count)], foot = foot, w = size * rng.Range(0.85f, 1.2f),
                            tint = new Color(shade, shade, shade * rng.Range(0.97f, 1.03f), 1), fade = fade, cuttable = fam != "rock" });
                    }
                }
            // One mesh per grid row, sorted north to south so the party can stand between rows.
            for (int y = 0; y < map.height; y++)
            {
                if (rows[y].Count == 0) continue;
                rows[y].Sort((a, b) => b.foot.y.CompareTo(a.foot.y));
                var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var feet = new List<Vector2>(); var flags = new List<Vector2>();
                var cols = new List<Color>(); var tris = new List<int>();
                foreach (var s in rows[y])
                {
                    var r = rects[s.tex]; var src = all[s.tex];
                    float h = s.w * src.height / (float)src.width;
                    float x0 = s.foot.x - s.w / 2, y0 = s.foot.y - h * 0.05f;
                    int v = verts.Count;
                    verts.Add(new Vector3(x0, y0)); verts.Add(new Vector3(x0 + s.w, y0)); verts.Add(new Vector3(x0, y0 + h)); verts.Add(new Vector3(x0 + s.w, y0 + h));
                    uvs.Add(new Vector2(r.xMin, r.yMin)); uvs.Add(new Vector2(r.xMax, r.yMin)); uvs.Add(new Vector2(r.xMin, r.yMax)); uvs.Add(new Vector2(r.xMax, r.yMax));
                    for (int k = 0; k < 4; k++) { feet.Add(s.foot); flags.Add(new Vector2(s.fade ? 1 : 0, s.cuttable ? 1 : 0)); cols.Add(s.tint); }
                    tris.Add(v); tris.Add(v + 2); tris.Add(v + 1); tris.Add(v + 1); tris.Add(v + 2); tris.Add(v + 3);
                }
                var mesh = new Mesh { name = "Stamps row " + y };
                mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetUVs(1, feet); mesh.SetUVs(2, flags); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                var go = new GameObject("Stamps row " + y, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = stampMat; mr.sortingOrder = Order(y);
                built.Add(go);
            }
        }

        private static int Order(int gridRow) { return gridRow * 2; }

        // ---------------------------------------------------------------- props, party, fog, marks

        private void BuildProps()
        {
            foreach (var p in map.pois)
            {
                var tex = TowerMapArt.Prop(p.kind);
                if (tex == null) continue;
                float width = p.kind == "camp" || p.kind == "lair" ? 2.6f : 2.2f;
                var sr = SpriteAt("Place " + p.id, SpriteOf(tex, 0.08f), Cell(p.x, p.y) + new Vector2(0, -0.25f), width / (tex.width / 100f), Order(p.y) + 1);
                props[p.id] = sr;
                var tell = SpriteAt("Tell " + p.id, Glow, Cell(p.x, p.y), 2.6f, 900);
                tell.color = p.kind == "lair" ? new Color(1f, 0.35f, 0.4f, 0.55f) : p.kind == "camp" ? new Color(1f, 0.8f, 0.45f, 0.6f) : new Color(0.75f, 0.85f, 1f, 0.45f);
                tells[p.id] = tell;
            }
        }

        private void BuildParty()
        {
            var run = rules.Run;
            int shown = 0;
            for (int i = 0; i < run.party.Count && shown < 3; i++)
            {
                var tex = Resources.Load<Texture2D>("AdamsHaven/Chibi/" + run.party[i]);
                if (tex == null) continue;
                var shadow = SpriteAt("Party shadow", Dot, Vector2.zero, 0.9f, 0);
                shadow.color = new Color(0, 0, 0, 0.35f);
                shadow.transform.localScale = new Vector3(0.9f, 0.32f, 1);
                float h = shown == 0 ? 1.55f : 1.3f;
                var fig = SpriteAt("Party " + run.party[i], SpriteOf(tex, 0.02f), Vector2.zero, h / (tex.height / 100f), 0);
                figures.Add(fig); shadows.Add(shadow);
                shown++;
            }
        }

        private void BuildFog()
        {
            fogMat = new Material(Shader.Find("AdamsHaven/MapFog"));
            fogMat.SetTexture("_Seen", seenTex);
            fogMat.SetVector("_Cells", new Vector4(map.width, map.height, 0, 0));
            var cloud = Resources.Load<Texture2D>(TowerMapArt.Root + "fx/cloud_tile");
            if (cloud != null) { fogMat.SetTexture("_Cloud", cloud); fogMat.SetFloat("_UseCloud", 1); }
            var quad = Quad("Fog", fogMat, 800);
            quad.transform.localPosition = new Vector3(map.width / 2f, map.height / 2f, -1);
            quad.transform.localScale = new Vector3(map.width + 8, map.height + 8, 1);
        }

        private GameObject Quad(string name, Material mat, int order)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat; mr.sortingOrder = order;
            built.Add(go);
            return go;
        }

        private SpriteRenderer SpriteAt(string name, Sprite sprite, Vector2 at, float scale, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = at;
            go.transform.localScale = new Vector3(scale, scale, 1);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.sortingOrder = order;
            built.Add(go);
            return sr;
        }

        private static readonly Dictionary<Texture2D, Sprite> spriteCache = new Dictionary<Texture2D, Sprite>();
        private static Sprite SpriteOf(Texture2D tex, float pivotY)
        {
            Sprite s;
            if (spriteCache.TryGetValue(tex, out s) && s != null) return s;
            s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, pivotY), 100);
            spriteCache[tex] = s;
            return s;
        }

        private static Sprite Dot { get { if (dot == null) dot = Soft(64, 0.15f, "Map dot"); return dot; } }
        private static Sprite Glow { get { if (glow == null) glow = Soft(128, 1f, "Map glow"); return glow; } }

        private static Sprite Soft(int size, float hard, string name)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = name };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01((1 - d) / hard) * (hard >= 1 ? (1 - d) : 1));
                }
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        public Vector2 Cell(int x, int y) { return new Vector2(x + 0.5f, map.height - y - 0.5f); }

        // ---------------------------------------------------------------- state

        // Redraw fog, worn road, places and the party from the rules.
        public void Sync()
        {
            if (map == null) return;
            var run = rules.Run;
            int n = map.cells.Length;
            if (seenCells == null || seenCells.Length != n) { seenCells = new float[n]; roadCells = new float[n]; }
            for (int i = 0; i < n; i++)
            {
                seenCells[i] = run.gridFog.Length == n && run.gridFog[i] != '0' ? 1 : 0;
                roadCells[i] = RoadValue(run.gridRoad.Length == n ? run.gridRoad[i] - '0' : 0);
            }
            UploadMask(seenTex, seenCells, 3);
            UploadMask(roadTex, roadCells, 1);
            foreach (var p in map.pois)
            {
                bool seen = rules.CellSeen(p.x, p.y);
                if (props.ContainsKey(p.id))
                {
                    props[p.id].gameObject.SetActive(seen);
                    props[p.id].color = run.cleared.Contains(p.id) ? new Color(0.72f, 0.8f, 0.72f) : Color.white;
                }
                // Places learned of but not yet seen glow through the fog.
                if (tells.ContainsKey(p.id)) tells[p.id].gameObject.SetActive(!seen && rules.NodeVisible(p.id));
            }
            if (!Walking) PlaceParty(Cell(run.cx, run.cy), run.cy, Vector2.right, 0, false);
        }

        private static float RoadValue(int strength)
        {
            if (strength <= 0) return 0;
            return strength >= TowerRules.RoadWalked ? 0.55f + 0.45f * (strength - TowerRules.RoadWalked) / (TowerRules.RoadMax - TowerRules.RoadWalked) : 0.25f * strength / TowerRules.RoadWalked;
        }

        private void PlaceParty(Vector2 lead, int row, Vector2 dir, float lead01, bool moving)
        {
            for (int i = 0; i < figures.Count; i++)
            {
                Vector2 pos; Vector2 d = dir;
                if (moving && walkPts != null) pos = PointAlong(walkDist - i * FollowGap, out d);
                else pos = lead + new Vector2(-0.45f * i, 0.18f * i);
                float bob = moving && walkDist - i * FollowGap > 0 && walkDist - i * FollowGap < walkLen ? Mathf.Abs(Mathf.Sin((walkDist - i * FollowGap) * 6f)) * 0.08f : 0;
                figures[i].transform.localPosition = new Vector3(pos.x, pos.y - 0.2f + bob, 0);
                shadows[i].transform.localPosition = new Vector3(pos.x, pos.y - 0.2f, 0);
                int r = Mathf.Clamp(map.height - 1 - Mathf.FloorToInt(pos.y - 0.2f), 0, map.height - 1);
                figures[i].sortingOrder = Order(r) + 1; shadows[i].sortingOrder = Order(r) + 1;
                var s = figures[i].transform.localScale;
                if (Mathf.Abs(d.x) > 0.25f) s.x = Mathf.Abs(s.x) * (d.x < 0 ? -1 : 1);
                figures[i].transform.localScale = s;
            }
            if (figures.Count > 0) stampMat.SetVector("_Party", new Vector4(figures[0].transform.localPosition.x + Origin.x, figures[0].transform.localPosition.y + Origin.y, 1.3f, 3.2f));
        }

        // ---------------------------------------------------------------- preview

        public void ShowPreview(List<Vector2Int> path)
        {
            ClearPreview();
            if (path == null || path.Count < 2) return;
            for (int i = 1; i < path.Count; i++)
            {
                bool end = i == path.Count - 1;
                var m = SpriteAt("Route mark", end ? Glow : Dot, Cell(path[i].x, path[i].y), end ? 1.6f : 0.26f, 790);
                m.color = end ? new Color(1f, 0.9f, 0.6f, 0.75f) : new Color(1f, 0.92f, 0.7f, 0.8f);
                marks.Add(m);
            }
            var t = Cell(path[path.Count - 1].x, path[path.Count - 1].y);
            stampMat.SetVector("_Target", new Vector4(t.x + Origin.x, t.y + Origin.y, 0.8f, 2.2f));
        }

        public void ClearPreview()
        {
            foreach (var m in marks) if (m != null) { built.Remove(m.gameObject); Destroy(m.gameObject); }
            marks.Clear();
            if (stampMat != null) stampMat.SetVector("_Target", new Vector4(-999, -999, 0.8f, 2.2f));
        }

        // ---------------------------------------------------------------- walking

        // Animates the party along cells the rules already walked, lifting fog and wearing road as they pass.
        public void Walk(List<Vector2Int> cells, Action done)
        {
            ClearPreview();
            if (cells == null || cells.Count < 2) { done?.Invoke(); return; }
            walkCells = cells;
            var pts = new List<Vector2>(); walkCellAt = new List<int>();
            for (int i = 0; i < cells.Count; i++) { pts.Add(Cell(cells[i].x, cells[i].y)); walkCellAt.Add(i); }
            // Round the corners (Chaikin), keeping track of which cell each point belongs to.
            for (int pass = 0; pass < 2; pass++)
            {
                var np = new List<Vector2> { pts[0] }; var nc = new List<int> { walkCellAt[0] };
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    np.Add(Vector2.Lerp(pts[i], pts[i + 1], 0.25f)); nc.Add(walkCellAt[i]);
                    np.Add(Vector2.Lerp(pts[i], pts[i + 1], 0.75f)); nc.Add(walkCellAt[i + 1]);
                }
                np.Add(pts[pts.Count - 1]); nc.Add(walkCellAt[walkCellAt.Count - 1]);
                pts = np; walkCellAt = nc;
            }
            walkPts = pts;
            walkLen = 0;
            for (int i = 1; i < pts.Count; i++) walkLen += Vector2.Distance(pts[i - 1], pts[i]);
            walkDist = 0; walkCellsDone = 0; walkDone = done;
        }

        private Vector2 PointAlong(float d, out Vector2 dir)
        {
            d = Mathf.Clamp(d, 0, walkLen);
            float run = 0;
            for (int i = 1; i < walkPts.Count; i++)
            {
                float seg = Vector2.Distance(walkPts[i - 1], walkPts[i]);
                if (run + seg >= d || i == walkPts.Count - 1)
                {
                    dir = seg > 1e-4f ? (walkPts[i] - walkPts[i - 1]) / seg : Vector2.right;
                    return Vector2.Lerp(walkPts[i - 1], walkPts[i], seg > 1e-4f ? Mathf.Clamp01((d - run) / seg) : 0);
                }
                run += seg;
            }
            dir = Vector2.right;
            return walkPts[walkPts.Count - 1];
        }

        private int CellIndexAt(float d)
        {
            float run = 0;
            for (int i = 1; i < walkPts.Count; i++)
            {
                run += Vector2.Distance(walkPts[i - 1], walkPts[i]);
                if (run >= d) return walkCellAt[i];
            }
            return walkCells.Count - 1;
        }

        private void Update()
        {
            if (map == null || cam == null) return;
            if (Walking)
            {
                float terrain = TowerOverworld.TerrainCost(map.At(walkCells[Mathf.Min(walkCellsDone, walkCells.Count - 1)].x, walkCells[Mathf.Min(walkCellsDone, walkCells.Count - 1)].y));
                float pace = terrain >= 3 ? 0.6f : terrain >= 2 ? 0.8f : 1f;
                walkDist += WalkSpeed * pace * Time.unscaledDeltaTime;
                // Lift fog and wear road as each cell is reached.
                int reached = CellIndexAt(walkDist);
                bool changed = false;
                while (walkCellsDone < reached)
                {
                    walkCellsDone++;
                    var c = walkCells[walkCellsDone];
                    RevealLocal(c.x, c.y);
                    int i = map.Index(c.x, c.y);
                    roadCells[i] = Mathf.Max(roadCells[i], RoadValue(rules.RoadStrength(c.x, c.y)));
                    changed = true;
                }
                if (changed) { UploadMask(seenTex, seenCells, 3); UploadMask(roadTex, roadCells, 1); }
                Vector2 dir;
                var lead = PointAlong(walkDist, out dir);
                PlaceParty(lead, 0, dir, 0, true);
                Follow(lead);
                if (walkDist >= walkLen + FollowGap * figures.Count)
                {
                    walkPts = null;
                    var done = walkDone; walkDone = null;
                    Sync();
                    done?.Invoke();
                }
            }
        }

        private void RevealLocal(int cx, int cy)
        {
            // Mirror of the rules' sight circle, so the fog lifts in step with the walk.
            int r = 5;
            for (int y = cy - r - 2; y <= cy + r + 2; y++)
                for (int x = cx - r - 2; x <= cx + r + 2; x++)
                    if (map.Inside(x, y) && rules.CellSeen(x, y) && (x - cx) * (x - cx) + (y - cy) * (y - cy) <= (r + 2) * (r + 2))
                        seenCells[map.Index(x, y)] = 1;
            foreach (var p in map.pois)
                if (props.ContainsKey(p.id) && seenCells[map.Index(p.x, p.y)] > 0) props[p.id].gameObject.SetActive(true);
        }

        // ---------------------------------------------------------------- camera

        public void CenterOnParty()
        {
            if (map == null) return;
            var run = rules.Run;
            var c = Cell(run.cx, run.cy);
            cam.transform.localPosition = new Vector3(c.x, c.y + 1, -10);
            Clamp();
        }

        private void Follow(Vector2 lead)
        {
            var p = cam.transform.localPosition;
            float halfH = cam.orthographicSize * 0.45f, halfW = halfH * cam.aspect;
            var target = new Vector3(Mathf.Clamp(p.x, lead.x - halfW, lead.x + halfW), Mathf.Clamp(p.y, lead.y - halfH, lead.y + halfH), p.z);
            cam.transform.localPosition = Vector3.Lerp(p, target, 1 - Mathf.Exp(-6 * Time.unscaledDeltaTime));
            Clamp();
        }

        public void Pan(Vector2 viewportDelta)
        {
            float h = cam.orthographicSize * 2, w = h * cam.aspect;
            cam.transform.localPosition -= new Vector3(viewportDelta.x * w, viewportDelta.y * h, 0);
            Clamp();
        }

        // Zoom around a viewport point (0..1).
        public void ZoomBy(float factor, Vector2 viewport)
        {
            var before = ViewportToMap(viewport);
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize / factor, 4.5f, Mathf.Max(6f, map.height * 0.62f));
            var after = ViewportToMap(viewport);
            cam.transform.localPosition += (Vector3)(before - after);
            Clamp();
        }

        private void Clamp()
        {
            if (cam == null || map == null) return;
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            var p = cam.transform.localPosition;
            float minX = halfW - 1, maxX = map.width - halfW + 1, minY = halfH - 1, maxY = map.height - halfH + 1;
            p.x = minX > maxX ? map.width / 2f : Mathf.Clamp(p.x, minX, maxX);
            p.y = minY > maxY ? map.height / 2f : Mathf.Clamp(p.y, minY, maxY);
            cam.transform.localPosition = p;
        }

        public Vector2 ViewportToMap(Vector2 viewport)
        {
            var w = cam.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, 10));
            return new Vector2(w.x - Origin.x, w.y - Origin.y);
        }

        public Vector2Int CellAt(Vector2 viewport)
        {
            var m = ViewportToMap(viewport);
            return new Vector2Int(Mathf.FloorToInt(m.x), map.height - 1 - Mathf.FloorToInt(m.y));
        }

        // Where a cell sits in the viewport (0..1), for placing UI next to it.
        public Vector2 CellToViewport(int x, int y)
        {
            var c = Cell(x, y);
            var v = cam.WorldToViewportPoint(new Vector3(c.x + Origin.x, c.y + Origin.y, 0));
            return new Vector2(v.x, v.y);
        }
    }
}
