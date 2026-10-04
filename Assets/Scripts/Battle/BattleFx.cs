using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

// Rigged field models can subscribe to these timed cues. The cutout renderer also
// uses the same timeline, so a future Animator does not have to infer actions from HP.
public enum BattleAnimationPhase { Ultimate, Action, Hit, Heal, Status, Down, DrawCards, PlayCard }

public sealed class BattleAnimationSignal
{
    public BattleAnimationPhase Phase;
    public BattleUnit Actor, Target;
    public BattleCard Card;
    public BattleFact Fact;
}

// Presentation timeline for Battle Mode. The rules resolve instantly; this layer replays the
// resulting BattleFacts as lunges, projectiles, impacts, floating numbers and HP drains, so the
// screen shows a fight rather than a spreadsheet updating.
public sealed partial class BattleMode
{
    public event Action<BattleAnimationSignal> AnimationSignal;

    private sealed class Beat { public float At; public Action Run; }

    private sealed class UnitVis
    {
        public float TargetHp, ShownHp, GhostHp;
        public float LungeStart = -9f, LungeDur = 0.66f, Impact = 0.30f;
        public float HoldTo = -1f;      // battle time a melee strike holds at the target until (a staged skill's pull-back)
        public Vector2 LungeTo;
        public bool Ranged;
        public float HurtStart = -9f, HurtDir = 1f;
        public float DeathStart = -1f;
        public float BannerStart = -9f;
        public string Banner = "";
        public bool IntentSpent, CollapseShown;
        // Handed over from an ultimate's video: the clip starts on its key pose (ClipFrom >= 0) at ClipStart, and a
        // melee fighter is already at the target (Arrive) instead of dashing in again.
        public float ClipFrom = -1f, ClipStart;
        public bool Arrive;
        // Summon battles (BattleSummons.cs): the battle-time window a fighter is out of their contract.
        public float SummonIn = -9f, SummonOut = -9f;
    }

    private struct Particle
    {
        public Vector2 Pos, Vel;
        public float Born, Life, Size, Drag, Gravity;
        public Color Color;
        public bool Spark;
    }

    private struct Effect
    {
        public byte Kind;              // 0 flash, 1 shockwave, 2 streak, 3 slash sheet, 4 burst sheet, 5 ground rune, 6 sparkle,
                                       // 7-8 painted slash/impact, 9 painted element sheet (Sheet), 10 full-screen warp,
                                       // 11 per-move colour sheet (moves.json impact), 12 imported picture,
                                       // 13 charge riding the muzzle, 14 beam, 15 bolt (BattleMoveFx.cs)
        public Vector2 Pos;
        public float Born, Life, Size, Angle;
        public Color Color;
        public Texture2D Sheet;
        public BattleUnit Unit, Target;
        public float Flight, Grow;
        public int Cols, Rows, Frames;
        public bool Under;             // drawn beneath the units (ground circles, walls of fire)
    }

    private sealed class Floater
    {
        public string Text;
        public Vector2 Pos, Vel;
        public float Born, Life;
        public int Size;
        public Color Color;
    }

    private struct Bolt { public Vector2 From, To; public float Start, Dur; public Color Color; }
    private struct FlyCard { public BattleCard Card; public Vector2 From; public float Angle, Scale, Start; }
    private struct BannerFx { public string Text; public Color Color; public float Start; }

    private readonly List<Beat> beats = new List<Beat>();
    private readonly Dictionary<BattleUnit, UnitVis> vis = new Dictionary<BattleUnit, UnitVis>();
    private readonly List<Particle> particles = new List<Particle>();
    private readonly List<Effect> effects = new List<Effect>();
    private readonly List<Floater> floaters = new List<Floater>();
    private readonly List<Bolt> bolts = new List<Bolt>();
    private readonly List<FlyCard> flying = new List<FlyCard>();
    private readonly List<BannerFx> banners = new List<BannerFx>();
    private float fx, queueEnd, shake, hitStop, cutStart = -9f;
    private int factsSeen, announcedRound;
    private BattleUnit cutUnit;
    private BattleCard cutCard;
    private VideoClip cutClip;
    private const float CutLen = 1.15f;
    private float cutLen = CutLen;
    // Ultimate cinematics (Resources/AdamsHaven/UltCutIns/<unit id>.mp4, from Tools/produce_battle_motion.py).
    private VideoPlayer ultPlayer;
    private RenderTexture ultTexture;
    private readonly Dictionary<string, VideoClip> ultClips = new Dictionary<string, VideoClip>();

    // One clip per ultimate card (UltCutIns/<card id>), falling back to one per fighter (UltCutIns/<unit id>).
    private VideoClip UltClip(BattleUnit unit, BattleCard card)
    {
        string key = unit.Id + "/" + (card != null ? card.Id : "");
        VideoClip clip;
        if (!ultClips.TryGetValue(key, out clip))
        {
            clip = card != null ? Resources.Load<VideoClip>("AdamsHaven/UltCutIns/" + card.Id) : null;
            if (clip == null) clip = Resources.Load<VideoClip>("AdamsHaven/UltCutIns/" + unit.Id);
            ultClips[key] = clip;
        }
        return clip;
    }

