using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using static BattleGui;

// Cinematics and per-move effects (2D rig pilot).
//  - Tiers: ultimates and awakenings always have a cinematic; a skill card has one only when its clip exists
//    (Resources/AdamsHaven/UltCutIns/<card id>.mp4). The CINE setting chooses: every time, first use per battle
//    (default), ultimates only, or none. Clips play at 1x whatever the battle speed, and a tap skips them.
//  - A fighter drawn as a 2D rig gets a match cut: the field zooms in on her until it matches the clip's first frame
//    (rendered from the same rig at the cine framing below), the clip plays, and the last frame zooms back out.
//  - Move effects: Resources/AdamsHaven/Fx/Moves/<card id>.png (16-frame full-colour sheet) or .mp4 (colour over a
//    stacked matte, drawn with Fx/StackedAlpha) from Tools/produce_move_fx.py.
public sealed partial class BattleMode
{
    public enum CinematicMode { Always, FirstUse, UltimatesOnly, Off }
    public const string CinematicKey = "AdamsHaven.Battle.Cinematics";
    public static CinematicMode Cinematics
    {
        get { return (CinematicMode)Mathf.Clamp(PlayerPrefs.GetInt(CinematicKey, (int)CinematicMode.FirstUse), 0, 3); }
        set { PlayerPrefs.SetInt(CinematicKey, (int)value); PlayerPrefs.Save(); }
    }
    static string CinematicLabel(CinematicMode m)
    {
        return m == CinematicMode.Always ? "CINE: ALL" : m == CinematicMode.FirstUse ? "CINE: FIRST" : m == CinematicMode.UltimatesOnly ? "CINE: ULTS" : "CINE: OFF";
    }

    // Cine framing used by Battle2DRigBuilder.RenderCineFrame (the retired 2D-rig match cut).
    public const float CineCenterX = .55f, CineCenterY = 1.35f, CineHalf = .95f;
    readonly HashSet<string> cinematicsSeen = new HashSet<string>();

    static bool IsUltimate(BattleCard card) { return card.Kind == BattleCardKind.Ultimate || card.Kind == BattleCardKind.Awakening; }
    // JD's decrees (BattleCatalog.SummonerUltimates) are his ultimates: they cut to video like the party's.
    static bool IsDecree(BattleCard card) { return card.Kind == BattleCardKind.Summoner && card.Id.StartsWith("ult_sum_"); }

    // The clip to play for this action, or null for none (setting, first use, or no clip).
    VideoClip CinematicFor(BattleUnit actor, BattleCard card)
    {
        if (actor == null || card == null || actor.Enemy) return null;
        var mode = Cinematics;
        if (mode == CinematicMode.Off) return null;
        // Skills are staged in battle now (BattleStage.cs); only ultimates and awakenings cut to video.
        // A joint ultimate has its own video or none: never the lead's solo one.
        if (BattleCombos.IsJoint(card)) return JointClip(card);
        return IsUltimate(card) || IsDecree(card) ? UltClip(actor, card) : null;
    }


    // ---- per-move effects --------------------------------------------------------------------------------

    struct MoveFxSpec
    {
        public bool OnTarget;     // at the target's chest; otherwise around the actor
        public float Size, Life;  // width in virtual pixels; seconds of battle time
        public float Lift;        // fraction of the unit's height above the feet
        public MoveFxSpec(bool onTarget, float size, float life, float lift) { OnTarget = onTarget; Size = size; Life = life; Lift = lift; }
    }
    static readonly Dictionary<string, MoveFxSpec> MoveFx = new Dictionary<string, MoveFxSpec>
    {
        { "mv_frost_jab", new MoveFxSpec(true, 300f, .5f, .55f) },
        { "mv_rime_sweep", new MoveFxSpec(true, 470f, .62f, .45f) },
        { "mv_shatter_hook", new MoveFxSpec(true, 430f, .7f, .55f) },
        { "mv_whiteout_counter", new MoveFxSpec(false, 380f, .8f, .5f) },
        { "mv_permafrost_brace", new MoveFxSpec(false, 360f, .8f, .2f) },
        { "mv_glacier_guard", new MoveFxSpec(false, 380f, 0f, .42f) },    // video: its own length; the shell rises from the feet
    };
    readonly Dictionary<string, Texture2D> moveSheets = new Dictionary<string, Texture2D>();
    readonly Dictionary<string, VideoClip> moveVideos = new Dictionary<string, VideoClip>();
    VideoPlayer fxPlayer;
    RenderTexture fxTexture;
    Material stackedAlpha;
    Vector2 fxVideoAt;
    float fxVideoSize;
    bool fxVideoOn;

