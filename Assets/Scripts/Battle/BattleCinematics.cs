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

    // Cine framing shared with Battle2DRigBuilder.RenderCineFrame: an orthographic 16:9 view of the rig, centred here.
    public const float CineCenterX = .55f, CineCenterY = 1.35f, CineHalf = .95f;
    const float MatchZoom = .32f;                     // seconds (real time) for each zoom of the match cut
    readonly HashSet<string> cinematicsSeen = new HashSet<string>();
    readonly Dictionary<string, VideoClip> cardClips = new Dictionary<string, VideoClip>();
    BattleUnit matchUnit;                             // the 2D fighter of the current match cut, or null
    float matchIn;                                    // zoom-in length in battle seconds (cutStart .. cutStart + matchIn)

    static bool IsUltimate(BattleCard card) { return card.Kind == BattleCardKind.Ultimate || card.Kind == BattleCardKind.Awakening; }

    // A skill card's own clip (no fallback): having one is what makes it a cinematic card.
    VideoClip CardClip(BattleCard card)
    {
        VideoClip clip;
        if (!cardClips.TryGetValue(card.Id, out clip))
        {
            clip = Resources.Load<VideoClip>("AdamsHaven/UltCutIns/" + card.Id);
            cardClips[card.Id] = clip;
        }
        return clip;
    }

    // The clip to play for this action, or null for none (setting, first use, or no clip).
    VideoClip CinematicFor(BattleUnit actor, BattleCard card)
    {
        if (actor == null || card == null || actor.Enemy) return null;
        var mode = Cinematics;
        if (mode == CinematicMode.Off) return null;
        if (IsUltimate(card)) return UltClip(actor, card);
        if (mode == CinematicMode.UltimatesOnly) return null;
        var clip = CardClip(card);
        if (clip == null) return null;
        if (mode == CinematicMode.FirstUse && cinematicsSeen.Contains(card.Id)) return null;
        return clip;
    }

    // Field rect of a fighter's rig image and the rect that lines it up with the cine framing (virtual canvas).
    Rect FieldRigRect(BattleUnit unit)
    {
        SlotInfo s = Slot(unit);
        float size = s.H * FieldCamHalf;
        return new Rect(s.Foot.x - size / 2, s.Foot.y - s.H * (FieldCamCenter + FieldCamHalf) / 2f, size, size);
    }
    static Rect CineRigRect()
    {
        float k = VH / (2f * CineHalf), halfW = CineHalf * VW / VH;
        return new Rect((-FieldCamHalf - (CineCenterX - halfW)) * k, ((CineCenterY + CineHalf) - (FieldCamCenter + FieldCamHalf)) * k,
            2f * FieldCamHalf * k, 2f * FieldCamHalf * k);
    }

    Texture2D cineBackdrop;
    Texture2D CineBackdrop { get { if (!cineBackdrop) cineBackdrop = Resources.Load<Texture2D>("AdamsHaven/Fx/cine_bg"); return cineBackdrop; } }

    // Zoom in (t from 0 to 1) or back out: the backdrop fades over the field while the rig image grows into place.
    void DrawMatchZoom(float t)
    {
        FieldRig rig;
        if (matchUnit == null || !fieldRigs.TryGetValue(matchUnit, out rig) || rig.Image == null) return;
        float k = Ease(Mathf.Clamp01(t));
        Rect from = FieldRigRect(matchUnit), to = CineRigRect();
        Rect r = new Rect(Mathf.Lerp(from.x, to.x, k), Mathf.Lerp(from.y, to.y, k), Mathf.Lerp(from.width, to.width, k), Mathf.Lerp(from.height, to.height, k));
        Color was = GUI.color;
        Rect cover = new Rect(-VW, -VH, VW * 3f, VH * 3f);
        if (CineBackdrop != null)
        {
            Fill(cover, new Color(0, 0, 0, k * .6f));
            GUI.color = new Color(1, 1, 1, k);
            GUI.DrawTexture(new Rect(0, 0, VW, VH), CineBackdrop, ScaleMode.ScaleAndCrop, false);
        }
        else Fill(cover, new Color(.02f, .03f, .07f, k));
        GUI.color = Color.white;
        GUI.DrawTexture(r, rig.Image, ScaleMode.StretchToFill, true);
        GUI.color = was;
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
        int frame = Mathf.Clamp(Mathf.FloorToInt(t * 16f), 0, 15);
        float cellW = e.Sheet.width / 4f, cellH = e.Sheet.height / 4f;
        float w = e.Size, h = w * cellH / cellW;
        Color was = GUI.color;
        GUI.color = new Color(1, 1, 1, t > .8f ? (1f - t) / .2f : 1f);
        GUI.DrawTextureWithTexCoords(new Rect(e.Pos.x - w / 2, e.Pos.y - h / 2, w, h), e.Sheet,
            new Rect((frame % 4) / 4f, 1f - (frame / 4 + 1) / 4f, .25f, .25f), true);
        GUI.color = was;
    }

    void ReleaseMoveFx()
    {
        if (fxPlayer != null) { Destroy(fxPlayer.gameObject); fxPlayer = null; }
        if (fxTexture != null) { fxTexture.Release(); Destroy(fxTexture); fxTexture = null; }
        if (stackedAlpha != null) { Destroy(stackedAlpha); stackedAlpha = null; }
        if (fxAdditive != null) { Destroy(fxAdditive); fxAdditive = null; }
        fxVideoOn = false;
        moveSheets.Clear(); moveVideos.Clear(); cardClips.Clear();
    }
}