    private void PlayUltClip(VideoClip clip)
    {
        if (ultPlayer == null)
        {
            GameObject host = new GameObject("UltCutInPlayer");
            host.transform.SetParent(transform, false);
            ultPlayer = host.AddComponent<VideoPlayer>();
            ultPlayer.playOnAwake = false;
            ultPlayer.isLooping = false;
            ultPlayer.renderMode = VideoRenderMode.RenderTexture;
            ultPlayer.audioOutputMode = VideoAudioOutputMode.None;
            ultPlayer.skipOnDrop = true;
        }
        if (ultTexture == null || ultTexture.width != (int)clip.width)
        {
            if (ultTexture != null) ultTexture.Release();
            ultTexture = new RenderTexture((int)clip.width, (int)clip.height, 0);
            ultPlayer.targetTexture = ultTexture;
        }
        ultPlayer.source = VideoSource.VideoClip;
        ultPlayer.audioOutputMode = VideoAudioOutputMode.None;
        ultPlayer.clip = clip;
        ultPlayer.playbackSpeed = 1f;       // cinematics run in real time, whatever the battle speed
        ultPlayer.time = 0;
        ultPlayer.Play();
    }

    // An imported cinematic (media library), played from its file.
    private string cutUrl;
    private void PlayUltUrl(string url, int width, int height)
    {
        if (ultPlayer == null)
        {
            GameObject host = new GameObject("UltCutInPlayer");
            host.transform.SetParent(transform, false);
            ultPlayer = host.AddComponent<VideoPlayer>();
            ultPlayer.playOnAwake = false;
            ultPlayer.isLooping = false;
            ultPlayer.renderMode = VideoRenderMode.RenderTexture;
            ultPlayer.audioOutputMode = VideoAudioOutputMode.None;
            ultPlayer.skipOnDrop = true;
        }
        int w = width > 0 ? width : 1280, h = height > 0 ? height : 720;
        if (ultTexture == null || ultTexture.width != w || ultTexture.height != h)
        {
            if (ultTexture != null) ultTexture.Release();
            ultTexture = new RenderTexture(w, h, 0);
            ultPlayer.targetTexture = ultTexture;
        }
        ultPlayer.clip = null;
        ultPlayer.source = VideoSource.Url;
        ultPlayer.url = url;
        // Imported clips keep their own sound track.
        ultPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        ultPlayer.playbackSpeed = 1f;
        ultPlayer.Play();
    }

    // The imported cinematic for this action, following the CINE setting like the game's own.
    private MediaLibrary.Entry MediaCineFor(BattleUnit actor, BattleCard card)
    {
        if (actor == null || card == null || actor.Enemy) return null;
        var entry = MediaLibrary.Get("card." + card.Id + ".cine");
        if (entry == null || entry.kind != MediaLibrary.Video) return null;
        var mode = Cinematics;
        if (mode == CinematicMode.Off) return null;
        if (mode == CinematicMode.UltimatesOnly && !IsUltimate(card)) return null;
        if (mode == CinematicMode.FirstUse && !IsUltimate(card) && cinematicsSeen.Contains(card.Id)) return null;
        return entry;
    }

    private bool CutVideoReady { get { return ultPlayer != null && ultTexture != null && (cutClip != null ? ultPlayer.clip == cutClip : cutUrl != null && ultPlayer.url == cutUrl); } }

    private void ReleaseUltClip()
    {
        if (ultPlayer != null) { Destroy(ultPlayer.gameObject); ultPlayer = null; }
        if (ultTexture != null) { ultTexture.Release(); Destroy(ultTexture); ultTexture = null; }
        ultClips.Clear();
    }

    private bool Busy { get { return fx < queueEnd - 0.001f || beats.Count > 0; } }

    // An ultimate cut-in is on screen (its length in battle time follows the battle speed).
    private bool CutInShowing { get { return cutUnit != null && cutCard != null && fx >= cutStart && fx < cutStart + cutLen; } }

    // Tap during a cut-in: jump to its end. The action itself still plays, so nothing is lost but the cinematic.
    private void SkipCutIn()
    {
        fx = cutStart + cutLen;
        hitStop = 0f;
        cutClip = null; cutUrl = null;
        if (ultPlayer != null) ultPlayer.Stop();
    }

    private void ResetFx()
    {
        beats.Clear(); vis.Clear(); particles.Clear(); effects.Clear(); floaters.Clear();
        bolts.Clear(); flying.Clear(); banners.Clear();
        fx = 0f; queueEnd = 0f; shake = 0f; hitStop = 0f; cutStart = -9f;
        cutUnit = null; cutCard = null; cutClip = null; factsSeen = 0; announcedRound = 0;
        cinematicsSeen.Clear();
        ResetStage();
    }

    private void Signal(BattleAnimationPhase phase, BattleUnit actor, BattleUnit target, BattleCard card, BattleFact fact = null)
    {
        Sound(phase, actor, card, fact);
        Action<BattleAnimationSignal> listeners = AnimationSignal;
        if (listeners == null) return;
        BattleAnimationSignal signal = new BattleAnimationSignal
        { Phase = phase, Actor = actor, Target = target, Card = card, Fact = fact };
        foreach (Delegate listener in listeners.GetInvocationList())
        {
            try { ((Action<BattleAnimationSignal>)listener)(signal); }
            catch (Exception error) { Debug.LogException(error); }
        }
    }

    private void At(float time, Action run) { beats.Add(new Beat { At = time, Run = run }); }

