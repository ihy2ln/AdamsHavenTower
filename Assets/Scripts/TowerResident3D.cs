using System;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // A resident shown as its rigged 3D chibi (TowerChibi3D) instead of the baked 2D atlas quad.
    // Same calls as TowerChibiAnimator so the cutaway drives both alike: this transform sits where the
    // quad's centre would (TowerRoomDepth.Project), the model hangs below it with its feet on the floor,
    // and the room it works in picks the Fallout Shelter work loop (TowerChibi3D.RoomClips).
    public sealed class TowerResident3D : MonoBehaviour
    {
        private const float FigureHeight = 1.22f;           // the 1.30 quad's figure, less its headroom
        private const float BodyHalf = 0.65f;               // TowerRoomDepth's half quad height
        // Work loops turn side-on: their stations (anvil, pot, well rope) stand in front of the body and
        // would hide it from the side-on tower camera.
        private const float WalkYaw = 70f, RestYaw = 20f, WorkYaw = 70f, TurnSpeed = 540f;

        public string ModelId { get; private set; }
        private TowerChibi3D chibi;
        private bool sleeping;
        private bool placed;
        private Vector3 lastPosition;
        private float yaw, facing = -1;                     // facing: -1 left, +1 right (last walk direction)
        private float gameSpeed = 1, groundSpeed;           // groundSpeed: smoothed sideways speed in world units/s
        private float strideRate = -1;                      // walk/run playback matched to groundSpeed; -1 = game speed

        // Roster heroes use their own model; Celestium bodies use the one for their chassis. Null keeps the 2D atlas.
        public static string ModelFor(TowerResident resident)
        {
            string id = resident.origin == "body" ? BodyModel(resident.chassisVariant) : resident.unitId;
            return !string.IsNullOrEmpty(id) && Array.IndexOf(TowerChibi3D.Ids, id) >= 0 ? id : null;
        }

        private static string BodyModel(string variant)
        {
            switch (variant)
            {
                case "normal": return "celestium-med";
                case "short": return "celestium-short";
                case "muscle": return "celestium-muscle";
                default: return null;                       // tall and hourglass have no 3D rig yet
            }
        }

        public static TowerResident3D Create(string modelId, string name, Transform parent)
        {
            var go = new GameObject(name + " 3D");
            go.transform.SetParent(parent, false);
            var model = TowerChibi3D.Spawn(modelId, go.transform, FigureHeight);
            if (model == null) { Destroy(go); return null; }
            model.transform.localPosition = new Vector3(0, -BodyHalf, 0);
            var actor = go.AddComponent<TowerResident3D>();
            actor.ModelId = modelId;
            actor.chibi = model;
            return actor;
        }

        public void SetPlaybackSpeed(float value) { gameSpeed = Mathf.Max(0, value); ApplySpeed(); }

        private void ApplySpeed() { chibi.Speed = strideRate >= 0 ? strideRate : gameSpeed; }

        public void SetSleeping(bool value) { sleeping = value; }

        // Shown again after being pooled off-screen: snap instead of turning or swinging springs across the tower.
        public void Reveal()
        {
            placed = false;
            if (chibi.Springs != null) chibi.Springs.ResetState();
        }

        // workRoom is the room type whose work loop plays while working; workers face roomCenterX so their
        // station stays inside the room.
        public void SetPose(Vector3 position, bool traveling, bool working, bool isDowned, string workRoom,
            float roomCenterX = float.NaN)
        {
            float depth = TowerRoomDepth.ScaleFromZ(position.z);
            transform.localPosition = position;
            transform.localScale = Vector3.one * depth;

            bool resting = isDowned || sleeping;
            float dx = placed ? position.x - lastPosition.x : 0;
            float dt = Time.deltaTime;
            if (dt > 0)
                groundSpeed = placed ? Mathf.Lerp(groundSpeed, Mathf.Abs(dx) / dt, 1 - Mathf.Exp(-12 * dt)) : 0;
            if (traveling && !resting && Mathf.Abs(dx) > 0.0005f) facing = Mathf.Sign(dx);
            else if (working && !traveling && !resting && !float.IsNaN(roomCenterX) &&
                Mathf.Abs(roomCenterX - position.x) > 0.05f) facing = Mathf.Sign(roomCenterX - position.x);
            float target = resting ? 0 : facing * -(traveling ? WalkYaw : working ? WorkYaw : RestYaw);
            yaw = placed ? Mathf.MoveTowardsAngle(yaw, target, TurnSpeed * Time.deltaTime) : target;
            chibi.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            lastPosition = position;
            placed = true;

            string clip;
            strideRate = -1;
            if (isDowned) clip = "AH_knocked_down";
            else if (sleeping) clip = "AH_sleep";
            else if (traveling) clip = Stride();
            else if (working && workRoom != null && TowerChibi3D.RoomClips.TryGetValue(workRoom, out clip)) { }
            else clip = "AH_idle";
            chibi.Play(clip);
            ApplySpeed();
        }

        // Walk, or run when moving fast, played at the rate whose stride matches the actual speed so the feet
        // don't slide. Riding the shaft straight up or down (no sideways speed) stands still instead.
        private string Stride()
        {
            if (groundSpeed < 0.05f) return "AH_idle";
            // The body is turned WalkYaw from the camera, so its stride runs at that angle to the floor's x axis.
            float along = groundSpeed / Mathf.Sin(WalkYaw * Mathf.Deg2Rad);
            float scale = chibi.transform.lossyScale.x;
            float walk = chibi.GroundSpeed("AH_walk") * scale, run = chibi.GroundSpeed("AH_run") * scale;
            string clip = run > 0 && along > walk * 1.6f ? "AH_run" : "AH_walk";
            float natural = clip == "AH_run" ? run : walk;
            if (natural > 0) strideRate = Mathf.Clamp(along / natural, 0.35f, 3f);
            return clip;
        }
    }
}
