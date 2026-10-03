using System.Collections.Generic;
using AdamsHaven.Tower;
using UnityEngine;
using static BattleGui;

// Chaos Zero Nightmare layer of the battle screen (BM 10.3.0):
//  - enemy action counts (badge beside the intent; each card played ticks it down, at 0 the enemy acts mid-turn)
//  - tenacity pips under enemy health and the BREAK stagger
//  - shields drawn over health bars; the free GUARD beside BASIC
//  - reserve partners: the +8% pairing and a once-per-battle ASSIST for 2 SP
//  - the CHAIN counter, the hand sweeping to the discard pile at the end of the turn, and the Epiphany choice
//  - sound routing: a move's own recorded/imported sound, element hits, shields, breaks, turns
public sealed partial class BattleMode
{
    private static readonly Color Mint = new Color(.45f, 1f, .78f);
    private int lastChain;
    private float chainAt = -9f;
    private BattleUnit assistUnit;
    private float assistUntil;
    private bool selectingAssist;
    private readonly HashSet<BattleUnit> ultReadySounded = new HashSet<BattleUnit>();
    private readonly Dictionary<string, AudioClip> moveSounds = new Dictionary<string, AudioClip>();

    private struct Discarded { public BattleCard Card; public Vector2 From; public float Angle, Scale, Start; }
    private readonly List<Discarded> discarding = new List<Discarded>();

    // ---- sound -----------------------------------------------------------------------------------------------

    // A recorded or imported clip when there is one, otherwise the fallback (synthesised) sound.
    private static void Sfx(string id, string fallback, float volume = 1f)
    {
        if (TowerAudio.Has(id)) TowerAudio.PlayVaried(id, volume, .04f);
        else if (!string.IsNullOrEmpty(fallback)) TowerAudio.PlayVaried(fallback, volume);
    }

    // A move's own sound: imported in the media library, else Resources/AdamsHaven/Audio/Moves/<card id>.
    private AudioClip MoveSound(BattleCard card)
    {
        if (card == null || string.IsNullOrEmpty(card.Id)) return null;
        var imported = MediaLibrary.AudioFor("card." + card.Id + ".sfx");
        if (imported != null) return imported;
        AudioClip clip;
        if (!moveSounds.TryGetValue(card.Id, out clip))
        {
            clip = Resources.Load<AudioClip>("AdamsHaven/Audio/Moves/" + card.Id);
            moveSounds[card.Id] = clip;
        }
        return clip;
    }

    private static string ElementName(BattleElement e) { return e.ToString().ToLowerInvariant(); }

    private void ActionSound(BattleUnit actor, BattleCard card)
    {
        var own = MoveSound(card);
        // An ultimate's own sound already played with its cut-in.
        if (own != null && IsUltimate(card) && Cinematics != CinematicMode.Off) return;
        // Recorded clips are loudness-matched; several stack on one action (move, hit, crit), so they sit below full.
        if (own != null) { TowerAudio.PlayClip(own, .7f, 1f + Random.Range(-.03f, .03f)); return; }
        if (actor != null && actor.Enemy && card != null && card.EffectivePower > 0)
        {
            string id = card.Magic ? "Sfx/enemy_magic" : actor.Boss || actor.Role == BattleRole.Tank ? "Sfx/enemy_slam"
                : actor.Role == BattleRole.Support ? "Sfx/enemy_bite" : "Sfx/enemy_claw";
            Sfx(id, Ranged(actor, card) ? null : "swing", .7f);
            return;
        }
        if (card != null && actor != null && !Ranged(actor, card)) TowerAudio.PlayVaried("swing", 0.6f);
    }

    private void HitSound(BattleFact fact)
    {
        if (fact == null) { TowerAudio.PlayVaried("hit", .75f); return; }
        if (fact.Kind == "poison") { TowerAudio.PlayVaried("status", 0.4f); return; }
        if (fact.Kind == "blocked") { Sfx("Sfx/shield_block", "status", .8f); return; }
        string element = "Sfx/hit_" + ElementName(fact.Card != null ? fact.Card.Element : BattleElement.Neutral);
        // Under a move's own recorded sound the shared impact is a quieter layer.
        float layer = fact.Card != null && MoveSound(fact.Card) != null ? .45f : .7f;
        if (fact.Crit) TowerAudio.PlayVaried("crit", .8f);
        Sfx(element, fact.Crit ? null : "hit", fact.Crit ? .5f : layer);
    }

