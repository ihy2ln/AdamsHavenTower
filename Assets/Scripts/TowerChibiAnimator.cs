using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // Baked civilian rig motion on a camera-facing mesh in the 3D cutaway.
    // The source rig/plates stay in art/; this component reads compact Unity atlases.
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class TowerChibiAnimator : MonoBehaviour
    {
        private const int Frames = 24;
        private const int Columns = 8;
        private const float Fps = 12;
        private const float AtlasSize = 1024;
        private const float CellWidth = 128, CellHeight = 192;
        private const float PadX = 2, PadY = 3;
        private const float FrameWidth = 124, FrameHeight = 186;
        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<Texture2D, Material> Materials = new Dictionary<Texture2D, Material>();

        private Mesh mesh;
        private MeshRenderer meshRenderer;
        private string unitId;
        private string clip;
        private string restingClip;
        private Vector3 destination;
        private bool moving;
        private bool downed;
        private float clipSeconds;
        private int shownFrame = -1;
        private float playbackSpeed = 1;
        private bool sleeping;

        private void Awake() { EnsureMesh(); }

        private void EnsureMesh()
        {
            if (mesh != null) return;
            mesh = new Mesh { name = "Tower chibi motion quad" };
            mesh.vertices = new[] {
                new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0),
                new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 }; // Face the cutaway camera at -Z.
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = GetComponent<MeshRenderer>();
        }

        public bool Configure(string rosterId, bool working, bool isDowned,
            Vector3 target, Vector3? walkFrom = null)
        {
            EnsureMesh();
            unitId = rosterId;
            if (Load("idle") == null) return false;
            downed = isDowned;
            restingClip = working ? "task" : "idle";
            destination = target;
            moving = !downed && walkFrom.HasValue &&
                Vector3.Distance(walkFrom.Value, target) > 0.04f && Load("walk_in_place") != null;
            transform.localPosition = moving ? walkFrom.Value : target;
            ApplyScale(target.z);
            SelectClip(downed ? "knocked_down" : moving ? "walk_in_place" : restingClip);
            return true;
        }

        public void SetPlaybackSpeed(float value) { playbackSpeed = Mathf.Max(0, value); }

        // Lying on the bed: the idle loop keeps breathing while the quad is turned on its side.
        public void SetSleeping(bool value)
        {
            if (sleeping == value) return;
            sleeping = value;
            transform.localRotation = value ? Quaternion.Euler(0, 0, 84) : Quaternion.identity;
        }

        public void SetPose(Vector3 position, bool traveling, bool working, bool isDowned)
        {
            moving = false;
            downed = isDowned;
            transform.localPosition = sleeping ? position + new Vector3(0, -0.34f, 0.05f) : position;
            ApplyScale(position.z);
            if (sleeping) { traveling = false; working = false; }
            string next = isDowned ? "knocked_down" : traveling ? "walk_in_place" :
                working ? "task" : "idle";
            if (clip != next) SelectClip(next);
        }

        private void ApplyScale(float z)
        {
            float depth = TowerRoomDepth.ScaleFromZ(z);
            transform.localScale = new Vector3((downed ? 1.15f : 0.85f) * depth, 1.30f * depth, 1);
        }

        private Texture2D Load(string name)
        {
            string key = unitId + "/" + name;
            Texture2D texture;
            if (!Textures.TryGetValue(key, out texture))
            {
                texture = Resources.Load<Texture2D>("AdamsHaven/ChibiMotion/" + key);
                Textures[key] = texture;
                if (texture != null) { texture.filterMode = FilterMode.Bilinear; texture.wrapMode = TextureWrapMode.Clamp; }
            }
            return texture;
        }

        private void SelectClip(string name)
        {
            Texture2D texture = Load(name);
            if (texture == null && name != "idle") { name = "idle"; texture = Load(name); }
            if (texture == null) return;
            clip = name;
            clipSeconds = 0;
            shownFrame = -1;
            Material material;
            if (!Materials.TryGetValue(texture, out material))
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Transparent");
                material = new Material(shader) { mainTexture = texture };
                Materials.Add(texture, material);
            }
            meshRenderer.sharedMaterial = material;
            SetFrame(0);
        }

        private void Update()
        {
            if (clip == null || playbackSpeed <= 0) return;
            float dt = Time.deltaTime * playbackSpeed;
            if (moving)
            {
                transform.localPosition = Vector3.MoveTowards(transform.localPosition, destination, 1.55f * dt);
                if (Vector3.Distance(transform.localPosition, destination) <= 0.005f)
                {
                    transform.localPosition = destination;
                    moving = false;
                    SelectClip(restingClip);
                    return;
                }
            }
            clipSeconds += dt;
            int frame = Mathf.FloorToInt(clipSeconds * Fps);
            SetFrame(downed ? Mathf.Min(Frames - 1, frame) : frame % Frames);
        }

        private void SetFrame(int index)
        {
            if (index == shownFrame) return;
            shownFrame = index;
            int column = index % Columns;
            int row = index / Columns;
            float u0 = (column * CellWidth + PadX) / AtlasSize;
            float u1 = (column * CellWidth + PadX + FrameWidth) / AtlasSize;
            float v1 = 1 - (row * CellHeight + PadY) / AtlasSize;
            float v0 = 1 - (row * CellHeight + PadY + FrameHeight) / AtlasSize;
            mesh.uv = new[] {
                new Vector2(u0, v0), new Vector2(u1, v0),
                new Vector2(u0, v1), new Vector2(u1, v1)
            };
        }

        private void OnDestroy()
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }
    }
}
