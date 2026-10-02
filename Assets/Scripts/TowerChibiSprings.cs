using System.Collections.Generic;
using UnityEngine;

// Secondary motion for the Tower 3D chibis, run after the clip is sampled each frame:
//  - jiggle: Breast_L/R and Butt_L/R (added by Tools/build_tower_rig.py) follow a damped spring tip, so the body
//    lags and settles slightly behind fast moves. Kept small on purpose; Strength scales it per character.
//  - chains: Tail1..N and braids (BraidL1..N / BraidR1..N) swing as verlet chains pulled back toward the animated
//    pose, with a slow idle sway on tails.
// Bones point along their local +Y (Blender primary bone axis), which is how the FBX arrives.
[DefaultExecutionOrder(100)]
public sealed class TowerChibiSprings : MonoBehaviour
{
    sealed class Jiggle
    {
        public Transform Bone;
        public Quaternion Rest;
        public Vector3 LocalAxis;
        public float Length, Stiffness, Damping, MaxAngle;
        public Vector3 Tip, Velocity;
        public bool Started;
    }

    sealed class Link
    {
        public Transform Bone;
        public Quaternion Rest;
        public Vector3 LocalAxis;
        public float Length;
        public Vector3 Particle, Previous;
    }

    sealed class Chain
    {
        public string Prefix;
        public readonly List<Link> Links = new List<Link>();
        public float Stiffness, Drag, Gravity, Sway;
        public bool Started;
    }

    public float Strength = 1f;
    readonly List<Jiggle> jiggles = new List<Jiggle>();
    readonly List<Chain> chains = new List<Chain>();
    float height = 1.7f, clock;

    // height: the character's world height, used to size the spring tips.
    public void Setup(float worldHeight, float strength)
    {
        height = Mathf.Max(.01f, worldHeight);
        Strength = strength;
        jiggles.Clear(); chains.Clear();
        var bones = new Dictionary<string, Transform>();
        foreach (var t in GetComponentsInChildren<Transform>(true)) bones[t.name] = t;
        // jiggle bones point straight out of the body: forward for the chest, backward for the hips.
        // The FBX character faces its root's +Z (TowerChibi3D turns the root to face the camera).
        var front = transform.forward;
        foreach (var name in new[] { "Breast_L", "Breast_R" })
            if (bones.TryGetValue(name, out var b))
                jiggles.Add(new Jiggle { Bone = b, Rest = b.localRotation, LocalAxis = b.InverseTransformDirection(front).normalized,
                                         Length = .05f * height, Stiffness = 260f, Damping = 15f, MaxAngle = 13f });
        foreach (var name in new[] { "Butt_L", "Butt_R" })
            if (bones.TryGetValue(name, out var b))
                jiggles.Add(new Jiggle { Bone = b, Rest = b.localRotation, LocalAxis = b.InverseTransformDirection(-front).normalized,
                                         Length = .055f * height, Stiffness = 300f, Damping = 17f, MaxAngle = 10f });
        AddChain(bones, "Tail", .045f, .14f, .25f, .035f);
        // braids rest where the A-pose arm held them out; let gravity win so they settle into a hang
        AddChain(bones, "BraidL", .012f, .22f, 3f, 0f);
        AddChain(bones, "BraidR", .012f, .22f, 3f, 0f);
    }

    void AddChain(Dictionary<string, Transform> bones, string prefix, float stiffness, float drag, float gravity, float sway)
    {
        var chain = new Chain { Prefix = prefix, Stiffness = stiffness, Drag = drag, Gravity = gravity, Sway = sway };
        Vector3 lastWorldAxis = Vector3.down;
        for (int i = 1; bones.TryGetValue(prefix + i, out var t); i++)
        {
            bones.TryGetValue(prefix + (i + 1), out var next);
            float len = next ? Vector3.Distance(t.position, next.position)
                             : (chain.Links.Count > 0 ? chain.Links[chain.Links.Count - 1].Length : .06f * height);
            // the bone's own axis, from its rest child (the last link continues its parent's direction)
            Vector3 worldAxis = next ? (next.position - t.position).normalized : lastWorldAxis;
            lastWorldAxis = worldAxis;
            chain.Links.Add(new Link { Bone = t, Rest = t.localRotation, LocalAxis = t.InverseTransformDirection(worldAxis).normalized,
                                       Length = Mathf.Max(.001f, len) });
        }
        if (chain.Links.Count > 0) chains.Add(chain);
    }