    Texture2D MoveSheet(string card)
    {
        Texture2D t;
        if (!moveSheets.TryGetValue(card, out t)) { t = Resources.Load<Texture2D>("AdamsHaven/Fx/Moves/" + card); moveSheets[card] = t; }
        return t;
    }
    VideoClip MoveVideo(string card)
    {
        VideoClip c;
        if (!moveVideos.TryGetValue(card, out c)) { c = Resources.Load<VideoClip>("AdamsHaven/Fx/Moves/" + card); moveVideos[card] = c; }
        return c;
    }

    // Called at the card's contact time (ScheduleGroup).
    void SpawnMoveFx(BattleCard card, BattleUnit actor, BattleUnit target)
    {
        if (card != null && SpawnImportedFx(card, actor, target)) return;
        MoveFxSpec spec;
        if (card == null || !MoveFx.TryGetValue(card.Id, out spec)) return;
        BattleUnit at = spec.OnTarget && target != null ? target : actor;
        if (at == null || !slots.ContainsKey(at)) return;
        SlotInfo s = Slot(at);
        Vector2 pos = s.Foot + new Vector2(0f, -s.H * spec.Lift);
        var video = MoveVideo(card.Id);
        if (video != null) { PlayFxVideo(video, pos, spec.Size); return; }
        var sheet = MoveSheet(card.Id);
        if (sheet == null) return;
        effects.Add(new Effect { Kind = 11, Pos = pos, Born = fx, Life = spec.Life, Size = spec.Size, Color = Color.white, Sheet = sheet });
    }

    bool SpawnImportedFx(BattleCard card, BattleUnit actor, BattleUnit target)
    {
        var entry = MediaLibrary.Get("card." + card.Id + ".fx");
        if (entry == null) return false;
        MoveFxSpec spec;
        if (!MoveFx.TryGetValue(card.Id, out spec)) spec = new MoveFxSpec(card.Power > 0, 400f, .7f, .5f);
        BattleUnit at = spec.OnTarget && target != null ? target : actor;
        if (at == null || !slots.ContainsKey(at)) return false;
        SlotInfo s = Slot(at);
        Vector2 pos = s.Foot + new Vector2(0f, -s.H * spec.Lift);
        if (entry.kind == MediaLibrary.Image)
        {
            var tex = MediaLibrary.ImageFor(entry.key);
            if (tex == null) return false;
            effects.Add(new Effect { Kind = 12, Pos = pos, Born = fx, Life = Mathf.Max(.5f, spec.Life), Size = spec.Size, Color = Color.white, Sheet = tex });
            return true;
        }
        if (entry.kind != MediaLibrary.Video) return false;
        PlayFxVideo(null, pos, spec.Size, MediaLibrary.VideoUrl(entry.key));
        return true;
    }

    // An imported effect picture: pops in, drifts up a little and fades.
    void DrawImportedFx(Effect e, float t)
    {
        float k = BattleGui.EaseOut(Mathf.Clamp01(t * 4f));
        float w = e.Size * (.7f + .3f * k), h = w * e.Sheet.height / Mathf.Max(1f, e.Sheet.width);
        Color was = GUI.color;
        GUI.color = new Color(1, 1, 1, t > .7f ? (1f - t) / .3f : 1f);
        GUI.DrawTexture(new Rect(e.Pos.x - w / 2, e.Pos.y - h / 2 - t * 30f, w, h), e.Sheet, ScaleMode.StretchToFill, true);
        GUI.color = was;
    }

    bool fxVideoAdditive;
    Material fxAdditive;

    void PlayFxVideo(VideoClip clip, Vector2 pos, float size)
    {
        PlayFxVideo(clip, pos, size, null);
    }