    // Each beat of the fight has a sound; they follow the beats, so they slow down with the battle speed.
    private void Sound(BattleAnimationPhase phase, BattleUnit actor, BattleCard card, BattleFact fact)
    {
        switch (phase)
        {
            // Ultimates and awakenings play their own recorded sound with the cut-in (it builds to a climax about as long
            // as the clip); without one, the shared whoosh.
            case BattleAnimationPhase.Ultimate:
            {
                var own = card != null && IsUltimate(card) ? MoveSound(card) : null;
                if (own != null) AdamsHaven.Tower.TowerAudio.PlayClip(own, .8f);
                else AdamsHaven.Tower.TowerAudio.Play("ult");
                break;
            }
            case BattleAnimationPhase.Action: ActionSound(actor, card); break;
            case BattleAnimationPhase.Hit: HitSound(fact); break;
            case BattleAnimationPhase.Heal: AdamsHaven.Tower.TowerAudio.PlayVaried("heal", 0.6f, 0.04f); break;
            case BattleAnimationPhase.Status: StatusSound(fact); break;
            case BattleAnimationPhase.Down: AdamsHaven.Tower.TowerAudio.Play("down", 0.8f); break;
            case BattleAnimationPhase.PlayCard: Sfx("Sfx/card_play", "card", 0.6f); break;
            case BattleAnimationPhase.DrawCards: if (AdamsHaven.Tower.TowerAudio.Has("Sfx/card_draw")) AdamsHaven.Tower.TowerAudio.PlayVaried("Sfx/card_draw", .5f); break;
        }
    }

    private UnitVis V(BattleUnit unit)
    {
        UnitVis v;
        if (!vis.TryGetValue(unit, out v))
        {
            v = new UnitVis { TargetHp = unit.Hp, ShownHp = unit.Hp, GhostHp = unit.Hp };
            vis[unit] = v;
        }
        return v;
    }

    // A unit is drawn until its own death beat has played, even though the rules already killed it.
    private bool ShownAlive(BattleUnit unit) { return unit.Alive || V(unit).DeathStart < 0f; }