    public bool HasTail => chains.Exists(c => c.Prefix == "Tail");
    public int ChainCount => chains.Count;
    public int JiggleCount => jiggles.Count;

    // Call after the pose is sampled when stepping manually (editor checks); LateUpdate does it in play.
    public void Step(float dt)
    {
        if (dt <= 0f) return;
        dt = Mathf.Min(dt, 1f / 20f);
        clock += dt;
        // clips never key the spring bones: start every step from their rest pose, or the offsets would accumulate
        foreach (var j in jiggles) if (j.Bone) j.Bone.localRotation = j.Rest;
        foreach (var c in chains) foreach (var l in c.Links) if (l.Bone) l.Bone.localRotation = l.Rest;
        foreach (var j in jiggles) StepJiggle(j, dt);
        foreach (var c in chains) StepChain(c, dt);
    }

    public void ResetState()
    {
        foreach (var j in jiggles) j.Started = false;
        foreach (var c in chains) c.Started = false;
    }

    void LateUpdate() { Step(Time.deltaTime); }

    void StepJiggle(Jiggle j, float dt)
    {
        if (!j.Bone || Strength <= 0f) return;
        Vector3 root = j.Bone.position, axis = j.Bone.TransformDirection(j.LocalAxis);
        Vector3 target = root + axis * j.Length;
        if (!j.Started) { j.Tip = target; j.Velocity = Vector3.zero; j.Started = true; }
        // a few substeps keep the stiff spring stable at low frame rates
        int steps = Mathf.CeilToInt(dt / (1f / 120f));
        float h = dt / steps;
        for (int s = 0; s < steps; s++)
        {
            Vector3 accel = (target - j.Tip) * j.Stiffness - j.Velocity * j.Damping;
            j.Velocity += accel * h;
            j.Tip += j.Velocity * h;
        }
        Vector3 dir = (j.Tip - root).normalized;
        float limit = j.MaxAngle * Mathf.Clamp(Strength, 0f, 2f);
        float angle = Vector3.Angle(axis, dir);
        if (angle > limit) dir = Vector3.Slerp(axis, dir, limit / angle);
        j.Tip = root + dir * j.Length;
        j.Bone.rotation = Quaternion.FromToRotation(axis, Vector3.Lerp(axis, dir, Mathf.Clamp01(Strength))) * j.Bone.rotation;
    }

    void StepChain(Chain c, float dt)
    {
        var links = c.Links;
        // animated chain positions (tips of each link) before simulation
        var animated = new Vector3[links.Count];
        for (int i = 0; i < links.Count; i++) animated[i] = links[i].Bone.position + links[i].Bone.TransformDirection(links[i].LocalAxis) * links[i].Length;
        if (!c.Started)
        {
            for (int i = 0; i < links.Count; i++) links[i].Particle = links[i].Previous = animated[i];
            c.Started = true;
        }
        Vector3 side = links[0].Bone.parent ? links[0].Bone.parent.right : Vector3.right;
        float frame = dt * 60f;
        for (int i = 0; i < links.Count; i++)
        {
            var link = links[i];
            Vector3 head = i == 0 ? link.Bone.position : links[i - 1].Particle;
            float u = (i + 1f) / links.Count;
            Vector3 goal = animated[i] + side * Mathf.Sin(clock * 2f * Mathf.PI / 1.7f - i * .7f) * c.Sway * height * u;
            Vector3 velocity = (link.Particle - link.Previous) * (1f - c.Drag);
            link.Previous = link.Particle;
            Vector3 p = link.Particle + velocity + Vector3.down * c.Gravity * 9.8f * height * dt * dt;
            p += (goal - p) * Mathf.Clamp01(c.Stiffness * frame * (1.5f - u * .8f));
            p = head + (p - head).normalized * link.Length;
            link.Particle = p;
        }
        // rotate each bone so its tip lands on its particle (parents first)
        for (int i = 0; i < links.Count; i++)
        {
            var bone = links[i].Bone;
            Vector3 wanted = links[i].Particle - bone.position;
            if (wanted.sqrMagnitude > 1e-10f)
                bone.rotation = Quaternion.FromToRotation(bone.TransformDirection(links[i].LocalAxis), wanted) * bone.rotation;
        }
    }
}