    void PlayFxVideo(VideoClip clip, Vector2 pos, float size, string url)
    {
        if (fxPlayer == null)
        {
            var host = new GameObject("Move FX video");
            host.transform.SetParent(transform, false);
            fxPlayer = host.AddComponent<VideoPlayer>();
            fxPlayer.playOnAwake = false; fxPlayer.isLooping = false;
            fxPlayer.renderMode = VideoRenderMode.RenderTexture;
            fxPlayer.audioOutputMode = VideoAudioOutputMode.None;
            fxPlayer.skipOnDrop = true;
            fxPlayer.loopPointReached += p => fxVideoOn = false;
        }
        int fw = clip != null ? (int)clip.width : 1024, fh = clip != null ? (int)clip.height : 1024;
        if (fxTexture == null || fxTexture.width != fw || fxTexture.height != fh)
        {
            if (fxTexture != null) fxTexture.Release();
            fxTexture = new RenderTexture(fw, fh, 0);
            fxPlayer.targetTexture = fxTexture;
        }
        if (stackedAlpha == null)
        {
            var shader = Shader.Find("AdamsHaven/StackedAlpha");
            if (shader != null) stackedAlpha = new Material(shader);
        }
        if (fxAdditive == null)
        {
            var shader = Shader.Find("AdamsHaven/ScreenAdd");
            if (shader != null) fxAdditive = new Material(shader);
        }
        fxVideoAdditive = clip == null;
        if (clip == null) { fxPlayer.source = VideoSource.Url; fxPlayer.url = url; }
        else fxPlayer.source = VideoSource.VideoClip;
        fxPlayer.clip = clip;
        fxPlayer.playbackSpeed = speed;             // effects stay on the battle clock, unlike cinematics
        fxPlayer.time = 0;
        fxPlayer.Play();
        fxVideoAt = pos; fxVideoSize = size; fxVideoOn = true;
    }

    // Drawn with the other effects; the colour half is premultiplied, so it composites like additive light.
    void DrawFxVideo()
    {
        if (!fxVideoOn || fxPlayer == null || !fxPlayer.isPlaying || fxTexture == null) return;
        if (Event.current.type != EventType.Repaint) return;
        if (fxVideoAdditive)
        {
            // Imported effect videos: full frame, added like light so black stays see-through.
            float aw = fxVideoSize, ah = aw * fxPlayer.height / Mathf.Max(1f, fxPlayer.width);
            if (fxAdditive != null) Graphics.DrawTexture(new Rect(fxVideoAt.x - aw / 2, fxVideoAt.y - ah / 2, aw, ah), fxTexture, fxAdditive);
            else GUI.DrawTexture(new Rect(fxVideoAt.x - aw / 2, fxVideoAt.y - ah / 2, aw, ah), fxTexture, ScaleMode.StretchToFill, true);
            return;
        }
        if (stackedAlpha == null) return;
        float w = fxVideoSize, h = w * (fxTexture.height * .5f) / fxTexture.width;
        Graphics.DrawTexture(new Rect(fxVideoAt.x - w / 2, fxVideoAt.y - h / 2, w, h), fxTexture, stackedAlpha);
    }

    void DrawMoveSheet(Effect e, float t)
    {
        int cols = e.Cols > 0 ? e.Cols : 4, rows = e.Rows > 0 ? e.Rows : 4, frames = e.Frames > 0 ? e.Frames : cols * rows;
        int frame = Mathf.Clamp(Mathf.FloorToInt(t * frames), 0, frames - 1);
        float cellW = e.Sheet.width / (float)cols, cellH = e.Sheet.height / (float)rows;
        float w = e.Size, h = w * cellH / cellW;
        Color was = GUI.color;
        GUI.color = new Color(1, 1, 1, t > .8f ? (1f - t) / .2f : 1f);
        GUI.DrawTextureWithTexCoords(new Rect(e.Pos.x - w / 2, e.Pos.y - h / 2, w, h), e.Sheet, SheetUv(e.Sheet, cols, rows, frame), true);
        GUI.color = was;
    }

    void ReleaseMoveFx()
    {
        if (fxPlayer != null) { Destroy(fxPlayer.gameObject); fxPlayer = null; }
        if (fxTexture != null) { fxTexture.Release(); Destroy(fxTexture); fxTexture = null; }
        if (stackedAlpha != null) { Destroy(stackedAlpha); stackedAlpha = null; }
        if (fxAdditive != null) { Destroy(fxAdditive); fxAdditive = null; }
        fxVideoOn = false;
        moveSheets.Clear(); moveVideos.Clear();
        ReleaseLayerSheets();
    }
}