    private void StatusSound(BattleFact fact)
    {
        string status = fact != null ? fact.Status : "";
        if (status == "Shield") Sfx("Sfx/shield_up", "status", .7f);
        else if (IsBuff(status)) Sfx("Sfx/buff", "status", .55f);
        else Sfx("Sfx/debuff", "status", .55f);
    }

    // A fighter's ultimate became ready: one bright ping.
    private void CheckUltReady()
    {
        if (battle == null || battle.Finished) return;
        foreach (BattleUnit u in battle.Allies)
        {
            bool ready = u.Alive && u.Ultimate >= 100 && battle.Sp >= battle.UltCost(u);
            if (ready && ultReadySounded.Add(u)) Sfx("Sfx/ult_ready", "chime", .5f);
            else if (!ready) ultReadySounded.Remove(u);
        }
    }

    // ---- plates ----------------------------------------------------------------------------------------------

    // Shield as a pale band over the health bar, with its value at the right end.
    private void DrawShieldOverlay(Rect bar, BattleUnit u)
    {
        float shield = u.StatusValue("Shield");
        if (shield <= 0f) return;
        float k = Mathf.Clamp01(shield / Mathf.Max(1f, u.MaxHp));
        Rect band = new Rect(bar.x, bar.y - 3f, Mathf.Max(6f, bar.width * k), 4f);
        Round(band, new Color(.85f, .95f, 1f, .95f), 2f);
        Rect tag = new Rect(bar.xMax + 4f, bar.y - 2f, 46f, bar.height + 4f);
        Round(tag, new Color(.05f, .12f, .2f, .92f), 6f);
        Outline(tag, new Color(.7f, .9f, 1f, .9f), 1.2f, 6f);
        Text(tag, Mathf.RoundToInt(shield).ToString(), 11, Color.white, TextAnchor.MiddleCenter, true);
    }

    // Tenacity: one pip per point; broken enemies show a cracked red bar.
    private void DrawTenacity(Rect r, BattleUnit u)
    {
        bool broken = u.HasStatus("Broken");
        if (broken)
        {
            float pulse = .6f + .4f * Mathf.Sin(Time.time * 9f);
            Round(r, new Color(1f, .3f, .25f, .85f * pulse), 3f);
            Text(new Rect(r.x, r.y - 4f, r.width, r.height + 8f), "BROKEN", 10, Color.white, TextAnchor.MiddleCenter, true, false, 1f);
            return;
        }
        int n = Mathf.Max(1, u.MaxTenacity);
        float gap = 2f, w = (r.width - gap * (n - 1)) / n;
        for (int i = 0; i < n; i++)
        {
            Rect pip = new Rect(r.x + i * (w + gap), r.y, w, r.height);
            Round(pip, i < u.Tenacity ? new Color(1f, .78f, .25f) : new Color(.12f, .12f, .16f, .9f), 2f);
        }
    }

    // The enemy's action count: how many more party cards before it acts. Red and pulsing on its last one.
    private void DrawActionBadge(Vector2 c, BattleUnit enemy)
    {
        if (enemy.ActionMax <= 0) return;
        bool last = enemy.ActionCount <= 1;
        float pulse = last ? .75f + .25f * Mathf.Sin(Time.time * 10f) : 1f;
        Disc(c, 17f, new Color(.03f, .04f, .08f, .95f));
        Ring(c, 16f, 2.2f, last ? new Color(1f, .35f, .3f, pulse) : new Color(.6f, .86f, 1f, .9f));
        // Ticks around the ring: one per remaining count.
        for (int i = 0; i < enemy.ActionMax; i++)
        {
            float a = (-90f + i * 360f / enemy.ActionMax) * Mathf.Deg2Rad;
            Vector2 p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 21f;
            Disc(p, 2.4f, i < enemy.ActionCount ? (last ? new Color(1f, .4f, .35f) : Ice) : new Color(.2f, .22f, .28f));
        }
        Text(new Rect(c.x - 16f, c.y - 13f, 32f, 26f), enemy.ActionCount.ToString(), 18, last ? new Color(1f, .7f, .65f) : Color.white, TextAnchor.MiddleCenter, true, false, 1.5f);
    }