    private void TickFx(float realDt)
    {
        // A tiny freeze on contact gives the slash and hit number a readable impact.
        float dt = hitStop > 0f ? 0f : realDt * speed * StageClock;
        hitStop = Mathf.Max(0f, hitStop - realDt);
        fx += dt;
        for (int i = 0; i < beats.Count; i++)
            if (beats[i].At <= fx) { Beat b = beats[i]; beats.RemoveAt(i); i--; b.Run(); }
        foreach (KeyValuePair<BattleUnit, UnitVis> pair in vis)
        {
            UnitVis v = pair.Value;
            v.ShownHp = Mathf.MoveTowards(v.ShownHp, v.TargetHp, Mathf.Max(1f, pair.Key.MaxHp) * 2.4f * dt + Mathf.Abs(v.TargetHp - v.ShownHp) * 6f * dt);
            if (v.GhostHp > v.ShownHp) v.GhostHp = Mathf.Max(v.ShownHp, v.GhostHp - (v.GhostHp - v.ShownHp) * 2.2f * dt - Mathf.Max(1f, pair.Key.MaxHp) * 0.03f * dt);
            else v.GhostHp = v.ShownHp;
        }
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            Particle p = particles[i];
            if (fx - p.Born > p.Life) { particles.RemoveAt(i); continue; }
            p.Vel *= Mathf.Max(0f, 1f - p.Drag * dt);
            p.Vel.y += p.Gravity * dt;
            p.Pos += p.Vel * dt;
            particles[i] = p;
        }
        for (int i = effects.Count - 1; i >= 0; i--) if (fx - effects[i].Born > effects[i].Life) effects.RemoveAt(i);
        for (int i = floaters.Count - 1; i >= 0; i--) if (fx - floaters[i].Born > floaters[i].Life) floaters.RemoveAt(i);
        for (int i = bolts.Count - 1; i >= 0; i--) if (fx - bolts[i].Start > bolts[i].Dur + 0.15f) bolts.RemoveAt(i);
        for (int i = flying.Count - 1; i >= 0; i--) if (fx - flying[i].Start > FlyLen) flying.RemoveAt(i);
        for (int i = banners.Count - 1; i >= 0; i--) if (fx - banners[i].Start > 1.05f) banners.RemoveAt(i);
        shake = Mathf.MoveTowards(shake, 0f, realDt * 46f);
    }

    // ---- palette -----------------------------------------------------------------------------

    private static Color ElementColor(BattleElement element)
    {
        switch (element)
        {
            case BattleElement.Fire: return new Color(1f, 0.52f, 0.20f);
            case BattleElement.Water: return new Color(0.38f, 0.82f, 1f);
            case BattleElement.Wind: return new Color(0.56f, 1f, 0.72f);
            case BattleElement.Earth: return new Color(0.94f, 0.74f, 0.32f);
            case BattleElement.Lightning: return new Color(1f, 0.94f, 0.36f);
            case BattleElement.Light: return new Color(1f, 0.94f, 0.72f);
            case BattleElement.Dark: return new Color(0.76f, 0.42f, 1f);
        }
        return new Color(0.88f, 0.94f, 1f);
    }

    private static bool IsBuff(string status)
    {
        return status == "AttackUp" || status == "DefenseUp" || status == "Haste" || status == "Regen"
            || status == "Taunt" || status == "IceCounter" || status == "Shield" || status == "Charging" || status == "Frenzy";
    }

    private static string StatusLabel(string status)
    {
        switch (status)
        {
            case "AttackUp": return "ATTACK UP";
            case "AttackDown": return "ATTACK DOWN";
            case "DefenseUp": return "DEFENSE UP";
            case "DefenseDown": return "DEFENSE DOWN";
            case "Slow": return "SLOWED";
            case "Haste": return "HASTE";
            case "Poison": return "POISONED";
            case "Regen": return "REGEN";
            case "Taunt": return "TAUNT";
            case "IceCounter": return "ICE COUNTER";
            case "Burn": return "BURNING";
            case "Stun": return "STUNNED";
            case "Shield": return "SHIELDED";
            case "Charging": return "CHARGING";
            case "Frenzy": return "FRENZY";
        }
        return status.ToUpperInvariant();
    }

    // ---- spawning ----------------------------------------------------------------------------

    private void Burst(Vector2 at, Color color, int count, float minSpeed, float maxSpeed, float minSize, float maxSize,
        float life, float gravity, bool spark = false)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = UnityEngine.Random.value * Mathf.PI * 2f;
            float s = UnityEngine.Random.Range(minSpeed, maxSpeed);
            particles.Add(new Particle { Pos = at, Vel = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * s, Born = fx,
                Life = life * UnityEngine.Random.Range(0.7f, 1.2f), Size = UnityEngine.Random.Range(minSize, maxSize),
                Drag = 2.2f, Gravity = gravity, Color = color, Spark = spark });
        }
    }

    private void Spawn(byte kind, Vector2 pos, Color color, float size, float life, float angle = 0f)
    {
        effects.Add(new Effect { Kind = kind, Pos = pos, Born = fx, Life = life, Size = size, Color = color, Angle = angle });
    }

    private void Float(Vector2 pos, string text, int size, Color color, float life = 1.05f, float rise = 78f, float drift = 0f)
    {
        floaters.Add(new Floater { Text = text, Pos = pos, Vel = new Vector2(drift, -rise), Born = fx, Life = life, Size = size, Color = color });
    }

    private void ShowBanner(string text, Color color)
    {
        banners.Add(new BannerFx { Text = text, Color = color, Start = fx });
    }

    // painted: the generic painted slash / star / element sheet; off when the card brings its own impact layer.
    private void Impact(Vector2 at, Color color, bool crit, float weight, BattleElement element = BattleElement.Neutral, bool painted = true)
    {
        hitStop = Mathf.Max(hitStop, crit ? 0.075f : Mathf.Lerp(0.025f, 0.055f, weight));
        Spawn(0, at, Color.white, 150f + weight * 130f, 0.20f);
        Spawn(0, at, color, 260f + weight * 200f, 0.34f);
        Spawn(1, at, color, 190f + weight * 190f, 0.42f);
        float tilt = UnityEngine.Random.Range(-40f, 40f);
        Spawn(3, at, color, 330f + weight * 190f, 0.34f, tilt);
        Spawn(3, at, Color.white, 250f + weight * 150f, 0.30f, tilt);
        Spawn(4, at, Color.Lerp(color, Color.white, 0.35f), 260f + weight * 220f + (crit ? 90f : 0f), 0.36f, UnityEngine.Random.Range(0f, 360f));
        Spawn(2, at, Color.white, 300f + weight * 160f, 0.24f, UnityEngine.Random.Range(-38f, -8f));
        // Painted H3 layers on top of the procedural ones (CZN-style white-core slash, star impact, crit warp).
        Color hot = Color.Lerp(color, Color.white, 0.5f); hot.a = 0.8f;
        if (painted && BattleGui.GenSlash != null) Spawn(7, at, hot, 290f + weight * 170f, 0.30f, tilt * 0.5f);
        if (painted && BattleGui.GenImpact != null) Spawn(8, at, hot, 210f + weight * 180f + (crit ? 100f : 0f), 0.34f, UnityEngine.Random.Range(-20f, 20f));
        Texture2D burst = painted ? BattleGui.ElementSheet(element) : null;
        if (burst != null) effects.Add(new Effect { Kind = 9, Pos = at + new Vector2(0f, -30f), Born = fx, Life = 0.5f,
            Size = 260f + weight * 140f, Color = new Color(hot.r, hot.g, hot.b, 0.65f), Sheet = burst });
        if (BattleGui.GenWarp != null && crit) Spawn(10, new Vector2(800f, 450f), new Color(hot.r, hot.g, hot.b, 0.6f), 1700f, 0.32f);
        Burst(at, color, 12 + (int)(weight * 12f), 180f, 520f, 4f, 9f, 0.55f, 520f, true);
        Burst(at, Color.white, 6, 90f, 260f, 6f, 12f, 0.42f, 0f);
        shake = Mathf.Max(shake, 3f + weight * 9f + (crit ? 5f : 0f));
    }

    // ---- ingesting facts ---------------------------------------------------------------------

    // Called after every rules mutation. Turns newly recorded facts into scheduled beats.
    private void Ingest()
    {
        int fresh = battle.FactSerial - factsSeen;
        factsSeen = battle.FactSerial;
        if (fresh <= 0) return;
        int start = Mathf.Max(0, battle.Facts.Count - fresh);
        List<BattleFact> list = battle.Facts.GetRange(start, battle.Facts.Count - start);
        queueEnd = Mathf.Max(queueEnd, fx);
        int i = 0;
        while (i < list.Count)
        {
            BattleFact head = list[i];
            if (head.Kind == "interrupt")
            {
                // An enemy's action count ran out mid-turn: it acts now, before the party's next card.
                BattleUnit who = head.Actor;
                float at = Mathf.Max(queueEnd, fx);
                At(at, () => { ShowBanner((who != null ? who.Name.ToUpperInvariant() : "ENEMY") + "  ACTS!", EnemyRed); Sfx("Sfx/enemy_turn", "status", .8f); });
                queueEnd = at + .8f;
                i++;
                continue;
            }
            if (IsSummonFact(head)) { ScheduleSummonFact(head); i++; continue; }
            int j = i + 1;
            while (j < list.Count && list[j].Actor == head.Actor && list[j].Card == head.Card && !IsSummonFact(list[j])) j++;
            ScheduleGroup(list.GetRange(i, j - i));
            i = j;
        }
    }

    private static bool Ranged(BattleUnit actor, BattleCard card)
    {
        return card.Magic || actor.Role == BattleRole.Ranger || actor.Role == BattleRole.Support;
    }

    private void ScheduleGroup(List<BattleFact> group)
    {
        BattleFact head = group[0];
        BattleUnit actor = head.Actor;
        BattleCard card = head.Card;
        bool tick = head.Kind == "poison" || head.Kind == "regen";
        bool offense = group.Exists(f => f.Kind == "damage" || f.Kind == "blocked");
        float t = Mathf.Max(queueEnd, fx);
        if (head.Kind == "epiphany")
        {
            At(t, () => ApplyFact(head));
            queueEnd = t + .9f;
            return;
        }
        bool payoff = false;      // an ultimate's video showed the wind-up: the field picks up on the pose it ended on
        MediaLibrary.Entry mediaCine = MediaCineFor(actor, card);
        VideoClip clip = mediaCine != null ? null : CinematicFor(actor, card);
        bool cine = actor != null && card != null && !actor.Enemy && (IsUltimate(card) || clip != null || mediaCine != null) && Cinematics != CinematicMode.Off;
        // The way in (BattleTransitions.cs): the summon rise, the push into the fighter and her light up to the cut.
        float cineFrom = t, cinePush = t, cutAt = t;
        if (cine) { cutAt = CineLeadIn(actor, t, out cinePush); t = cutAt; }
        if (cine && mediaCine != null)
        {
            // An imported cinematic: played by URL at real time, like the rendered ones (no match cut).
            BattleUnit who = actor; BattleCard which = card;
            string url = MediaLibrary.VideoUrl(mediaCine.key);
            float len = (mediaCine.length > .1f ? mediaCine.length : 3f) * speed;
            cinematicsSeen.Add(card.Id);
            At(t, () => { cutUnit = who; cutCard = which; cutClip = null; cutUrl = url; cutStart = fx; cutLen = len;
                PlayUltUrl(url, mediaCine.width, mediaCine.height); Signal(BattleAnimationPhase.Ultimate, who, null, which); });
            t += len;
        }
        else if (cine)
        {
            BattleUnit who = actor; BattleCard which = card;
            if (clip != null) cinematicsSeen.Add(card.Id);
            // Clips play at 1x whatever the battle speed: their length in battle time scales with the speed.
            float len = clip != null ? (float)clip.length * speed : CutLen;
            At(t, () => { cutUnit = who; cutCard = which; cutClip = clip; cutUrl = null; cutStart = fx; cutLen = len;
                if (clip != null) PlayUltClip(clip);
                Signal(BattleAnimationPhase.Ultimate, who, null, which); });
            t += len;
            payoff = IsUltimate(card) || IsDecree(card);
        }
        // Battle seconds from the action to its hit: a clip fighter's own contact frame, else the fixed beats.
        bool rangedAct = actor != null && card != null && Ranged(actor, card);
        float lead = actor != null && card != null ? ImpactLead(actor, card, offense, rangedAct) : .34f;
        MoveDef def = !tick ? MoveDefFor(card, actor) : null;
        float staged = -1f;
        if (payoff) lead = PayoffLead(actor, card);
        else if (def != null && def.stage && !cine && StageWanted(card))
        {
            // A skill staged in battle (BattleStage.cs): the camera pushes in, the clip plays large, slightly slowed.
            // In a summon battle the push starts on her circle, and the action waits until she has formed.
            lead = StageLead(actor, card, lead);
            float t0 = t;
            bool rise = FreshSummon(actor, t0);
            t += Mathf.Max(.2f, .25f * speed) * .5f + (rise ? SummonRiseTime * .7f : 0f);
            if (rise) SummonEarly(actor, t0, t);
            staged = BeginStage(actor, card, group, t0, t, t + (rangedAct ? ReleaseLead(actor, card, lead) : lead), t + lead, rangedAct);
            cinematicsSeen.Add(card.Id);
        }
        if (cine) staged = BeginCineStage(actor, card, group, cineFrom, cinePush, cutAt, t, t + lead, rangedAct);
        if (def != null)
            ScheduleMoveLayers(def, actor, card, group, t, t + (payoff ? 0f : rangedAct ? ReleaseLead(actor, card, lead) : lead), t + lead, rangedAct);
        else if (actor != null && card != null && !actor.Enemy)
        {
            BattleUnit fxTarget = group[0].Target;
            At(t + lead, () => SpawnMoveFx(card, actor, fxTarget));
        }
        float impact = t + 0.05f, actionAt = t;
        if (actor != null && card != null && !tick)
        {
            bool ranged = rangedAct, handed = payoff;
            impact = t + lead;
            List<BattleFact> snapshot = group;
            At(t, () => BeginAction(actor, card, snapshot, ranged, lead, handed));
        }
        for (int i = 0; i < group.Count; i++)
        {
            BattleFact fact = group[i];
            At(impact + i * 0.085f, () => ApplyFact(fact));
        }
        queueEnd = Mathf.Max(impact + group.Count * 0.085f + (offense ? 0.52f : tick ? 0.22f : 0.40f), staged);
        SummonForGroup(group, actionAt, lead, queueEnd);
    }

    // handed: picked up from an ultimate's video (the clip starts on its key pose, a melee fighter is already there).
    private void BeginAction(BattleUnit actor, BattleCard card, List<BattleFact> group, bool ranged, float lead, bool handed = false)
    {
        UnitVis v = V(actor);
        v.Banner = card.Name; v.BannerStart = fx; v.IntentSpent = true;
        Vector2 from = Foot(actor);
        Vector2 dest = Vector2.zero;
        bool damaging = group.Exists(f => f.Kind == "damage" || f.Kind == "blocked");
        v.Ranged = ranged; v.Impact = lead;
        v.LungeDur = v.Impact + 0.42f;
        v.Arrive = handed && !ranged && damaging;
        if (handed) { v.ClipFrom = 0f; v.ClipStart = fx; }
        // A party card with its own layers (moves.json) draws them instead of the generic orb and glows.
        bool layered = MoveDefFor(card, actor) != null;
        Signal(BattleAnimationPhase.Action, actor, group[0].Target, card);
        if (!ranged && damaging)
        {
            Vector2 sum = Vector2.zero; int n = 0;
            for (int i = 0; i < group.Count; i++) if ((group[i].Kind == "damage" || group[i].Kind == "blocked") && group[i].Target != null) { sum += Foot(group[i].Target); n++; }
            if (n > 0)
            {
                Vector2 aim = sum / n;
                float stand = n > 1 ? 260f : 110f + Slot(group[0].Target).W * 0.28f;
                float dx = aim.x - from.x;
                float step = Mathf.Sign(dx) * Mathf.Max(0f, Mathf.Abs(dx) - stand);
                dest = new Vector2(step, (aim.y - from.y) * 0.7f);
            }
        }
        v.LungeTo = dest; v.LungeStart = fx;
        HoldForStage(actor, v);
        Color color = ElementColor(card.Element);
        if (ranged && damaging && !layered)
        {
            Vector2 origin = Chest(actor);
            float release = ReleaseLead(actor, card, v.Impact);      // a clip fighter lets go on its release frame
            for (int i = 0; i < group.Count; i++)
                if ((group[i].Kind == "damage" || group[i].Kind == "blocked") && group[i].Target != null)
                    bolts.Add(new Bolt { From = origin, To = Chest(group[i].Target), Start = fx + release + i * 0.02f, Dur = v.Impact - release - 0.02f, Color = color });
            Spawn(0, origin, color, 210f, 0.36f);
            Spawn(1, origin, color, 150f, 0.38f);
            Spawn(5, Foot(actor), color, Slot(actor).W * 0.9f + 50f, 0.9f, UnityEngine.Random.Range(0f, 360f));
        }
        else if (!damaging && !layered)
        {
            Spawn(1, Foot(actor) + new Vector2(0f, -8f), color, 240f, 0.55f);
            Spawn(5, Foot(actor), color, Slot(actor).W * 0.95f + 60f, 1.05f, UnityEngine.Random.Range(0f, 360f));
            Burst(Chest(actor), color, 8, 30f, 120f, 5f, 10f, 0.8f, -90f);
        }
    }

    private void ApplyFact(BattleFact f)
    {
        BattleUnit target = f.Target;
        if (target == null) return;
        if (ApplyCznFact(f)) return;
        if (f.Kind == "damage" || f.Kind == "poison") Signal(BattleAnimationPhase.Hit, f.Actor, target, f.Card, f);
        else if (f.Kind == "heal" || f.Kind == "regen") Signal(BattleAnimationPhase.Heal, f.Actor, target, f.Card, f);
        else if (f.Kind == "status") Signal(BattleAnimationPhase.Status, f.Actor, target, f.Card, f);
        UnitVis v = V(target);
        SlotInfo s = Slot(target);
        Vector2 chest = Chest(target), head = Head(target);
        Color element = f.Card != null ? CardColor(f.Card, f.Actor) : Color.white;
        float jitter = UnityEngine.Random.Range(-26f, 26f);
        switch (f.Kind)
        {
            case "damage":
            case "poison":
            {
                bool poison = f.Kind == "poison";
                v.TargetHp = f.Hp; v.HurtStart = fx;
                v.HurtDir = f.Actor != null ? Mathf.Sign(Foot(target).x - Foot(f.Actor).x) : (target.Enemy ? 1f : -1f);
                Color number = poison ? new Color(0.78f, 0.5f, 1f) : f.Crit ? new Color(1f, 0.86f, 0.28f)
                    : target.Enemy ? Color.white : new Color(1f, 0.55f, 0.5f);
                Float(head + new Vector2(jitter, -6f), f.Amount.ToString(), f.Crit ? 76 : poison ? 42 : 56, number, 1.15f, 90f, jitter * 0.6f);
                if (!poison && f.Crit) Float(head + new Vector2(jitter, -64f), "CRITICAL", 24, new Color(1f, 0.86f, 0.28f), 1.0f, 60f);
                if (!poison && f.Element > 1.01f) Float(head + new Vector2(60f, 34f), "WEAK", 26, new Color(1f, 0.95f, 0.4f), 1.0f, 40f, 30f);
                else if (!poison && f.Element < 0.99f) Float(head + new Vector2(60f, 34f), "RESIST", 22, new Color(0.7f, 0.76f, 0.85f), 0.9f, 30f, 30f);
                float weight = Mathf.Clamp01(f.Amount / Mathf.Max(1f, target.MaxHp * 0.35f));
                if (poison) Burst(chest, new Color(0.78f, 0.5f, 1f), 8, 40f, 140f, 5f, 10f, 0.7f, -60f);
                else Impact(chest, element, f.Crit, weight, f.Card != null ? f.Card.Element : BattleElement.Neutral, !HasImpactLayer(f.Card, f.Actor));
                if (!target.Enemy && target.CollapseRounds > 0 && !v.CollapseShown)
                {
                    v.CollapseShown = true;
                    Float(head + new Vector2(0f, -110f), "BREAKDOWN", 34, new Color(1f, 0.4f, 0.85f), 1.6f, 34f);
                    Spawn(1, Foot(target), new Color(1f, 0.4f, 0.85f), 420f, 0.7f);
                }
                if (f.Fatal)
                {
                    Signal(BattleAnimationPhase.Down, f.Actor, target, f.Card, f);
                    v.DeathStart = fx;
                    Burst(chest, element, 26, 120f, 560f, 5f, 12f, 0.9f, 260f, true);
                    Spawn(1, Foot(target), Color.white, 520f, 0.55f);
                    shake = Mathf.Max(shake, 12f);
                }
                break;
            }
            case "heal":
            case "regen":
                v.TargetHp = f.Hp;
                if (f.Amount > 0) Float(head + new Vector2(jitter, -6f), "+" + f.Amount, 46, new Color(0.5f, 1f, 0.66f), 1.1f, 70f, jitter * 0.4f);
                Spawn(1, Foot(target) + new Vector2(0f, -6f), new Color(0.5f, 1f, 0.66f), 300f, 0.6f);
                Spawn(5, Foot(target), new Color(0.5f, 1f, 0.66f), s.W * 0.95f + 40f, 1.0f, UnityEngine.Random.Range(0f, 360f));
                Burst(chest, new Color(0.6f, 1f, 0.72f), 12, 30f, 130f, 5f, 11f, 1.0f, -150f);
                break;
            case "status":
            {
                bool buff = IsBuff(f.Status);
                Color c = buff ? new Color(0.5f, 0.95f, 0.85f) : new Color(1f, 0.5f, 0.55f);
                Float(head + new Vector2(0f, 6f), StatusLabel(f.Status), 25, c, 1.2f, 46f);
                Spawn(1, Foot(target) + new Vector2(0f, -6f), c, 280f, 0.6f);
                Spawn(5, Foot(target), c, s.W * 0.9f + 40f, 1.0f, UnityEngine.Random.Range(0f, 360f));
                Burst(chest, c, 9, 30f, 120f, 5f, 10f, 0.9f, buff ? -140f : 120f);
                break;
            }
        }
    }

    // ---- drawing the effect layers ------------------------------------------------------------

    private void DrawParticles()
    {
        for (int i = 0; i < particles.Count; i++)
        {
            Particle p = particles[i];
            float age = (fx - p.Born) / p.Life;
            float a = 1f - age; a *= a;
            Color c = new Color(p.Color.r, p.Color.g, p.Color.b, a);
            if (p.Spark)
                BattleGui.DrawStreak(p.Pos, p.Size * 3.6f * (1f - age * 0.5f), p.Size * 0.7f, Mathf.Atan2(p.Vel.y, p.Vel.x) * Mathf.Rad2Deg, c);
            else BattleGui.DrawGlow(p.Pos, p.Size * (1.6f - age * 0.6f), c);
        }
    }

    // under: the layers drawn beneath the units (BattleMoveFx); everything else is drawn over them.
    private void DrawEffects(bool under = false)
    {
        for (int i = 0; i < effects.Count; i++)
        {
            Effect e = effects[i];
            if (e.Under != under || fx < e.Born) continue;
            float t = (fx - e.Born) / e.Life;
            if (e.Kind == 13 && e.Sheet != null) DrawCharge(e, t);
            else if ((e.Kind == 14 || e.Kind == 15) && e.Sheet != null) DrawTravel(e);
            else if (e.Kind == 3)
                BattleGui.DrawSheet(BattleGui.SlashSheet, 8, Mathf.FloorToInt(t * 8f), e.Pos, e.Size, e.Angle, e.Color);
            else if (e.Kind == 4)
                BattleGui.DrawSheet(BattleGui.BurstSheet, 8, Mathf.FloorToInt(t * 8f), e.Pos, e.Size, e.Angle, e.Color);
            else if (e.Kind >= 7 && e.Kind <= 9)
            {
                Texture2D sheet = e.Kind == 7 ? BattleGui.GenSlash : e.Kind == 8 ? BattleGui.GenImpact : e.Sheet;
                BattleGui.DrawSheet(sheet, 8, Mathf.FloorToInt(t * 8f), e.Pos, e.Size, e.Angle, e.Color);
            }
            else if (e.Kind == 11 && e.Sheet != null) DrawMoveSheet(e, t);
            else if (e.Kind == 12 && e.Sheet != null) DrawImportedFx(e, t);
            else if (e.Kind == 10)
            {
                float w = e.Size, h = w * 704f / 1248f;
                BattleGui.DrawSheetRect(BattleGui.GenWarp, 8, Mathf.FloorToInt(t * 8f), new Rect(e.Pos.x - w * 0.5f, e.Pos.y - h * 0.5f, w, h),
                    new Color(e.Color.r, e.Color.g, e.Color.b, e.Color.a * (1f - t)));
            }
            else if (e.Kind == 5)
            {
                float a = t < 0.18f ? t / 0.18f : 1f - (t - 0.18f) / 0.82f;
                float r = e.Size * 0.5f * (0.72f + 0.28f * BattleGui.EaseOut(Mathf.Min(1f, t * 2.2f)));
                BattleGui.DrawGround(BattleGui.Rune, e.Pos + new Vector2(0f, -2f), r, e.Angle + t * 110f, 0.34f, new Color(e.Color.r, e.Color.g, e.Color.b, a * 0.95f));
            }
            else if (e.Kind == 6)
                BattleGui.DrawSpin(BattleGui.Sparkle, e.Pos, e.Size * (1f - t * 0.4f), e.Angle + t * 60f, new Color(e.Color.r, e.Color.g, e.Color.b, 1f - t));
            else if (e.Kind == 0) BattleGui.DrawGlow(e.Pos, e.Size * (0.5f + 0.5f * BattleGui.EaseOut(t)), new Color(e.Color.r, e.Color.g, e.Color.b, (1f - t) * (1f - t)));
            else if (e.Kind == 1)
            {
                float r = e.Size * (0.25f + 0.75f * BattleGui.EaseOut(t)) * 0.5f;
                BattleGui.Shockwave(e.Pos, r, new Color(e.Color.r, e.Color.g, e.Color.b, 1f - t));
            }
            else
            {
                float grow = BattleGui.EaseOut(Mathf.Clamp01(t * 2.4f));
                BattleGui.DrawStreak(e.Pos, e.Size * grow, 22f * (1f - t * 0.6f), e.Angle, new Color(e.Color.r, e.Color.g, e.Color.b, 1f - t * t));
            }
        }
        for (int i = 0; i < bolts.Count && !under; i++)
        {
            Bolt b = bolts[i];
            float t = (fx - b.Start) / Mathf.Max(0.05f, b.Dur);
            if (t < 0f || t > 1.05f) continue;
            float k = BattleGui.Ease(Mathf.Clamp01(t));
            Vector2 pos = Vector2.Lerp(b.From, b.To, k) + new Vector2(0f, -Mathf.Sin(k * Mathf.PI) * 46f);
            for (int s = 0; s < 6; s++)
            {
                float kk = Mathf.Max(0f, k - s * 0.035f);
                Vector2 trail = Vector2.Lerp(b.From, b.To, kk) + new Vector2(0f, -Mathf.Sin(kk * Mathf.PI) * 46f);
                BattleGui.DrawGlow(trail, 26f - s * 3f, new Color(b.Color.r, b.Color.g, b.Color.b, 0.55f - s * 0.08f));
            }
            BattleGui.DrawGlow(pos, 42f, new Color(b.Color.r, b.Color.g, b.Color.b, 0.9f));
            BattleGui.DrawGlow(pos, 20f, new Color(1f, 1f, 1f, 0.95f));
        }
    }

    private void DrawFloaters()
    {
        for (int i = 0; i < floaters.Count; i++)
        {
            Floater f = floaters[i];
            float age = fx - f.Born;
            float t = age / f.Life;
            float pop = age < 0.14f ? 1f + 0.55f * (1f - age / 0.14f) : 1f;
            float alpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
            // Drawn on the screen matrix at the camera's position for it, so numbers stay crisp when it zooms.
            Vector2 pos = FieldToScreen(f.Pos + f.Vel * BattleGui.EaseOut(t) * 0.9f);
            int size = Mathf.RoundToInt(f.Size * pop / 2f) * 2;
            Rect r = new Rect(pos.x - 200f, pos.y - size * 0.6f, 400f, size * 1.2f);
            BattleGui.Text(r, f.Text, size, BattleGui.Alpha(f.Color, alpha), TextAnchor.MiddleCenter, true, false, Mathf.Max(2f, size / 16f));
        }
    }

    private void DrawBanners()
    {
        for (int i = 0; i < banners.Count; i++)
        {
            BannerFx b = banners[i];
            float t = (fx - b.Start) / 1.05f;
            float slide = t < 0.2f ? 1f - BattleGui.EaseOut(t / 0.2f) : t > 0.8f ? -BattleGui.Ease((t - 0.8f) / 0.2f) : 0f;
            float alpha = t < 0.12f ? t / 0.12f : t > 0.86f ? (1f - t) / 0.14f : 1f;
            float h = 96f * (t < 0.14f ? BattleGui.EaseOut(t / 0.14f) : 1f);
            float cy = 318f;
            GUI.color = new Color(1, 1, 1, alpha);
            BattleGui.Fill(new Rect(0, cy - h * 0.5f, 1600, h), new Color(0.01f, 0.02f, 0.04f, 0.72f));
            BattleGui.Fill(new Rect(0, cy - h * 0.5f, 1600, 3f), BattleGui.Alpha(b.Color, 0.9f));
            BattleGui.Fill(new Rect(0, cy + h * 0.5f - 3f, 1600, 3f), BattleGui.Alpha(b.Color, 0.9f));
            BattleGui.Text(new Rect(slide * 900f, cy - 50f, 1600, 100), b.Text, 66, b.Color, TextAnchor.MiddleCenter, true, false, 3f);
            GUI.color = Color.white;
        }
    }
}