    // Under a reserve tile: who they partner and the ASSIST button.
    private void DrawPartnerControls(BattleUnit reserve, Rect tile)
    {
        BattleUnit partner = battle.PartneredBy(reserve);
        if (partner == null) return;
        Text(new Rect(tile.x - 4f, tile.yMax + 10f, tile.width + 8f, 14f), "+" + partner.Name.Split(' ')[0].ToUpperInvariant(), 9,
            reserve.Alive ? Mint : new Color(.5f, .5f, .55f), TextAnchor.MiddleCenter, true);
        bool can = battle.CanPartnerAssist(reserve) && !Busy;
        string label = reserve.PartnerUsed ? "USED" : "ASSIST";
        if (MiniButton(new Rect(tile.x - 2f, tile.yMax + 25f, tile.width + 4f, 22f), label, can, selectingAssist && selectedActor == reserve, Gold, -1f, 10))
        {
            BattleCard move = battle.PartnerMove(reserve);
            if (move == null) return;
            ClearSelection(); focusUnit = null; swapFrom = null; swapReserve = null;
            if (move.Target == BattleTarget.Enemy || move.Target == BattleTarget.Ally)
            {
                selected = move; selectedActor = reserve; selectedUltimate = -1; selectingAssist = true;
                Toast(reserve.Name.Split(' ')[0] + " is ready to step in: choose a target.");
            }
            else if (!Perform(move, reserve, null, -1, true)) Toast("The assist cannot be used now.");
        }
    }

    // ---- hand flow ---------------------------------------------------------------------------------------------

    // Cards that left the hand without being played (end of turn) sweep to the discard pile.
    private void NoteDiscarded(BattleCard card, CardVis cv)
    {
        if (cv == null || cv.AppearAt > fx) return;
        for (int i = 0; i < flying.Count; i++) if (flying[i].Card == card) return;
        discarding.Add(new Discarded { Card = card, From = cv.Pos, Angle = cv.Angle, Scale = cv.Scale, Start = fx });
    }

    private void DrawDiscarding()
    {
        for (int i = discarding.Count - 1; i >= 0; i--)
        {
            Discarded d = discarding[i];
            float t = (fx - d.Start) / .42f;
            if (t >= 1f) { discarding.RemoveAt(i); continue; }
            float k = EaseOut(Mathf.Clamp01(t));
            Vector2 pos = Vector2.Lerp(d.From, DiscardPos, k) + new Vector2(0f, -Mathf.Sin(k * Mathf.PI) * 60f);
            DrawCard(d.Card, pos, Mathf.Lerp(d.Angle, 30f, k), Mathf.Lerp(d.Scale, .3f, k), 1f - k * k, false, false, false);
        }
    }

    // ---- turn order (top bar) ----------------------------------------------------------------------------------

    private static readonly Rect CommanderPlate = new Rect(1116f, 12f, 250f, 88f);
    private float enemyPhaseUntil = -1f;
    private readonly List<BattleUnit> order = new List<BattleUnit>();

    // Who acts next: enemies by their action count (fewest cards left first; the faster one on a tie). Every card the
    // party plays ticks each count down; at 0 that enemy acts at once. Whoever is left acts at END TURN in speed order.
    private List<BattleUnit> TurnOrder()
    {
        order.Clear();
        foreach (BattleUnit e in battle.Enemies) if (e.Alive && e.ActionMax > 0) order.Add(e);
        order.Sort((a, b) =>
        {
            int c = a.ActionCount.CompareTo(b.ActionCount);
            return c != 0 ? c : b.EffectiveSpeed.CompareTo(a.EffectiveSpeed);
        });
        return order;
    }

    private bool EnemyPhase { get { return fx < enemyPhaseUntil && Busy; } }

    // The turn panel (top centre): whose turn it is and the CHAIN on the first row around the round number, the fight's
    // title on the second, and what END TURN will throw out on the third.
    private static readonly Rect TurnPanel = new Rect(392f, 8f, 566f, 66f);

    private void DrawTurnPanel(Rect r, string header)
    {
        bool enemyTurn = EnemyPhase;
        Color accent = enemyTurn ? EnemyRed : Gold;
        Round(r, new Color(.02f, .04f, .07f, .86f), 18f);
        Outline(r, BattleGui.Alpha(accent, .65f), 1.6f, 18f);
        // Row 1: turn | ROUND N | chain.
        Text(new Rect(r.x + 16f, r.y + 4f, 170f, 30f), enemyTurn ? "ENEMY TURN" : "YOUR TURN", 19, accent, TextAnchor.MiddleLeft, true, false, 1.5f);
        Text(new Rect(r.center.x - 100f, r.y + 2f, 200f, 32f), "ROUND " + battle.Round, 26, Color.white, TextAnchor.MiddleCenter, true, false, 1.5f);
        if (!enemyTurn && battle.CardsThisTurn > 0)
        {
            // CHAIN: cards played this turn, popping on each new card.
            float age = fx - chainAt;
            float pop = age < .18f ? 1f + .45f * (1f - age / .18f) : 1f;
            Rect chainRect = new Rect(r.xMax - 136f, r.y + 4f, 120f, 30f);
            Matrix4x4 keep = GUI.matrix;
            ScaleAround(pop, chainRect.center);
            Text(chainRect, "CHAIN x" + battle.CardsThisTurn, 18, Gold, TextAnchor.MiddleRight, true, false, 1.5f);
            GUI.matrix = keep;
        }
        // Row 2: the fight.
        Text(new Rect(r.x + 12f, r.y + 32f, r.width - 24f, 16f), header, 12, Ice, TextAnchor.MiddleCenter, true);
        // Row 3: the hand, and what END TURN will throw out (every card not played, unless it Retains).
        string line;
        Color lineColor = new Color(.8f, .86f, .95f);
        if (enemyTurn) line = "enemies act in speed order";
        else
        {
            int inHand = battle.Hand.Count, toss = 0;
            foreach (BattleCard card in battle.Hand) if (!card.Retain) toss++;
            line = inHand + " card" + (inHand == 1 ? "" : "s") + " in hand" + (toss > 0 ? "  -  " + toss + " discarded at END TURN" : "");
            if (toss > 0) lineColor = new Color(1f, .8f, .65f);
        }
        Text(new Rect(r.x + 12f, r.y + 48f, r.width - 24f, 15f), line, 11, lineColor, TextAnchor.MiddleCenter, true);
    }

    // Right of the turn panel: the next enemy to act and how many cards away it is.
    private void DrawNextBox(Rect r)
    {
        List<BattleUnit> next = TurnOrder();
        Round(r, new Color(.02f, .04f, .07f, .86f), 14f);
        Outline(r, BattleGui.Alpha(EnemyRed, .55f), 1.6f, 14f);
        if (next.Count == 0) { Text(r, "NO FOES", 12, EnemyRed, TextAnchor.MiddleCenter, true); return; }
        BattleUnit u = next[0];
        Rect face = new Rect(r.x + 6f, r.y + 5f, 40f, 40f);
        Round(face, new Color(.02f, .03f, .06f), 8f);
        DrawPortrait(new Rect(face.x + 2f, face.y + 2f, 36f, 36f), Spr(u.Art), .46f);
        bool last = u.ActionCount <= 1;
        Outline(face, last ? new Color(1f, .35f, .3f, .7f + .3f * Mathf.Sin(Time.time * 9f)) : BattleGui.Alpha(EnemyRed, .7f), 1.6f, 8f);
        Text(new Rect(r.x + 52f, r.y + 2f, r.width - 56f, 18f), "NEXT TO ACT", 10, EnemyRed, TextAnchor.MiddleLeft, true);
        string when = u.ActionCount <= 0 ? "now" : u.ActionCount == 1 ? "after 1 card" : "after " + u.ActionCount + " cards";
        Text(new Rect(r.x + 52f, r.y + 18f, r.width - 56f, 28f), when, 14, last ? new Color(1f, .7f, .65f) : Color.white, TextAnchor.MiddleLeft, true);
    }

    // Under the turn panel: the whole order. YOU first (you act until you end the turn), then each enemy with its
    // count, then the enemy commander, who always acts at END TURN.
    private void DrawTurnOrder(Vector2 center)
    {
        List<BattleUnit> list = TurnOrder();
        bool commander = battle.EnemySummoner != null && battle.EnemySummoner.Alive;
        const float chip = 38f, gap = 8f, youW = 52f;
        int n = list.Count + (commander ? 1 : 0);
        float total = youW + gap + n * (chip + gap) - gap;
        float x = center.x - total * .5f, y = center.y - chip * .5f;
        Rect back = new Rect(x - 10f, y - 5f, total + 20f, chip + 10f);
        Round(back, new Color(.02f, .04f, .07f, .72f), 12f);
        // YOU
        Rect you = new Rect(x, y, youW, chip);
        bool yourTurn = !EnemyPhase;
        Round(you, yourTurn ? new Color(.25f, .2f, .06f, .95f) : new Color(.06f, .07f, .1f, .9f), 8f);
        Outline(you, BattleGui.Alpha(Gold, yourTurn ? .95f : .35f), yourTurn ? 2f : 1.2f, 8f);
        Text(you, "YOU", 13, yourTurn ? Gold : new Color(.6f, .6f, .65f), TextAnchor.MiddleCenter, true);
        x += youW + gap;
        for (int i = 0; i < n; i++)
        {
            BattleUnit u = i < list.Count ? list[i] : battle.EnemySummoner;
            bool isCommander = i >= list.Count;
            Rect r = new Rect(x, y, chip, chip);
            Text(new Rect(x - gap - 2f, y, gap + 4f, chip), ">", 12, new Color(.6f, .66f, .75f), TextAnchor.MiddleCenter, true);
            Round(r, new Color(.02f, .03f, .06f), 8f);
            Color was = GUI.color;
            if (u.ActedThisRound && !isCommander) GUI.color = new Color(.6f, .6f, .65f, was.a);
            DrawPortrait(new Rect(r.x + 2f, r.y + 2f, chip - 4f, chip - 4f), Spr(u.Art), isCommander ? .3f : .46f);
            GUI.color = was;
            bool last = !isCommander && u.ActionCount <= 1;
            Color edge = isCommander ? Violet : last ? new Color(1f, .35f, .3f, .7f + .3f * Mathf.Sin(Time.time * 9f)) : BattleGui.Alpha(EnemyRed, .75f);
            Outline(r, edge, last ? 2.2f : 1.4f, 8f);
            // Count badge (END for the commander).
            Vector2 b = new Vector2(r.xMax - 4f, r.yMax - 4f);
            Disc(b, isCommander ? 13f : 10f, new Color(.03f, .04f, .08f, .97f));
            Ring(b, isCommander ? 13f : 10f, 1.5f, edge);
            Text(new Rect(b.x - 14f, b.y - 10f, 28f, 20f), isCommander ? "END" : u.ActionCount.ToString(), isCommander ? 8 : 12, Color.white, TextAnchor.MiddleCenter, true);
            if (r.Contains(mouse) && Event.current.type == EventType.Repaint) hoverUnit = u;
            x += chip + gap;
        }
    }

    // ---- epiphany -----------------------------------------------------------------------------------------------

    private void DrawEpiphany()
    {
        modalDrawing = true;
        BattleCard card = battle.EpiphanyCard;
        float t = Time.time;
        Fill(new Rect(-VW, -VH, VW * 3f, VH * 3f), new Color(0, 0, .03f, .72f));
        DrawSpin(Rays, new Vector2(800f, 250f), 1300f, t * 12f, new Color(.9f, .85f, 1f, .25f));
        Text(new Rect(0, 40f, VW, 70f), "EPIPHANY", 60, new Color(1f, .93f, .7f), TextAnchor.MiddleCenter, true, false, 3f);
        Text(new Rect(0, 104f, VW, 28f), card.Name.ToUpperInvariant() + " can grow. Choose one; it lasts for the rest of the expedition.", 17, Color.white, TextAnchor.MiddleCenter, true);
        DrawCard(card, new Vector2(800f, 290f), Mathf.Sin(t * 1.4f) * 2f, .95f, 1f, true, false, false);
        int n = battle.EpiphanyOptions.Count;
        float w = 330f, gap = 26f, x0 = 800f - (n * w + (n - 1) * gap) * .5f;
        for (int i = 0; i < n; i++)
        {
            string mod = battle.EpiphanyOptions[i];
            Rect r = new Rect(x0 + i * (w + gap), 460f, w, 160f);
            bool hover = r.Contains(mouse);
            DrawGlow(new Rect(r.x - 20f, r.y - 20f, r.width + 40f, r.height + 40f), new Color(1f, .9f, .6f, hover ? .35f : .12f));
            Round(r, new Color(.04f, .06f, .11f, .97f), 16f);
            Outline(r, hover ? Gold : BattleGui.Alpha(Gold, .55f), hover ? 3f : 1.6f, 16f);
            Text(new Rect(r.x, r.y + 18f, r.width, 40f), BattleCatalog.EpiphanyName(mod).ToUpperInvariant(), 30, Gold, TextAnchor.MiddleCenter, true, false, 2f);
            Text(new Rect(r.x + 18f, r.y + 66f, r.width - 36f, 70f), BattleCatalog.EpiphanyText(mod), 17, Color.white, TextAnchor.UpperCenter, false, true);
            if (Press(r))
            {
                battle.ChooseEpiphany(i);
                Sfx("Sfx/epiphany", "chime", .9f);
                AfterAction();
            }
        }
        modalDrawing = false;
    }

    // ---- facts -------------------------------------------------------------------------------------------------

    // The CZN fact kinds the timeline plays: breaks, fully blocked hits, shields from cards, epiphanies.
    private bool ApplyCznFact(BattleFact f)
    {
        BattleUnit target = f.Target;
        Vector2 head = Head(target), chest = Chest(target);
        switch (f.Kind)
        {
            case "break":
                Float(head + new Vector2(0f, -70f), "BREAK!", 54, new Color(1f, .78f, .3f), 1.3f, 50f);
                Spawn(1, chest, new Color(1f, .8f, .35f), 520f, .55f);
                Spawn(0, chest, Color.white, 420f, .3f);
                Burst(chest, new Color(1f, .85f, .4f), 28, 200f, 640f, 5f, 12f, .8f, 380f, true);
                shake = Mathf.Max(shake, 14f);
                hitStop = Mathf.Max(hitStop, .1f);
                Sfx("Sfx/break", "crit", .95f);
                return true;
            case "blocked":
                V(target).HurtStart = fx; V(target).HurtDir = f.Actor != null ? Mathf.Sign(Foot(target).x - Foot(f.Actor).x) : 1f;
                Float(head + new Vector2(0f, -6f), "BLOCK " + f.Amount, 40, new Color(.75f, .92f, 1f), 1f, 60f);
                Spawn(1, chest, new Color(.7f, .9f, 1f), 300f, .4f);
                Signal(BattleAnimationPhase.Hit, f.Actor, target, f.Card, f);
                return true;
            case "shield":
                Float(head + new Vector2(0f, 6f), "+SHIELD", 26, new Color(.75f, .92f, 1f), 1.1f, 46f);
                Spawn(1, Foot(target) + new Vector2(0f, -6f), new Color(.7f, .9f, 1f), 300f, .6f);
                Sfx("Sfx/shield_up", "status", .7f);
                return true;
            case "epiphany":
                Float(head + new Vector2(0f, -80f), "EPIPHANY", 40, new Color(1f, .93f, .7f), 1.4f, 40f);
                Burst(chest, new Color(1f, .95f, .75f), 22, 60f, 260f, 5f, 11f, 1.1f, -120f);
                Spawn(6, chest, new Color(1f, .95f, .8f), 240f, .9f);
                return true;
        }
        return false;
    }
}
