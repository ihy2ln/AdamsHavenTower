using System;
using System.Collections.Generic;
using UnityEngine;
using static BattleGui;

// Battle Mode screen. AdamsHavenPrototype only launches it and receives its result; the rules live
// in BattleRules/BattleCatalog, the replay timeline in BattleFx, and drawing helpers in BattleGui.
// Layout follows a Chaos Zero Nightmare style split: a painted stage with chibi fighters and
// monster cutouts, a fanned hand of full-model cards below it, party tiles on the left, and a
// round end-turn button on the right.
public sealed partial class BattleMode : MonoBehaviour
{
    private const float VW = 1600f, VH = 900f;
    private const float FlyLen = 0.42f;
    private const float CardW = 172f, CardH = 258f;
    private static readonly Vector2 DrawPilePos = new Vector2(1408f, 846f);
    private static readonly Vector2 DiscardPos = new Vector2(1490f, 846f);
    private static readonly Vector2 EndTurnPos = new Vector2(1500f, 722f);

    private static readonly Color Ink = new Color(.025f, .045f, .075f, .94f);
    private static readonly Color Panel = new Color(.03f, .055f, .09f, .86f);
    private static readonly Color Gold = new Color(.97f, .82f, .48f);
    private static readonly Color Ice = new Color(.60f, .86f, 1f);
    private static readonly Color EnemyRed = new Color(1f, .55f, .50f);
    private static readonly Color Violet = new Color(.72f, .46f, 1f);

    private struct SlotInfo { public Vector2 Foot; public float H, W; public Cutout Sprite; }

    private sealed class CardVis
    {
        public Vector2 Pos, Rest;
        public float Angle, Scale = 0.4f, Alpha, AppearAt, RestAngle;
    }

    private sealed class IntentInfo
    {
        public BattleCard Card;
        public int Damage, Kind;   // Kind: 0 attack, 1 guard/buff, 2 debuff
        public float Element = 1f;
        public bool Area;
        public BattleUnit Target;
    }

    private BattleState battle;
    private Action<bool, int> leave;
    private BattleCard selected;
    private BattleUnit selectedActor, chooseUltimateFor, swapFrom, swapReserve, focusUnit;
    private int selectedUltimate = -1;
    private int floor, reward;
    private bool auto, confirmWithdraw, showLog, fastForward;
    private float autoTimer, toastStart = -9f;
    private string toast = "";
    private Vector2 logScroll;
    private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
    private readonly Dictionary<BattleUnit, SlotInfo> slots = new Dictionary<BattleUnit, SlotInfo>();
    private readonly Dictionary<BattleCard, CardVis> cardVis = new Dictionary<BattleCard, CardVis>();
    private readonly Dictionary<BattleUnit, IntentInfo> intents = new Dictionary<BattleUnit, IntentInfo>();
    private readonly List<BattleCard> handOrder = new List<BattleCard>();
    private readonly List<BattleUnit> drawOrder = new List<BattleUnit>();
    private BattleCard hoverCard;
    private BattleUnit hoverUnit;
    private Vector2 mouse;
#if UNITY_EDITOR
    public string DebugSummary()
    {
        if (battle == null) return "no battle";
        return "hover=" + (hoverCard != null ? hoverCard.Name : "-") + " selected=" + (selected != null ? selected.Name : "-")
            + " flying=" + flying.Count + " hand=" + handOrder.Count + " busy=" + Busy + " fx=" + fx + " queueEnd=" + queueEnd
            + " mouse=" + mouse + " screen=" + Screen.width + "x" + Screen.height + CardDump();
    }

    // Test hooks: place the virtual pointer and click, so input can be exercised without a real mouse.
    private Vector2 debugMouse;
    private bool debugMouseOn, debugClick;
    public void DebugMouse(Vector2 virtualPoint) { debugMouse = virtualPoint; debugMouseOn = true; }
    public void DebugClick() { debugClick = true; }

    // Test hook: fires a fighter's first ultimate (the first fighter, or the one with unitId),
    // optionally leaving every enemy on 1 HP first. Single-target ultimates hit the first living enemy.
    public void DebugUltimate(bool killAll, string unitId = null)
    {
        if (battle == null || battle.Allies.Count == 0) return;
        BattleUnit actor = battle.Allies[0];
        if (unitId != null) actor = battle.Allies.Find(u => u.Id == unitId);
        if (actor == null) return;
        actor.Ultimate = 100;
        if (battle.Sp < BattleState.UltimateSpCost) battle.Sp = BattleState.UltimateSpCost;
        if (killAll) foreach (BattleUnit enemy in battle.Enemies) enemy.Hp = 1;
        SelectUltimate(actor, 0);
        if (selected != null && selectedActor == actor) Commit(battle.Enemies.Find(e => e.Alive));
    }

    public void DebugReadyUltimate()
    {
        if (battle == null || battle.Allies.Count == 0) return;
        battle.Allies[0].Ultimate = 100;
        if (battle.Sp < BattleState.UltimateSpCost) battle.Sp = BattleState.UltimateSpCost;
    }

    // Test hook: advances the presentation clock without needing editor frames.
    public void DebugAdvance(float seconds)
    {
        if (battle == null) return;
        for (float t = 0f; t < seconds; t += 1f / 60f)
        {
            TickFx(1f / 60f);
            LayoutField();
            UpdateHand(1f / 60f);
            if (!Busy) RefreshIntents();
        }
    }

    private string CardDump()
    {
        string text = "";
        for (int i = 0; i < handOrder.Count; i++)
        {
            CardVis cv = cardVis[handOrder[i]];
            text += " | " + handOrder[i].Name + " pos=" + cv.Pos + " rest=" + cv.Rest + " ang=" + cv.Angle + " scale=" + cv.Scale + " alpha=" + cv.Alpha;
        }
        return text;
    }
#endif
    private bool pressed, rightPressed, used, modalDrawing;
    private float[] moteSeed;
    private float speed = 1f;
    private BattleUnit popupUnit;
    private Rect popupRect, popupTile;

    // ---- setup -------------------------------------------------------------------------------

    public void Begin(int towerFloor, Action<bool, int> onLeave)
    {
        List<BattleUnit> party = BattleCatalog.Party();
        Begin(towerFloor, party.GetRange(0, 3), party.GetRange(3, 3), onLeave);
    }

    // Expedition entry: the caller supplies the party (already scaled by gear and current HP) and reads
    // the units' Hp back after onLeave. Text on the result screen names where the party returns to.
    public string ReturnLabel = "RETURN TO TOWER";

    public void Begin(int towerFloor, List<BattleUnit> field, List<BattleUnit> reserve, Action<bool, int> onLeave)
    {
        BattleGui.Build();
        ResetFx();
        cardVis.Clear(); handOrder.Clear(); slots.Clear(); drawOrder.Clear(); intents.Clear();
        ClearSelection();
        swapFrom = null; swapReserve = null; focusUnit = null; popupUnit = null;
        hoverCard = null; hoverUnit = null; showLog = false; confirmWithdraw = false;
        auto = false; autoTimer = 0f; fastForward = false; speed = 1f;
        floor = towerFloor;
        leave = onLeave;
        var party = new List<BattleUnit>(field);
        party.AddRange(reserve);
        battle = new BattleState(Environment.TickCount, field, reserve,
            BattleCatalog.Encounter(floor), BattleCatalog.Deck(party),
            BattleCatalog.JD(), BattleCatalog.EnemyCommander(floor));
        reward = Mathf.Abs(floor) >= 8 ? 300 : Mathf.Abs(floor) >= 3 ? 160 : 80;
        moteSeed = new float[48 * 4];
        for (int i = 0; i < moteSeed.Length; i++) moteSeed[i] = UnityEngine.Random.value;
        foreach (BattleUnit unit in battle.Allies) V(unit);
        foreach (BattleUnit unit in battle.Reserves) V(unit);
        foreach (BattleUnit unit in battle.Enemies) V(unit);
        V(battle.Summoner);
        if (battle.EnemySummoner != null) V(battle.EnemySummoner);
        factsSeen = battle.FactSerial;
        announcedRound = battle.Round;
        PreloadArt();
        BuildFieldRigs();
        queueEnd = 1.0f;
        ShowBanner("ROUND 1", Gold);
        SyncHand();
        Toast("Pick a card, then a highlighted target. Every fighter has a free basic attack.");
    }

    // Trimming reads pixels back through a render target, so do it here rather than mid-OnGUI.
    private void PreloadArt()
    {
        foreach (BattleUnit unit in battle.Allies) Spr(unit.Art);
        foreach (BattleUnit unit in battle.Reserves) Spr(unit.Art);
        foreach (BattleUnit unit in battle.Enemies) Spr(unit.Art);
        Spr(battle.Summoner.Art);
        if (battle.EnemySummoner != null) Spr(battle.EnemySummoner.Art);
    }

    private void Toast(string text) { toast = text; toastStart = Time.time; }

    private Texture2D Art(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        Texture2D texture;
        if (!textures.TryGetValue(id, out texture))
        {
            texture = Resources.Load<Texture2D>("AdamsHaven/" + id);
            textures.Add(id, texture);
        }
        return texture;
    }

    private Cutout Spr(string id) { return Trim(Art(id)); }

    // ---- per-frame update --------------------------------------------------------------------

    private void Update()
    {
        if (battle == null) return;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        TickFx(dt);
        LayoutField();
        TickFieldRigs();
        UpdateHand(dt);
        if (!Busy) RefreshIntents();
        if (battle.Finished || !auto || showLog || confirmWithdraw || Busy) return;
        autoTimer -= dt;
        if (autoTimer > 0f) return;
        autoTimer = 0.3f;
        if (!AutoStep()) DoEndTurn();
    }

    private bool AutoStep()
    {
        if (battle == null) return false;
        for (int i = 0; i < battle.Hand.Count; i++)
        {
            BattleCard card = battle.Hand[i];
            BattleUnit actor = battle.OwnerOf(card);
            if (!battle.CanPay(card, actor)) continue;
            BattleUnit target = AutomaticTarget(card, actor);
            if (battle.IsTarget(card, actor, target)) return Perform(card, actor, target);
        }
        for (int i = 0; i < battle.Allies.Count; i++)
        {
            BattleUnit actor = battle.Allies[i];
            BattleCard basic = BattleCatalog.Basic(actor);
            BattleUnit target = AutomaticTarget(basic, actor);
            if (battle.CanPay(basic, actor) && battle.IsTarget(basic, actor, target)) return Perform(basic, actor, target);
        }
        return false;
    }

    private BattleUnit AutomaticTarget(BattleCard card, BattleUnit actor)
    {
        if (card.Target == BattleTarget.Self) return actor;
        if (card.Target == BattleTarget.Ally)
        {
            BattleUnit weakest = null;
            for (int i = 0; i < battle.Allies.Count; i++)
            {
                BattleUnit unit = battle.Allies[i];
                if (!unit.Alive) continue;
                if (weakest == null || (float)unit.Hp / unit.MaxHp < (float)weakest.Hp / weakest.MaxHp) weakest = unit;
            }
            return weakest;
        }
        if (card.Target == BattleTarget.Enemy)
        {
            for (int i = 0; i < battle.Enemies.Count; i++)
                if (battle.Exposed(battle.Enemies[i])) return battle.Enemies[i];
            return battle.EnemySummoner != null && battle.EnemySummoner.Alive ? battle.EnemySummoner : null;
        }
        return null;
    }

    // ---- actions (every rules mutation goes through here so the timeline sees it) ----------

    private bool Perform(BattleCard card, BattleUnit actor, BattleUnit target, int ultimate = -1)
    {
        CardVis cv = null;
        bool fromHand = ultimate < 0 && battle.Hand.Contains(card) && cardVis.TryGetValue(card, out cv);
        float before = Mathf.Max(queueEnd, fx);
        queueEnd = before;
        if (fromHand)
        {
            flying.Add(new FlyCard { Card = card, From = cv.Pos, Angle = cv.Angle, Scale = cv.Scale, Start = fx });
            queueEnd = fx + FlyLen;
        }
        bool ok = ultimate >= 0 ? battle.TryUltimate(actor, ultimate, target) : battle.TryPlay(card, target);
        if (!ok)
        {
            if (fromHand) flying.RemoveAt(flying.Count - 1);
            queueEnd = before;
            return false;
        }
        if (fromHand) Signal(BattleAnimationPhase.PlayCard, battle.Summoner, target, card);
        AfterAction();
        return true;
    }

    private void AfterAction()
    {
        Ingest();
        if (!battle.Finished && battle.Round != announcedRound)
        {
            announcedRound = battle.Round;
            float t = Mathf.Max(queueEnd, fx);
            string label = "ROUND " + battle.Round;
            At(t, () => ShowBanner(label, Gold));
            queueEnd = t + 0.95f;
        }
        SyncHand();
    }

    private void DoEndTurn()
    {
        if (battle == null || battle.Finished || Busy) return;
        ClearSelection(); focusUnit = null; swapFrom = null; swapReserve = null;
        float t = Mathf.Max(queueEnd, fx);
        At(t, () => ShowBanner("ENEMY TURN", EnemyRed));
        queueEnd = t + 0.95f;
        battle.EndTurn();
        AfterAction();
    }

    private void DoDecree()
    {
        if (battle.Finished || Busy || battle.Sp < BattleState.SpMax) return;
        ClearSelection();
        float t = Mathf.Max(queueEnd, fx);
        At(t, () => ShowBanner("JD'S DECREE", Gold));
        queueEnd = t + 0.95f;
        battle.TrySummonerUltimate();
        AfterAction();
    }

    private void Select(BattleCard card)
    {
        if (battle.Finished || Busy) return;
        if (selected == card && card != null && battle.Hand.Contains(card)) { ClearSelection(); return; }
        BattleUnit actor = battle.OwnerOf(card);
        string why = Reason(card, actor);
        if (why.Length > 0) { Toast(why + "."); return; }
        selected = card; selectedActor = actor; selectedUltimate = -1;
        chooseUltimateFor = null; focusUnit = null; swapFrom = null; swapReserve = null;
        if (card.Target == BattleTarget.Self) Commit(actor);
        else if (card.Target == BattleTarget.None || card.Target == BattleTarget.AllAllies || card.Target == BattleTarget.AllEnemies) Commit(null);
    }

    private void SelectUltimate(BattleUnit actor, int choice)
    {
        List<BattleCard> choices = BattleCatalog.Ultimates(actor);
        if (choice >= choices.Count || Busy) return;
        selected = choices[choice]; selectedActor = actor; selectedUltimate = choice;
        chooseUltimateFor = null; focusUnit = null;
        if (selected.Target == BattleTarget.AllAllies || selected.Target == BattleTarget.AllEnemies) Commit(null);
    }

    private void Commit(BattleUnit target)
    {
        if (selected == null || Busy || battle.Finished) return;
        bool played = Perform(selected, selectedActor, target, selectedUltimate);
        if (!played) Toast(target == null ? "This action cannot be played now." : "A taunting unit protects that target.");
        else ClearSelection();
    }

    private void ClearSelection()
    {
        selected = null; selectedActor = null; selectedUltimate = -1; chooseUltimateFor = null;
    }

    private string Reason(BattleCard card, BattleUnit actor)
    {
        if (battle.Finished) return "The battle is over";
        if (card.Kind == BattleCardKind.Summoner)
            return battle.Cp < Math.Max(1, card.Ap) ? "JD needs " + Math.Max(1, card.Ap) + " CP" : "";
        if (actor == null || !actor.Alive) return "Its owner is down";
        if (actor.Strained(card)) return actor.Name.Split(' ')[0] + " is in breakdown";
        if (actor.Ap < card.Ap) return actor.Name.Split(' ')[0] + " has no AP left";
        if (actor.Ep < card.Ep) return actor.Name.Split(' ')[0] + " needs more EP";
        return "";
    }

    private void ExitBattle()
    {
        if (battle == null) return;
        Action<bool, int> callback = leave; leave = null;
        bool won = battle.Victory;
        battle = null;
        ReleaseUltClip();
        if (callback != null) callback(won, won ? reward : 0);
    }

    // ---- hand model --------------------------------------------------------------------------

    private void SyncHand()
    {
        List<BattleCard> gone = null;
        foreach (KeyValuePair<BattleCard, CardVis> pair in cardVis)
            if (!battle.Hand.Contains(pair.Key)) { if (gone == null) gone = new List<BattleCard>(); gone.Add(pair.Key); }
        if (gone != null) for (int i = 0; i < gone.Count; i++) cardVis.Remove(gone[i]);
        int fresh = 0;
        for (int i = 0; i < battle.Hand.Count; i++)
        {
            if (cardVis.ContainsKey(battle.Hand[i])) continue;
            cardVis[battle.Hand[i]] = new CardVis { Pos = DrawPilePos, Rest = DrawPilePos, Angle = 24f, Scale = 0.35f,
                AppearAt = Mathf.Max(fx, queueEnd) + fresh * 0.09f };
            fresh++;
        }
        if (fresh > 0)
            At(Mathf.Max(fx, queueEnd), () => Signal(BattleAnimationPhase.DrawCards, battle.Summoner, null, null));
    }

    private int OwnerRank(BattleCard card)
    {
        if (card.Kind == BattleCardKind.Summoner) return 9;
        for (int i = 0; i < battle.Allies.Count; i++) if (battle.Allies[i].Id == card.Owner) return i;
        return 8;
    }

    private void UpdateHand(float dt)
    {
        handOrder.Clear();
        for (int i = 0; i < battle.Hand.Count; i++)
        {
            CardVis cv;
            if (cardVis.TryGetValue(battle.Hand[i], out cv) && cv.AppearAt <= fx) handOrder.Add(battle.Hand[i]);
        }
        // Group by owner in lane order; insertion sort keeps draw order inside a group.
        for (int i = 1; i < handOrder.Count; i++)
        {
            BattleCard item = handOrder[i];
            int rank = OwnerRank(item), j = i - 1;
            while (j >= 0 && OwnerRank(handOrder[j]) > rank) { handOrder[j + 1] = handOrder[j]; j--; }
            handOrder[j + 1] = item;
        }
        int n = handOrder.Count;
        float mid = (n - 1) * 0.5f;
        float cardScale = n > 9 ? 0.9f : 1f;
        float spacing = n <= 1 ? 0f : Mathf.Min(148f, 930f / (n - 1)) * cardScale;
        int hoverIndex = hoverCard != null ? handOrder.IndexOf(hoverCard) : -1;
        float k = 1f - Mathf.Exp(-dt * 13f);
        for (int i = 0; i < n; i++)
        {
            CardVis cv = cardVis[handOrder[i]];
            float offset = i - mid;
            float x = 836f + offset * spacing;
            float y = 790f + offset * offset * 2.2f;
            float angle = Mathf.Clamp(offset * 3.3f, -17f, 17f);
            float scale = cardScale;
            cv.Rest = new Vector2(x, y); cv.RestAngle = angle;
            if (hoverIndex >= 0 && i != hoverIndex)
            {
                int gap = i - hoverIndex;
                x += Mathf.Sign(gap) * (Mathf.Abs(gap) == 1 ? 46f : Mathf.Abs(gap) == 2 ? 20f : 6f);
            }
            if (i == hoverIndex) { y -= 96f; angle = 0f; scale *= 1.14f; }
            if (handOrder[i] == selected) { y -= 70f; angle = 0f; scale *= 1.06f; }
            cv.Pos = Vector2.Lerp(cv.Pos, new Vector2(x, y), k);
            cv.Angle = Mathf.Lerp(cv.Angle, angle, k);
            cv.Scale = Mathf.Lerp(cv.Scale, scale, k);
            cv.Alpha = Mathf.MoveTowards(cv.Alpha, 1f, dt * 5f);
        }
    }

    // ---- field layout ------------------------------------------------------------------------

    private static float EnemyHeight(string species)
    {
        switch (species)
        {
            case "eclipse_core_golem": return 420f;
            case "moonstone_ravager": return 390f;
            case "amberhide_grazer": return 350f;
            case "thorncrystal_stalker": return 345f;
            case "obsidian_talon": return 330f;
            case "flintjaw_skitterer": return 262f;
            case "shardling_sprout": return 240f;
        }
        return 320f;
    }

    private void AddSlot(BattleUnit unit, Vector2 foot, float height)
    {
        Cutout sprite = Spr(unit.Art);
        Texture2D clip = AnimeClip(unit.Id, "idle");
        if (clip) sprite = AnimeCell(clip, 0);
        float aspect = sprite.Valid ? sprite.Aspect : 0.75f;
        slots[unit] = new SlotInfo { Foot = foot, H = height, W = height * aspect, Sprite = sprite };
    }

    private void LayoutField()
    {
        slots.Clear();
        float[] laneX = { 604f, 408f, 212f };
        float[] laneY = { 584f, 536f, 576f };
        float[] laneH = { 268f, 252f, 262f };
        for (int i = 0; i < battle.Allies.Count; i++)
        {
            BattleUnit unit = battle.Allies[i];
            int lane = Mathf.Clamp(unit.Lane, 0, 2);
            AddSlot(unit, new Vector2(laneX[lane], laneY[lane]), laneH[lane]);
        }
        List<BattleUnit> foes = new List<BattleUnit>(battle.Enemies);
        foes.Sort((a, b) => a.Lane.CompareTo(b.Lane));
        float total = 0f;
        for (int i = 0; i < foes.Count; i++)
        {
            Cutout s = Spr(foes[i].Art);
            total += EnemyHeight(foes[i].Species) * (s.Valid ? s.Aspect : 0.85f) * 0.84f;
        }
        float fit = Mathf.Min(1f, 640f / Mathf.Max(1f, total));
        float cursor = 912f;
        float[] footY = { 588f, 538f, 580f, 542f, 582f, 540f };
        for (int i = 0; i < foes.Count; i++)
        {
            Cutout s = Spr(foes[i].Art);
            float h = EnemyHeight(foes[i].Species) * fit;
            float w = h * (s.Valid ? s.Aspect : 0.85f);
            AddSlot(foes[i], new Vector2(cursor + w * 0.5f, footY[i % footY.Length]), h);
            cursor += w * 0.84f;
        }
        AddSlot(battle.Summoner, new Vector2(115f, 450f), 205f);
        if (battle.EnemySummoner != null) AddSlot(battle.EnemySummoner, new Vector2(1490f, 462f), 244f);
        drawOrder.Clear();
        foreach (KeyValuePair<BattleUnit, SlotInfo> pair in slots) drawOrder.Add(pair.Key);
        drawOrder.Sort((a, b) => slots[a].Foot.y.CompareTo(slots[b].Foot.y));
    }

    private SlotInfo Slot(BattleUnit unit)
    {
        SlotInfo s;
        if (unit != null && slots.TryGetValue(unit, out s)) return s;
        return new SlotInfo { Foot = new Vector2(VW * 0.5f, 560f), H = 240f, W = 180f };
    }

    private Vector2 Foot(BattleUnit unit) { return Slot(unit).Foot; }
    private Vector2 Chest(BattleUnit unit) { SlotInfo s = Slot(unit); return s.Foot + new Vector2(0f, -s.H * 0.52f); }
    private Vector2 Head(BattleUnit unit) { SlotInfo s = Slot(unit); return s.Foot + new Vector2(0f, -s.H * 0.98f); }

    private void RefreshIntents()
    {
        intents.Clear();
        for (int i = 0; i < battle.Enemies.Count; i++)
        {
            BattleUnit enemy = battle.Enemies[i];
            BattleCard card;
            if (!enemy.Alive || !battle.Intents.TryGetValue(enemy.Id, out card)) continue;
            BattleUnit target;
            battle.IntentTargets.TryGetValue(enemy.Id, out target);
            IntentInfo info = new IntentInfo { Card = card, Target = target, Area = card.Target == BattleTarget.AllEnemies };
            if (card.EffectivePower > 0)
            {
                BattleUnit aim = target;
                if (aim == null) for (int a = 0; a < battle.Allies.Count && aim == null; a++) if (battle.Allies[a].Alive) aim = battle.Allies[a];
                info.Damage = battle.PreviewDamage(card, enemy, aim, out info.Element);
                info.Kind = 0;
            }
            else info.Kind = !string.IsNullOrEmpty(card.Status) && !IsBuff(card.Status) ? 2 : 1;
            intents[enemy] = info;
            V(enemy).IntentSpent = false;
        }
    }

    // ---- input helpers -----------------------------------------------------------------------

    private bool Press(Rect rect)
    {
        if (!pressed || used || !rect.Contains(mouse)) return false;
        if ((showLog || confirmWithdraw) && !modalDrawing) return false;
        used = true;
        return true;
    }

    private bool Over(Rect rect)
    {
        if ((showLog || confirmWithdraw) && !modalDrawing) return false;
        return rect.Contains(mouse);
    }

    private bool MiniButton(Rect r, string label, bool enabled, bool active, Color accent, float fill = -1f, int size = 13)
    {
        bool hover = enabled && Over(r);
        CommandSurface(r, accent, enabled, hover, active, fill);
        Text(r, label, size, enabled ? Color.white : new Color(.5f, .54f, .6f), TextAnchor.MiddleCenter, true);
        return enabled && Press(r);
    }

    // ---- OnGUI -------------------------------------------------------------------------------

    private void OnGUI()
    {
        if (battle == null) return;
        BattleGui.Build();
        Event e = Event.current;
        float ppp = 1f;
        float sw = Screen.width / ppp, sh = Screen.height / ppp;
        float scale = Mathf.Min(sw / VW, sh / VH);
        Vector2 offset = new Vector2((sw - VW * scale) * 0.5f, (sh - VH * scale) * 0.5f);
        if (e.type != EventType.Layout) mouse = (e.mousePosition - offset) / scale;
        pressed = e.type == EventType.MouseDown && e.button == 0;
        rightPressed = e.type == EventType.MouseDown && e.button == 1;
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) rightPressed = true;
#if UNITY_EDITOR
        if (debugMouseOn) mouse = debugMouse;
        if (debugClick && e.type == EventType.Repaint) { pressed = true; debugClick = false; }
#endif
        used = false; modalDrawing = false;

        Matrix4x4 old = GUI.matrix;
        Matrix4x4 baseMatrix = Matrix4x4.TRS(new Vector3(offset.x, offset.y, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
        Vector2 jolt = new Vector2(Mathf.Sin(fx * 93f), Mathf.Cos(fx * 71f)) * shake;
        bool paint = e.type == EventType.Repaint;

        FindHover();
        GUI.matrix = baseMatrix * Matrix4x4.Translate(new Vector3(jolt.x, jolt.y, 0f));
        if (paint) { DrawBackground(); DrawAmbient(); }
        DrawField();
        if (paint) { DrawParticles(); DrawEffects(); DrawFloaters(); }
        GUI.matrix = baseMatrix;
        if (paint) DrawVignette();
        DrawTopBar();
        DrawPartyTiles();
        DrawReserves();
        DrawPiles();
        DrawEndTurn();
        DrawHand();
        if (chooseUltimateFor != null && chooseUltimateFor == popupUnit) DrawUltimateChoices(popupUnit, popupTile);
        else popupUnit = null;
        DrawHint();
        if (paint) { DrawFlyingCards(); DrawCutIn(); DrawBanners(); DrawToast(); }
        DrawTooltip();
        if (battle.Finished && !Busy) DrawResult();
        if (showLog) DrawLog();
        if (confirmWithdraw) DrawWithdrawConfirm();

        if (pressed && !used && !showLog && !confirmWithdraw && (selected != null || focusUnit != null || swapFrom != null || swapReserve != null))
        { ClearSelection(); focusUnit = null; swapFrom = null; swapReserve = null; used = true; }
        if (rightPressed) { ClearSelection(); focusUnit = null; swapFrom = null; swapReserve = null; }
        if (used && (e.type == EventType.MouseDown)) e.Use();
        GUI.matrix = old;
    }

    private void FindHover()
    {
        BattleCard previous = hoverCard;
        hoverCard = null; hoverUnit = null;
        if (showLog || confirmWithdraw) return;
        if (popupUnit != null && popupRect.Contains(mouse)) return;
        // Keep the current hover while the pointer is over either its lifted or resting shape,
        // otherwise a lifted card would drop away as soon as the pointer reached its lower half.
        if (previous != null && handOrder.Contains(previous))
        {
            CardVis pv = cardVis[previous];
            if (InsideCard(mouse, pv.Pos, pv.Angle, pv.Scale) || InsideCard(mouse, pv.Rest, pv.RestAngle, 1f)) { hoverCard = previous; return; }
        }
        for (int i = handOrder.Count - 1; i >= 0; i--)
        {
            CardVis cv = cardVis[handOrder[i]];
            if (InsideCard(mouse, cv.Rest, cv.RestAngle, 1f)) { hoverCard = handOrder[i]; return; }
        }
        for (int i = drawOrder.Count - 1; i >= 0; i--)
        {
            BattleUnit u = drawOrder[i];
            if (!ShownAlive(u) && u.Enemy) continue;
            SlotInfo s = slots[u];
            Rect hit = new Rect(s.Foot.x - s.W * 0.36f, s.Foot.y - s.H, s.W * 0.72f, s.H + 48f);
            if (hit.Contains(mouse)) { hoverUnit = u; return; }
        }
        // The commander plates in the top corners are targetable too.
        if (new Rect(16f, 12f, 356f, 88f).Contains(mouse)) hoverUnit = battle.Summoner;
        else if (battle.EnemySummoner != null && new Rect(1010f, 12f, 356f, 88f).Contains(mouse)) hoverUnit = battle.EnemySummoner;
    }

    private static bool InsideCard(Vector2 point, Vector2 center, float angle, float scale)
    {
        Vector2 p = point - center;
        float rad = -angle * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
        p = new Vector2(p.x * c - p.y * s, p.x * s + p.y * c) / Mathf.Max(0.1f, scale);
        return Mathf.Abs(p.x) <= CardW * 0.5f && Mathf.Abs(p.y) <= CardH * 0.5f;
    }

    // ---- stage -------------------------------------------------------------------------------

    private void DrawBackground()
    {
        Fill(new Rect(0, 0, VW, VH), new Color(.02f, .03f, .05f));
        Texture2D bg = Art("Battle/silverwood_battle_v1");
        float zoom = 1.06f + Mathf.Sin(Time.time * 0.17f) * 0.006f;
        Vector2 par = new Vector2(Mathf.Clamp((mouse.x - 800f) / 800f, -1f, 1f) * -16f, Mathf.Clamp((mouse.y - 450f) / 450f, -1f, 1f) * -8f);
        Rect r = new Rect(-VW * (zoom - 1f) * 0.5f + par.x, -VH * (zoom - 1f) * 0.5f + par.y - 10f, VW * zoom, VH * zoom);
        if (bg != null) GUI.DrawTexture(r, bg, ScaleMode.ScaleAndCrop);
        Fill(new Rect(-40, -40, VW + 80, VH + 80), new Color(.02f, .03f, .08f, .16f));
        // Team light pools give each side ground to stand on.
        DrawGlow(new Rect(20f, 500f, 780f, 250f), new Color(.20f, .52f, .95f, .20f));
        DrawGlow(new Rect(820f, 500f, 780f, 250f), new Color(.95f, .28f, .30f, .17f));
    }

    private void DrawAmbient()
    {
        float t = Time.time;
        // Lanterns and the corruption crystal breathe.
        DrawGlow(new Vector2(172f, 150f), 120f + Mathf.Sin(t * 6.1f) * 6f, new Color(1f, .68f, .28f, .40f + Mathf.Sin(t * 7.3f) * .06f));
        DrawGlow(new Vector2(1420f, 236f), 120f + Mathf.Sin(t * 5.7f + 1f) * 6f, new Color(1f, .68f, .28f, .40f + Mathf.Sin(t * 6.4f) * .06f));
        DrawGlow(new Vector2(662f, 126f), 190f, new Color(.65f, .35f, 1f, .20f + Mathf.Sin(t * 1.3f) * .07f));
        for (int i = 0; i < 4; i++)
        {
            float drift = Mathf.Repeat(t * (8f + i * 3f) + i * 400f, 2400f) - 500f;
            DrawGlow(new Rect(drift, 350f + i * 12f, 800f, 130f), new Color(.55f, .62f, .95f, .10f));
        }
        if (moteSeed == null) return;
        for (int i = 0; i < 48; i++)
        {
            float a = moteSeed[i * 4], b = moteSeed[i * 4 + 1], c = moteSeed[i * 4 + 2], d = moteSeed[i * 4 + 3];
            float x = Mathf.Repeat(a * VW + t * (6f + c * 16f) + Mathf.Sin(t * .6f + d * 9f) * 20f, VW + 60f) - 30f;
            float y = Mathf.Repeat(b * 640f - t * (4f + d * 12f), 640f) + 60f;
            float twinkle = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(t * (0.8f + c) + d * 20f));
            DrawGlow(new Vector2(x, y), 5f + c * 9f, new Color(.72f, .82f, 1f, .22f * twinkle));
        }
    }

    private void DrawVignette()
    {
        GUI.DrawTexture(new Rect(0, 0, VW, VH), Vignette);
        GradientDown(new Rect(0, 0, VW, 150f), new Color(0f, 0f, .02f, .62f));
        GradientUp(new Rect(0, 610f, VW, 290f), new Color(0f, .01f, .03f, .82f));
    }

    // ---- units -------------------------------------------------------------------------------

    private static void LungeOffset(UnitVis v, float now, bool enemy, out Vector2 off, out float lean)
    {
        off = Vector2.zero; lean = 0f;
        float t = now - v.LungeStart;
        if (t < 0f || t > v.LungeDur) return;
        float impact = v.Impact;
        float back = enemy ? 1f : -1f;
        if (v.Ranged || v.LungeTo == Vector2.zero)
        {
            float k = t < impact ? Ease(t / impact) : 1f - Ease((t - impact) / 0.32f);
            off = new Vector2(back * 16f * k, -Mathf.Sin(Mathf.Clamp01(t / v.LungeDur) * Mathf.PI) * 14f);
            lean = -back * 5f * k;
            return;
        }
        float fwd = Mathf.Sign(v.LungeTo.x == 0f ? -back : v.LungeTo.x);
        float windEnd = Mathf.Max(0.05f, impact - 0.10f);
        Vector2 wind = new Vector2(back * 28f, 0f);
        if (t < windEnd) { float k = Ease(t / windEnd); off = wind * k; lean = -fwd * 7f * k; }
        else if (t < impact)
        {
            float k = EaseOut((t - windEnd) / Mathf.Max(0.01f, impact - windEnd));
            off = Vector2.Lerp(wind, v.LungeTo, k); lean = Mathf.Lerp(-fwd * 7f, fwd * 11f, k);
        }
        else if (t < impact + 0.12f) { off = v.LungeTo; lean = fwd * 11f; }
        else
        {
            float k = Ease((t - impact - 0.12f) / Mathf.Max(0.05f, v.LungeDur - impact - 0.12f));
            off = Vector2.Lerp(v.LungeTo, Vector2.zero, k); lean = fwd * 11f * (1f - k);
        }
    }

    private bool Offered(BattleUnit unit)
    {
        return selected != null && battle.IsTarget(selected, selectedActor, unit);
    }

    private void DrawField()
    {
        bool paint = Event.current.type == EventType.Repaint;
        for (int i = 0; i < drawOrder.Count; i++)
        {
            BattleUnit unit = drawOrder[i];
            SlotInfo s = slots[unit];
            if (paint) DrawUnitBody(unit, s);
        }
        for (int i = 0; i < drawOrder.Count; i++)
        {
            BattleUnit unit = drawOrder[i];
            SlotInfo s = slots[unit];
            if (!ShownAlive(unit) && unit.Enemy) continue;
            if (paint) DrawUnitPlate(unit, s);
        }
        if (paint) { DrawIntents(); DrawPreviews(); }
        HandleFieldClicks();
        DrawFocusBar();
    }

    private void DrawUnitBody(BattleUnit u, SlotInfo s)
    {
        UnitVis v = V(u);
        s.Sprite = AnimeFrame(u, v, s.Sprite);
        bool alive = ShownAlive(u);
        float death = v.DeathStart >= 0f ? fx - v.DeathStart : -1f;
        if (u.Enemy && death > 0.85f) return;
        Vector2 off; float lean;
        LungeOffset(v, fx, u.Enemy, out off, out lean);
        float ht = fx - v.HurtStart;
        float hurt = ht >= 0f && ht < 0.36f ? 1f - ht / 0.36f : 0f;
        off.x += v.HurtDir * 24f * hurt * hurt + Mathf.Sin(ht * 74f) * 5f * hurt;
        float phase = Time.time * (u.Enemy ? 1.5f : 2.0f) + u.Lane * 1.7f + (Mathf.Abs(u.Id.GetHashCode()) % 7);
        float bob = Mathf.Sin(phase) * (u.Enemy ? 3.5f : 3f);
        float breathe = 1f + Mathf.Sin(phase * .7f) * .013f;
        float alpha = 1f;
        Color tint = Color.white;
        if (death >= 0f && u.Enemy) { alpha = 1f - death / 0.85f; off.y += death * 26f; off.x += Mathf.Sin(death * 60f) * 4f * (1f - death); }
        else if (!alive) { tint = new Color(.55f, .55f, .62f); alpha = .55f; }
        if (hurt > 0f) tint = Color.Lerp(tint, new Color(1f, .35f, .35f), Mathf.Clamp01(hurt * 1.4f));
        bool offered = Offered(u);
        bool covered = selected != null && selected.Target == BattleTarget.Enemy && u.Enemy && alive && !offered && u != battle.EnemySummoner;
        Vector2 foot = s.Foot + off;
        // Contact shadow stays on the ground while the body lunges.
        DrawGlow(new Rect(foot.x - s.W * 0.46f, s.Foot.y - 16f, s.W * 0.92f, 40f), new Color(0, 0, 0, .62f * alpha));
        // Target / state rings on the ground.
        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 6f);
        if (offered)
        {
            DrawGlow(new Rect(s.Foot.x - s.W * 0.7f, s.Foot.y - s.H * 0.9f, s.W * 1.4f, s.H * 1.1f), new Color(.30f, .95f, .68f, .20f * pulse));
            DrawGround(Rune, new Vector2(s.Foot.x, s.Foot.y + 2f), Mathf.Clamp(s.W * 0.62f, 90f, 190f), Time.time * 38f, 0.34f, new Color(.45f, 1f, .78f, .95f * pulse));
            Outline(new Rect(s.Foot.x - s.W * 0.42f, s.Foot.y - 12f, s.W * 0.84f, 30f), new Color(.45f, 1f, .78f, .55f * pulse), 2f, 15f);
        }
        else if (u == hoverUnit && selected == null)
            Outline(new Rect(s.Foot.x - s.W * 0.44f, s.Foot.y - 12f, s.W * 0.88f, 32f), new Color(1f, 1f, 1f, .55f), 2f, 16f);
        if (!u.Enemy && u.AwakeningReady && alive)
            Outline(new Rect(s.Foot.x - s.W * 0.46f, s.Foot.y - 13f, s.W * 0.92f, 34f), new Color(1f, .82f, .3f, .9f * pulse), 3f, 17f);
        if (!u.Enemy && u.CollapseRounds > 0 && alive)
            Outline(new Rect(s.Foot.x - s.W * 0.46f, s.Foot.y - 13f, s.W * 0.92f, 34f), new Color(1f, .35f, .8f, .9f * pulse), 3f, 17f);
        if (selectedActor == u && selected != null)
            Outline(new Rect(s.Foot.x - s.W * 0.46f, s.Foot.y - 13f, s.W * 0.92f, 34f), new Color(1f, .9f, .5f, .95f), 3f, 17f);
        if (focusUnit == u) Outline(new Rect(s.Foot.x - s.W * 0.46f, s.Foot.y - 13f, s.W * 0.92f, 34f), Ice, 3f, 17f);

        float hh = s.H * breathe, ww = s.W * breathe;
        Rect r = new Rect(foot.x - ww * 0.5f, foot.y - hh + bob, ww, hh);
        // Two faint earlier poses make the placeholder cutout read as a fast dash.
        float dashAge = fx - v.LungeStart;
        if (!v.Ranged && v.LungeTo != Vector2.zero && dashAge > Mathf.Max(.05f, v.Impact - .10f)
            && dashAge < v.Impact + .08f && s.Sprite.Valid && !fieldRigs.ContainsKey(u))
        {
            Color trail = ElementColor(u.Element);
            Color savedColor = GUI.color;
            for (int echo = 2; echo >= 1; echo--)
            {
                Vector2 older; float olderLean;
                LungeOffset(v, fx - echo * .035f, u.Enemy, out older, out olderLean);
                Vector2 priorFoot = s.Foot + older;
                Rect prior = new Rect(priorFoot.x - ww * .5f, priorFoot.y - hh + bob, ww, hh);
                GUI.color = new Color(trail.r, trail.g, trail.b, echo == 1 ? .27f : .13f);
                DrawSprite(prior, s.Sprite);
            }
            GUI.color = savedColor;
        }
        Matrix4x4 keep = GUI.matrix;
        RotateAround(lean, new Vector2(foot.x, foot.y));
        Color before = GUI.color;
        GUI.color = new Color(tint.r, tint.g, tint.b, alpha * (covered ? 0.72f : 1f));
        if (!DrawFieldRig(u, foot, s.H) && s.Sprite.Valid) DrawSprite(r, s.Sprite);
        GUI.color = before;
        GUI.matrix = keep;
    }

    private void DrawUnitPlate(BattleUnit u, SlotInfo s)
    {
        UnitVis v = V(u);
        bool alive = ShownAlive(u);
        bool commander = u == battle.Summoner || u == battle.EnemySummoner;
        float bw = u.Enemy ? Mathf.Clamp(s.W * 0.66f, 138f, 196f) : 156f;
        float cx = s.Foot.x, y = s.Foot.y;
        string label = u == battle.Summoner ? "JD  -  SUMMONER" : u == battle.EnemySummoner ? "VALE COMMANDER"
            : u.Enemy ? u.Name : u.Name.Split(' ')[0];
        float a = alive ? 1f : 0.45f;
        Color old = GUI.color; GUI.color = new Color(1, 1, 1, a);
        Text(new Rect(cx - 130f, y + 5f, 260f, 22f), label, u.Enemy ? 16 : 17, u.Enemy ? EnemyRed : Ice, TextAnchor.MiddleCenter, true, false, 1.5f);
        Rect bar = new Rect(cx - bw * 0.5f, y + 29f, bw, 14f);
        float max = Mathf.Max(1f, u.MaxHp);
        Color fill = u.Enemy ? new Color(.86f, .30f, .30f) : new Color(.28f, .78f, .88f);
        Bar(bar, v.ShownHp / max, v.GhostHp / max, fill, new Color(1f, .88f, .7f, .9f));
        Text(new Rect(bar.x, bar.y - 1f, bar.width, bar.height + 2f), Mathf.CeilToInt(v.ShownHp) + " / " + u.MaxHp, 11, Color.white, TextAnchor.MiddleCenter, true, false, 1f);
        float chipY = y + 48f;
        if (!u.Enemy && !commander)
        {
            Rect sb = new Rect(bar.x, y + 46f, bw, 6f);
            Color sc = u.CollapseRounds > 0 ? new Color(1f, .3f, .75f) : u.Stress >= 75 ? new Color(.95f, .4f, .45f) : new Color(.6f, .4f, .9f);
            Bar(sb, u.Stress / 100f, 0f, sc, sc);
            chipY = y + 56f;
        }
        DrawChips(u, cx - bw * 0.5f, chipY, bw);
        GUI.color = old;
        // Card-name banner while the unit acts.
        float bt = fx - v.BannerStart;
        if (bt >= 0f && bt < 1.0f && v.Banner.Length > 0)
        {
            float ba = bt < 0.15f ? bt / 0.15f : bt > 0.75f ? (1f - bt) / 0.25f : 1f;
            Vector2 head = Head(u);
            Rect pill = new Rect(cx - 110f, head.y - 78f - (u.Enemy ? 34f : 0f), 220f, 30f);
            Round(pill, new Color(.03f, .05f, .09f, .88f * ba), 15f);
            Outline(pill, BattleGui.Alpha(u.Enemy ? EnemyRed : Gold, .95f * ba), 2f, 15f);
            Text(pill, v.Banner, 16, new Color(1f, 1f, 1f, ba), TextAnchor.MiddleCenter, true);
        }
    }

    private static string ChipLabel(string status)
    {
        switch (status)
        {
            case "AttackUp": return "ATK+";
            case "AttackDown": return "ATK-";
            case "DefenseUp": return "DEF+";
            case "DefenseDown": return "DEF-";
            case "Slow": return "SLOW";
            case "Haste": return "HASTE";
            case "Poison": return "PSN";
            case "Regen": return "RGN";
            case "Taunt": return "TAUNT";
            case "IceCounter": return "CTR";
        }
        return status.Length > 4 ? status.Substring(0, 4).ToUpperInvariant() : status.ToUpperInvariant();
    }

    private void DrawChips(BattleUnit u, float x, float y, float width)
    {
        float cx = x, cy = y;
        for (int i = 0; i < u.Statuses.Count; i++)
        {
            BattleStatus st = u.Statuses[i];
            if (st.Name == "Taunt" && u.InnateTaunt) continue;
            string text = ChipLabel(st.Name) + " " + st.Turns;
            float w = 24f + text.Length * 6.4f;
            if (cx + w > x + width + 24f) { cx = x; cy += 22f; }
            bool buff = IsBuff(st.Name);
            Color c = buff ? new Color(.40f, .92f, .78f) : new Color(1f, .46f, .55f);
            Rect r = new Rect(cx, cy, w, 19f);
            Round(r, new Color(.02f, .04f, .07f, .9f), 6f);
            Outline(r, BattleGui.Alpha(c, .9f), 1.5f, 6f);
            Text(r, text, 11, c, TextAnchor.MiddleCenter, true);
            cx += w + 4f;
        }
        if (u.Taunting)
        {
            Rect r = new Rect(cx, cy, 52f, 19f);
            if (cx + 52f > x + width + 24f) { r.x = x; r.y = cy + 22f; }
            Round(r, new Color(.02f, .04f, .07f, .9f), 6f);
            Outline(r, new Color(1f, .8f, .35f, .9f), 1.5f, 6f);
            Text(r, "TAUNT", 11, Gold, TextAnchor.MiddleCenter, true);
        }
    }

    private void DrawPreviews()
    {
        if (selected == null || selectedActor == null || selected.EffectivePower <= 0f) return;
        bool area = selected.Target == BattleTarget.AllEnemies;
        for (int i = 0; i < battle.Enemies.Count + 1; i++)
        {
            BattleUnit u = i < battle.Enemies.Count ? battle.Enemies[i] : battle.EnemySummoner;
            if (u == null || !u.Alive) continue;
            bool show = area ? u != battle.EnemySummoner : u == hoverUnit && Offered(u);
            if (!show) continue;
            float element;
            int damage = battle.PreviewDamage(selected, selectedActor, u, out element);
            bool lethal = damage >= u.Hp;
            Vector2 head = Head(u);
            Rect pill = new Rect(head.x - 62f, head.y - 62f - Mathf.Sin(Time.time * 5f) * 3f, 124f, 40f);
            Round(pill, new Color(.03f, .04f, .08f, .94f), 20f);
            Outline(pill, lethal ? new Color(1f, .35f, .35f) : element > 1.01f ? new Color(1f, .9f, .35f) : Ice, 2.4f, 20f);
            Text(pill, "-" + damage, 26, lethal ? new Color(1f, .5f, .5f) : Color.white, TextAnchor.MiddleCenter, true, false, 2f);
            string tag = lethal ? "LETHAL" : element > 1.01f ? "WEAK" : element < 0.99f ? "RESIST" : "";
            if (tag.Length > 0) Text(new Rect(pill.x, pill.yMax + 1f, pill.width, 18f), tag, 13, lethal ? new Color(1f, .5f, .5f) : element > 1.01f ? new Color(1f, .95f, .4f) : new Color(.7f, .76f, .85f), TextAnchor.MiddleCenter, true, false, 1.5f);
        }
    }

    private void DrawIntents()
    {
        if (Busy && intents.Count == 0) return;
        for (int i = 0; i < battle.Enemies.Count; i++)
        {
            BattleUnit enemy = battle.Enemies[i];
            IntentInfo info;
            if (!enemy.Alive || !intents.TryGetValue(enemy, out info) || V(enemy).IntentSpent) continue;
            SlotInfo s = Slot(enemy);
            Vector2 head = Head(enemy);
            float bob = Mathf.Sin(Time.time * 3f + i) * 3f;
            float py = head.y - 34f + bob;
            float w = info.Kind == 0 ? 116f : 132f;
            Rect pill = new Rect(head.x - w * 0.5f, py - 17f, w, 34f);
            Color accent = info.Kind == 0 ? new Color(1f, .40f, .38f) : info.Kind == 1 ? new Color(.45f, .85f, 1f) : new Color(.85f, .5f, 1f);
            Round(pill, new Color(.03f, .04f, .08f, .92f), 17f);
            Outline(pill, accent, 2f, 17f);
            Vector2 icon = new Vector2(pill.x + 19f, pill.center.y);
            Disc(icon, 13f, BattleGui.Alpha(accent, .95f));
            DrawIntentGlyph(icon, info.Kind);
            if (info.Kind == 0)
                Text(new Rect(pill.x + 36f, pill.y, w - 40f, pill.height), (info.Area ? "ALL " : "") + info.Damage, info.Area ? 20 : 24, Color.white, TextAnchor.MiddleCenter, true, false, 1.5f);
            else Text(new Rect(pill.x + 36f, pill.y, w - 40f, pill.height), info.Card.Name, 12, Color.white, TextAnchor.MiddleCenter, true);
            if (info.Target != null && info.Card.Target == BattleTarget.Enemy)
            {
                Rect face = new Rect(pill.xMax + 6f, pill.y - 3f, 40f, 40f);
                Round(new Rect(face.x - 2f, face.y - 2f, 44f, 44f), BattleGui.Alpha(accent, .95f), 9f);
                Round(face, new Color(.03f, .05f, .09f), 8f);
                DrawPortrait(new Rect(face.x + 2f, face.y + 2f, 36f, 36f), Spr(info.Target.Art), info.Target == battle.Summoner ? 0.30f : 0.46f);
            }
            if (info.Kind == 0)
                Text(new Rect(head.x - 90f, py + 16f, 180f, 18f), info.Card.Name, 12, new Color(1f, .82f, .8f, .9f), TextAnchor.MiddleCenter, true, false, 1f);
        }
    }

    private static void DrawBar(Vector2 center, float length, float thickness, float angle, Color color)
    {
        Matrix4x4 old = GUI.matrix;
        RotateAround(angle, center);
        Round(new Rect(center.x - length * 0.5f, center.y - thickness * 0.5f, length, thickness), color, thickness * 0.5f);
        GUI.matrix = old;
    }

    private static void DrawIntentGlyph(Vector2 c, int kind)
    {
        Color w = new Color(1f, 1f, 1f, .97f);
        if (kind == 0)
        {
            DrawBar(c, 19f, 3.6f, 45f, w);
            DrawBar(c, 19f, 3.6f, -45f, w);
        }
        else if (kind == 1)
        {
            Round(new Rect(c.x - 6.5f, c.y - 8f, 13f, 15f), w, 5f);
            Round(new Rect(c.x - 3.5f, c.y - 5f, 7f, 9f), new Color(.2f, .5f, .7f, 1f), 3f);
        }
        else
        {
            DrawBar(c + new Vector2(-4.5f, -1f), 12f, 3.6f, 55f, w);
            DrawBar(c + new Vector2(4.5f, -1f), 12f, 3.6f, -55f, w);
        }
    }

    private void HandleFieldClicks()
    {
        if (!pressed || used || hoverCard != null || showLog || confirmWithdraw) return;
        BattleUnit u = hoverUnit;
        if (u == null) return;
        if (selected != null)
        {
            if (battle.IsTarget(selected, selectedActor, u)) { used = true; Commit(u); }
            else { used = true; Toast(u.Enemy ? "A taunting enemy guards this target." : "That is not a valid target."); }
            return;
        }
        if (swapReserve != null && battle.Allies.Contains(u))
        {
            used = true;
            if (Busy) return;
            if (battle.TrySwap(u, swapReserve)) { Toast(swapReserve.Name.Split(' ')[0] + " joins the line."); AfterAction(); }
            swapReserve = null;
            return;
        }
        if (battle.Allies.Contains(u)) { used = true; focusUnit = focusUnit == u ? null : u; }
    }

    private void DrawFocusBar()
    {
        if (focusUnit == null || !battle.Allies.Contains(focusUnit) || !focusUnit.Alive) { focusUnit = null; return; }
        SlotInfo s = Slot(focusUnit);
        Vector2 head = Head(focusUnit);
        float w = 3 * 78f + 16f;
        Rect box = new Rect(head.x - w * 0.5f, head.y - 62f, w, 42f);
        Round(box, new Color(.03f, .05f, .09f, .94f), 12f);
        Outline(box, Ice, 1.8f, 12f);
        bool canMove = !battle.Finished && !Busy && focusUnit.Ap > 0;
        if (MiniButton(new Rect(box.x + 8f, box.y + 8f, 72f, 26f), "FORWARD", canMove && focusUnit.Lane > 0, false, Ice, -1f, 11))
        { if (battle.TryMove(focusUnit, focusUnit.Lane - 1)) { Toast(battle.LastMessage); AfterAction(); } focusUnit = null; }
        if (MiniButton(new Rect(box.x + 86f, box.y + 8f, 72f, 26f), "BACK", canMove && focusUnit.Lane < 2, false, Ice, -1f, 11))
        { if (battle.TryMove(focusUnit, focusUnit.Lane + 1)) { Toast(battle.LastMessage); AfterAction(); } focusUnit = null; }
        bool anyReserve = false;
        for (int i = 0; i < battle.Reserves.Count; i++) anyReserve |= battle.Reserves[i].Alive;
        if (MiniButton(new Rect(box.x + 164f, box.y + 8f, 72f, 26f), "SWAP", anyReserve && !Busy, swapFrom == focusUnit, Gold, -1f, 11))
        { swapFrom = focusUnit; swapReserve = null; focusUnit = null; }
    }

    // ---- top bar -----------------------------------------------------------------------------

    private void DrawPlate(Rect r, BattleUnit unit, bool enemy, string title)
    {
        UnitVis v = V(unit);
        bool offered = Offered(unit);
        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 6f);
        Round(r, Panel, 14f);
        Outline(r, offered ? new Color(.45f, 1f, .78f, pulse) : BattleGui.Alpha(enemy ? EnemyRed : Ice, .55f), offered ? 3f : 1.6f, 14f);
        Rect face = enemy ? new Rect(r.xMax - 78f, r.y + 8f, 70f, 72f) : new Rect(r.x + 8f, r.y + 8f, 70f, 72f);
        Round(face, new Color(.02f, .04f, .07f), 10f);
        DrawPortrait(new Rect(face.x + 2f, face.y + 2f, face.width - 4f, face.height - 4f), Spr(unit.Art), 0.30f);
        Outline(face, BattleGui.Alpha(enemy ? EnemyRed : Ice, .8f), 1.6f, 10f);
        float x = enemy ? r.x + 14f : r.x + 88f;
        float w = r.width - 106f;
        Text(new Rect(x, r.y + 5f, w, 22f), title, 15, enemy ? EnemyRed : Gold, enemy ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, true);
        Rect bar = new Rect(x, r.y + 30f, w, 16f);
        float max = Mathf.Max(1f, unit.MaxHp);
        Bar(bar, v.ShownHp / max, v.GhostHp / max, enemy ? new Color(.86f, .30f, .30f) : new Color(.28f, .78f, .88f), new Color(1f, .88f, .7f, .9f));
        Text(bar, Mathf.CeilToInt(v.ShownHp) + " / " + unit.MaxHp, 12, Color.white, TextAnchor.MiddleCenter, true, false, 1f);
    }

    private void DrawTopBar()
    {
        Rect jd = new Rect(16f, 12f, 356f, 88f);
        DrawPlate(jd, battle.Summoner, false, "JD  -  SUMMONER");
        // SP track and CP pips live under the plate's HP bar.
        for (int i = 0; i < BattleState.SpMax; i++)
        {
            Rect pip = new Rect(jd.x + 88f + i * 24.5f, jd.y + 54f, 21f, 10f);
            bool on = i < battle.Sp;
            bool full = battle.Sp >= BattleState.SpMax;
            Round(pip, on ? (full ? BattleGui.Alpha(Gold, .75f + .25f * Mathf.Sin(Time.time * 7f)) : new Color(.45f, .82f, 1f)) : new Color(.09f, .12f, .18f, .95f), 4f);
        }
        Text(new Rect(jd.x + 88f, jd.y + 65f, 250f, 18f), "SP " + battle.Sp + "/" + BattleState.SpMax + "     CP " + battle.Cp + "/2", 12, new Color(.75f, .85f, .95f), TextAnchor.MiddleLeft, true);
        bool ready = battle.Sp >= BattleState.SpMax && !battle.Finished;
        if (MiniButton(new Rect(16f, 106f, 150f, 30f), "JD DECREE", ready && !Busy, ready, Gold, ready ? 1f : battle.Sp / (float)BattleState.SpMax, 14)) DoDecree();

        Rect pill = new Rect(640f, 12f, 320f, 50f);
        Round(pill, new Color(.02f, .04f, .07f, .82f), 25f);
        Outline(pill, BattleGui.Alpha(Gold, .6f), 1.6f, 25f);
        Text(new Rect(pill.x, pill.y + 2f, pill.width, 30f), "ROUND " + battle.Round, 26, Color.white, TextAnchor.MiddleCenter, true, false, 1.5f);
        string stage = Mathf.Abs(floor) >= 8 ? "BOSS" : Mathf.Abs(floor) >= 3 ? "ELITE" : "GROVE";
        Text(new Rect(pill.x, pill.y + 28f, pill.width, 18f), "SILVERWOOD  -  " + stage + "  -  FLOOR " + floor, 12, Ice, TextAnchor.MiddleCenter, true);

        if (battle.EnemySummoner != null) DrawPlate(new Rect(1010f, 12f, 356f, 88f), battle.EnemySummoner, true, "VALE COMMANDER");
        else
        {
            Rect wild = new Rect(1120f, 12f, 246f, 50f);
            Round(wild, Panel, 14f);
            Outline(wild, BattleGui.Alpha(EnemyRed, .5f), 1.6f, 14f);
            Text(wild, "WILD CORRUPTION", 15, EnemyRed, TextAnchor.MiddleCenter, true);
        }

        if (MiniButton(new Rect(1382f, 14f, 46f, 46f), "LOG", true, showLog, Ice, -1f, 12)) showLog = !showLog;
        if (MiniButton(new Rect(1436f, 14f, 46f, 46f), fastForward ? "2x" : "1x", true, fastForward, Gold, -1f, 15))
        { fastForward = !fastForward; speed = fastForward ? 2f : 1f; }
        if (MiniButton(new Rect(1490f, 14f, 46f, 46f), "AUTO", !battle.Finished, auto, Gold, -1f, 11))
        { auto = !auto; autoTimer = .2f; ClearSelection(); }
        if (MiniButton(new Rect(1544f, 14f, 46f, 46f), battle.Finished ? "EXIT" : "RUN", true, false, EnemyRed, -1f, 12))
        { if (battle.Finished) ExitBattle(); else confirmWithdraw = true; }
    }

    // ---- reserves, piles, end turn -------------------------------------------------------------

    private void DrawReserves()
    {
        Text(new Rect(16f, 144f, 200f, 18f), swapFrom != null || swapReserve != null ? "CHOOSE A SWAP" : "RESERVE", 12, swapFrom != null || swapReserve != null ? Gold : new Color(.7f, .8f, .9f), TextAnchor.MiddleLeft, true, false, 1f);
        for (int i = 0; i < battle.Reserves.Count; i++)
        {
            BattleUnit unit = battle.Reserves[i];
            UnitVis v = V(unit);
            Rect r = new Rect(16f + i * 64f, 164f, 56f, 56f);
            bool live = unit.Alive;
            bool swapping = swapFrom != null || swapReserve != null;
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 6f);
            CommandSurface(r, Ice, live, Over(r), swapReserve == unit);
            Color old = GUI.color; GUI.color = live ? Color.white : new Color(.5f, .5f, .55f, .6f);
            DrawPortrait(new Rect(r.x + 7f, r.y + 7f, 42f, 42f), Spr(unit.Art));
            GUI.color = old;
            Outline(r, swapReserve == unit ? Gold : swapFrom != null && live ? BattleGui.Alpha(Gold, pulse) : BattleGui.Alpha(Ice, .7f), swapping && live ? 2.6f : 1.6f, 10f);
            Rect hp = new Rect(r.x + 3f, r.yMax + 4f, r.width - 6f, 6f);
            Bar(hp, unit.Hp / (float)Mathf.Max(1, unit.MaxHp), 0f, new Color(.28f, .78f, .88f), Color.clear);
            if (!live) Text(r, "DOWN", 12, EnemyRed, TextAnchor.MiddleCenter, true, false, 1f);
            if (Press(r) && !Busy && !battle.Finished)
            {
                if (swapFrom != null && live)
                {
                    if (battle.TrySwap(swapFrom, unit)) { Toast(unit.Name.Split(' ')[0] + " joined the line."); AfterAction(); }
                    swapFrom = null; ClearSelection();
                }
                else if (live) { swapReserve = swapReserve == unit ? null : unit; swapFrom = null; ClearSelection(); focusUnit = null; }
            }
        }
    }

    private void DrawPile(Vector2 center, string label, int count, Color accent)
    {
        for (int i = 2; i >= 0; i--)
        {
            Rect r = new Rect(center.x - 20f + i * 3f, center.y - 28f - i * 3f, 40f, 54f);
            Round(r, new Color(.04f + i * .02f, .07f + i * .02f, .12f + i * .03f, 1f), 7f);
            Outline(r, BattleGui.Alpha(accent, .8f), 1.6f, 7f);
        }
        Text(new Rect(center.x - 30f, center.y - 16f, 60f, 32f), count.ToString(), 26, Color.white, TextAnchor.MiddleCenter, true, false, 2f);
        Text(new Rect(center.x - 40f, center.y + 30f, 80f, 16f), label, 11, accent, TextAnchor.MiddleCenter, true, false, 1f);
    }

    private void DrawPiles()
    {
        DrawPile(DrawPilePos, "DRAW", battle.DrawPile.Count, Ice);
        DrawPile(DiscardPos, "USED", battle.Discard.Count, Gold);
    }

    private bool HasPlay()
    {
        for (int i = 0; i < battle.Allies.Count; i++)
        {
            BattleUnit u = battle.Allies[i];
            if (u.Alive && u.Ap > 0 && !u.Strained(BattleCatalog.Basic(u))) return true;
        }
        for (int i = 0; i < battle.Hand.Count; i++)
            if (Reason(battle.Hand[i], battle.OwnerOf(battle.Hand[i])).Length == 0) return true;
        return false;
    }

    private void DrawEndTurn()
    {
        bool enabled = !battle.Finished && !Busy;
        float r = 56f;
        bool hover = enabled && Vector2.Distance(mouse, EndTurnPos) <= r + 4f && !showLog && !confirmWithdraw;
        bool hint = enabled && !HasPlay();
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4.4f);
        if (hint) DrawGlow(EndTurnPos, 118f, new Color(.4f, .9f, 1f, .38f * pulse + .12f));
        DrawGlow(EndTurnPos, 96f, new Color(0f, 0f, 0f, .55f));
        Color discBefore = GUI.color;
        GUI.color = enabled ? Color.white : new Color(.43f, .47f, .52f);
        DrawSprite(new Rect(EndTurnPos.x - r - 7f, EndTurnPos.y - r - 7f, (r + 7f) * 2f, (r + 7f) * 2f), CommandDiscSkin);
        GUI.color = discBefore;
        if (hover || hint) Ring(EndTurnPos, r - 9f, 1.8f, BattleGui.Alpha(Ice, hover ? .95f : .4f + pulse * .5f));
        Color mark = enabled ? Color.white : new Color(.5f, .56f, .62f);
        DrawBar(EndTurnPos + new Vector2(-14f, 8f), 22f, 10f, 45f, mark);
        DrawBar(EndTurnPos + new Vector2(7f, 0f), 44f, 10f, -52f, mark);
        Text(new Rect(EndTurnPos.x - 80f, EndTurnPos.y + r + 10f, 160f, 24f), "END TURN", 17, enabled ? Color.white : new Color(.5f, .56f, .62f), TextAnchor.MiddleCenter, true, false, 1.5f);
        if (enabled && Vector2.Distance(mouse, EndTurnPos) <= r + 4f && Press(new Rect(EndTurnPos.x - r, EndTurnPos.y - r, r * 2f, r * 2f))) DoEndTurn();
    }

    // ---- hand --------------------------------------------------------------------------------

    private void DrawHand()
    {
        bool paint = Event.current.type == EventType.Repaint;
        if (paint)
        {
            for (int i = 0; i < handOrder.Count; i++)
            {
                BattleCard card = handOrder[i];
                if (card == hoverCard || card == selected) continue;
                DrawCardFor(card);
            }
            if (selected != null && handOrder.Contains(selected)) DrawCardFor(selected);
            if (hoverCard != null && hoverCard != selected) DrawCardFor(hoverCard);
        }
        if (hoverCard != null && pressed && !used && !showLog && !confirmWithdraw)
        { used = true; Select(hoverCard); }
    }

    private void DrawCardFor(BattleCard card)
    {
        CardVis cv = cardVis[card];
        BattleUnit actor = battle.OwnerOf(card);
        bool payable = Reason(card, actor).Length == 0 && !battle.Finished;
        DrawCard(card, cv.Pos, cv.Angle, cv.Scale, cv.Alpha, payable, card == selected, card == hoverCard);
    }

    private static string TargetLabel(BattleTarget target)
    {
        if (target == BattleTarget.AllEnemies) return "ALL FOES";
        if (target == BattleTarget.AllAllies) return "TEAM";
        if (target == BattleTarget.Enemy) return "FOE";
        if (target == BattleTarget.Ally) return "ALLY";
        if (target == BattleTarget.Self) return "SELF";
        return "TACTIC";
    }

    private static string MoveSummary(BattleCard card)
    {
        List<string> effects = new List<string>();
        if (card.Power > 0) effects.Add(card.EffectivePower.ToString("0.#") + "x damage");
        if (card.Heal > 0) effects.Add("heal " + Mathf.RoundToInt(card.EffectiveHeal));
        if (!string.IsNullOrEmpty(card.Status))
        {
            string status = card.Status.Replace("Up", " up").Replace("Down", " down");
            string strength = card.Magnitude > 0 && card.Magnitude < 1
                ? " " + Mathf.RoundToInt(card.Magnitude * 100) + "%"
                : card.Magnitude >= 1 ? " " + card.Magnitude.ToString("0.#") : "";
            effects.Add(status + strength + " (" + card.Duration + "t)");
        }
        if (card.Draw > 0) effects.Add("draw " + card.Draw);
        if (card.EpGain > 0) effects.Add("+" + card.EpGain + " EP");
        if (card.ApGain > 0) effects.Add("+" + card.ApGain + " AP");
        if (card.TransferEp) effects.Add("give remaining EP");
        if (card.TransferAp) effects.Add("give remaining AP");
        return effects.Count == 0 ? "Support move" : string.Join(" - ", effects.ToArray());
    }

    private void DrawCard(BattleCard card, Vector2 center, float angle, float scale, float alpha, bool payable, bool isSelected, bool isHover)
    {
        Matrix4x4 old = GUI.matrix;
        Color before = GUI.color;
        ScaleAround(scale, center);
        RotateAround(angle, center);
        GUI.color = new Color(1, 1, 1, alpha);
        Rect r = new Rect(center.x - CardW * 0.5f, center.y - CardH * 0.5f, CardW, CardH);
        bool summoner = card.Kind == BattleCardKind.Summoner;
        Color accent = summoner ? Gold : ElementColor(card.Element);
        if (isSelected) DrawGlow(new Rect(r.x - 44f, r.y - 44f, r.width + 88f, r.height + 88f), new Color(1f, .85f, .4f, .55f * (0.75f + 0.25f * Mathf.Sin(Time.time * 6f))));
        else if (isHover) DrawGlow(new Rect(r.x - 34f, r.y - 34f, r.width + 68f, r.height + 68f), BattleGui.Alpha(accent, .40f));
        DrawGlow(new Rect(r.x - 20f, r.y + 8f, r.width + 40f, r.height + 34f), new Color(0, 0, 0, .55f));
        Round(new Rect(r.x - 1f, r.y - 1f, r.width + 2f, r.height + 2f), new Color(.02f, .03f, .05f, 1f), 14f);
        Rect art = new Rect(r.x + 9f, r.y + 9f, r.width - 18f, r.height - 18f);
        Texture2D tex = Art("Cards/" + card.Id) ?? Art("FullCards/" + card.Owner);
        Fill(art, new Color(.03f, .05f, .09f));
        if (tex != null) GUI.DrawTexture(art, tex, ScaleMode.ScaleAndCrop, true);
        if (!payable) Fill(art, new Color(.02f, .03f, .06f, .58f));
        GradientUp(new Rect(art.x, art.y + art.height * 0.50f, art.width, art.height * 0.50f), new Color(.01f, .02f, .04f, .94f));
        GradientDown(new Rect(art.x, art.y, art.width, 62f), new Color(.01f, .02f, .04f, .55f));
        Color frameTint = isSelected ? new Color(1f, .93f, .62f) : Color.Lerp(accent, Color.white, .30f);
        if (!payable) frameTint = Color.Lerp(frameTint, new Color(.5f, .52f, .58f), .65f);
        if (CardFrame != null)
        {
            Color keep = GUI.color;
            GUI.color = new Color(frameTint.r, frameTint.g, frameTint.b, alpha);
            GUI.DrawTexture(r, CardFrame);
            GUI.color = keep;
        }
        else Outline(r, frameTint, 4.5f, 12f);
        Color nameColor = payable ? Color.white : new Color(.66f, .68f, .74f);
        int nameSize = Mathf.Clamp(Mathf.FloorToInt(136f / (card.Name.Length * 0.55f)), 13, 18);
        Text(new Rect(r.x + 6f, r.y + CardH - 96f, r.width - 30f, 28f), card.Name, nameSize, nameColor, TextAnchor.MiddleCenter, true, false, 1.5f);
        Text(new Rect(r.x + 10f, r.y + CardH - 68f, r.width - 34f, 34f), MoveSummary(card), 12, payable ? new Color(.72f, .9f, 1f) : new Color(.55f, .62f, .68f), TextAnchor.MiddleCenter, false, true, 1f);
        string cost = summoner ? Math.Max(1, card.Ap) + " CP" : card.Ep + " EP / " + card.Ap + " AP";
        Text(new Rect(r.x + 12f, r.y + CardH - 32f, r.width - 24f, 18f), cost + "  -  " + (card.Owner ?? "").ToUpperInvariant(), 11, Gold, TextAnchor.MiddleCenter, true, false, 1f);
        // Cost gems.
        Vector2 gem = new Vector2(r.x + 27f, r.y + 29f);
        if (summoner)
        {
            DrawGem(gem, 24f, new Color(.62f, .38f, 1f));
            Text(new Rect(gem.x - 20f, gem.y - 17f, 40f, 34f), Math.Max(1, card.Ap).ToString(), 26, Color.white, TextAnchor.MiddleCenter, true, false, 2f);
        }
        else
        {
            DrawGem(gem, 24f, new Color(.22f, .60f, 1f));
            Text(new Rect(gem.x - 20f, gem.y - 17f, 40f, 34f), card.Ep.ToString(), 26, Color.white, TextAnchor.MiddleCenter, true, false, 2f);
            Vector2 ap = new Vector2(r.x + 27f, r.y + 70f);
            DrawGem(ap, 16f, new Color(1f, .70f, .20f));
            Text(new Rect(ap.x - 14f, ap.y - 11f, 28f, 22f), card.Ap.ToString(), 17, Color.white, TextAnchor.MiddleCenter, true, false, 1.5f);
        }
        Rect tag = new Rect(r.xMax - 76f, r.y + 14f, 62f, 20f);
        Round(tag, new Color(.02f, .04f, .07f, .86f), 10f);
        Outline(tag, BattleGui.Alpha(accent, .8f), 1.4f, 10f);
        Text(tag, TargetLabel(card.Target), 10, Gold, TextAnchor.MiddleCenter, true);
        GUI.color = before;
        GUI.matrix = old;
    }

    private static void DrawGem(Vector2 center, float radius, Color tint)
    {
        if (GemTex == null)
        {
            Disc(center, radius, new Color(.02f, .03f, .06f));
            Disc(center, radius - 2.5f, tint);
            return;
        }
        Color keep = GUI.color;
        GUI.color = new Color(tint.r, tint.g, tint.b, keep.a);
        GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), GemTex);
        GUI.color = keep;
    }

    private void DrawFlyingCards()
    {
        for (int i = 0; i < flying.Count; i++)
        {
            FlyCard f = flying[i];
            float t = (fx - f.Start) / FlyLen;
            float k = EaseOut(Mathf.Min(1f, t / 0.7f));
            Vector2 pos = Vector2.Lerp(f.From, new Vector2(800f, 330f), k);
            float alpha = t < 0.6f ? 1f : Mathf.Clamp01((1f - t) / 0.4f);
            DrawCard(f.Card, pos, Mathf.Lerp(f.Angle, 0f, k), Mathf.Lerp(f.Scale, 1.05f + t * 0.25f, k), alpha, true, true, false);
        }
    }

    // ---- party tiles -------------------------------------------------------------------------

    private void DrawPartyTiles()
    {
        for (int i = 0; i < battle.Allies.Count; i++)
        {
            BattleUnit u = battle.Allies[i];
            Rect tile = new Rect(16f, 640f + i * 84f, 256f, 78f);
            bool alive = u.Alive;
            bool actorNow = selectedActor == u && selected != null;
            Color accent = ElementColor(u.Element);
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 6f);
            Round(tile, new Color(.02f, .04f, .07f, .88f), 12f);
            Outline(tile, actorNow ? Gold : focusUnit == u ? Ice : BattleGui.Alpha(accent, .6f), actorNow ? 3f : 1.6f, 12f);
            Rect face = new Rect(tile.x + 7f, tile.y + 8f, 62f, 62f);
            Round(face, new Color(.02f, .03f, .06f), 10f);
            Color old = GUI.color; GUI.color = alive ? Color.white : new Color(.5f, .5f, .55f, .6f);
            DrawPortrait(new Rect(face.x + 2f, face.y + 2f, 58f, 58f), Spr(u.Art));
            GUI.color = old;
            Outline(face, u.Ultimate >= 100 && battle.Sp >= BattleState.UltimateSpCost ? BattleGui.Alpha(Gold, pulse) : BattleGui.Alpha(accent, .85f), 2f, 10f);
            float x = tile.x + 76f;
            Text(new Rect(x, tile.y + 4f, 130f, 20f), u.Name.Split(' ')[0].ToUpperInvariant(), 15, alive ? Color.white : new Color(.6f, .6f, .66f), TextAnchor.MiddleLeft, true, false, 1f);
            Text(new Rect(x + 96f, tile.y + 4f, 76f, 20f), u.Role.ToString().ToUpperInvariant(), 10, new Color(.7f, .8f, .9f), TextAnchor.MiddleRight, true);
            // AP / EP pips.
            for (int a = 0; a < Mathf.Max(1, u.MaxAp); a++)
            {
                Rect pip = new Rect(x + a * 16f, tile.y + 27f, 12f, 12f);
                Round(pip, a < u.Ap ? new Color(.96f, .72f, .22f) : new Color(.10f, .12f, .17f), 4f);
                Outline(pip, new Color(1f, .85f, .5f, .8f), 1.2f, 4f);
            }
            int epPips = Mathf.Max(u.MaxEp, u.Ep);
            float epStart = x + Mathf.Max(1, u.MaxAp) * 16f + 10f;
            for (int p = 0; p < epPips; p++)
            {
                Vector2 c = new Vector2(epStart + p * 15f + 6f, tile.y + 33f);
                Disc(c, 6f, p < u.Ep ? new Color(.30f, .72f, 1f) : new Color(.10f, .12f, .17f));
                Ring(c, 6.4f, 1.2f, new Color(.7f, .92f, 1f, .85f));
            }
            // Stress.
            Rect stress = new Rect(x + 96f, tile.y + 26f, 76f, 12f);
            Color sc = u.CollapseRounds > 0 ? new Color(1f, .3f, .75f) : u.Stress >= 75 ? new Color(.95f, .4f, .45f) : new Color(.6f, .4f, .9f);
            Bar(stress, u.Stress / 100f, 0f, sc, sc);
            Text(stress, u.CollapseRounds > 0 ? "BREAKDOWN " + u.CollapseRounds + "T" : "STRESS " + u.Stress + "%", 9, Color.white, TextAnchor.MiddleCenter, true, false, 1f);
            // Actions.
            Rect row = new Rect(x, tile.y + 46f, 172f, 26f);
            bool canAct = alive && !battle.Finished && !Busy;
            if (u.AwakeningReady && alive)
            {
                float glow = 0.6f + 0.4f * Mathf.Sin(Time.time * 6f);
                if (MiniButton(row, "AWAKEN", canAct && selected == null, true, BattleGui.Alpha(Gold, glow), 1f, 14))
                {
                    BattleCard awakening = BattleCatalog.Awakening(u);
                    float t = Mathf.Max(queueEnd, fx);
                    queueEnd = t;
                    if (battle.TryAwakening(u)) { Toast(u.Name.Split(' ')[0] + " awakens with " + awakening.Name + "."); AfterAction(); }
                }
            }
            else
            {
                BattleCard basic = BattleCatalog.Basic(u);
                bool basicOn = canAct && u.Ap > 0 && !u.Strained(basic);
                bool basicActive = selected != null && selected.Id == basic.Id && selectedActor == u;
                if (MiniButton(new Rect(row.x, row.y, 76f, 26f), "BASIC", basicOn, basicActive, Ice, -1f, 13))
                { if (basicActive) ClearSelection(); else { ClearSelection(); Select(basic); } }
                bool ultReady = u.Ultimate >= 100 && battle.Sp >= BattleState.UltimateSpCost;
                if (MiniButton(new Rect(row.x + 82f, row.y, 90f, 26f), ultReady ? "ULTIMATE" : "ULT " + u.Ultimate + "%",
                    canAct && ultReady, chooseUltimateFor == u, Gold, u.Ultimate / 100f, ultReady ? 12 : 13))
                { ClearSelection(); chooseUltimateFor = chooseUltimateFor == u ? null : u; focusUnit = null; }
            }
            if (chooseUltimateFor == u) { popupUnit = u; popupTile = tile; popupRect = UltimatePopupRect(u, tile); }
            if (Press(tile) && selected == null && !Busy)
            {
                if (swapReserve != null && !Busy) { if (battle.TrySwap(u, swapReserve)) { Toast(swapReserve.Name.Split(' ')[0] + " joins the line."); AfterAction(); } swapReserve = null; }
                else if (alive) focusUnit = focusUnit == u ? null : u;
            }
        }
    }

    private static Rect UltimatePopupRect(BattleUnit u, Rect tile)
    {
        return new Rect(tile.xMax + 10f, tile.y - 40f - BattleCatalog.Ultimates(u).Count * 40f + 40f, 250f, 26f + BattleCatalog.Ultimates(u).Count * 40f);
    }

    private void DrawUltimateChoices(BattleUnit u, Rect tile)
    {
        List<BattleCard> options = BattleCatalog.Ultimates(u);
        Rect box = UltimatePopupRect(u, tile);
        Round(box, new Color(.03f, .05f, .09f, .96f), 12f);
        Outline(box, Gold, 2f, 12f);
        Text(new Rect(box.x + 10f, box.y + 2f, box.width - 20f, 22f), "CHOOSE " + u.Name.Split(' ')[0].ToUpperInvariant() + "'S ULTIMATE", 12, Gold, TextAnchor.MiddleLeft, true);
        for (int i = 0; i < options.Count && i < 2; i++)
            if (MiniButton(new Rect(box.x + 10f, box.y + 26f + i * 40f, box.width - 20f, 34f), options[i].Name, true, false, Gold, -1f, 14)) SelectUltimate(u, i);
    }

    // ---- hint, toast, tooltip ----------------------------------------------------------------

    private void DrawHint()
    {
        string hint = null;
        if (selected != null) hint = selected.Name + ": " + Description(selected) + "  (right-click to cancel)";
        else if (swapReserve != null) hint = "Click the fighter " + swapReserve.Name.Split(' ')[0] + " should replace.";
        else if (swapFrom != null) hint = "Click a reserve to replace " + swapFrom.Name.Split(' ')[0] + ".";
        else if (hoverCard != null)
        {
            string why = Reason(hoverCard, battle.OwnerOf(hoverCard));
            if (why.Length > 0) hint = why + ".";
        }
        if (hint == null || Event.current.type != EventType.Repaint) return;
        float w = Mathf.Min(900f, 40f + hint.Length * 8.4f);
        Rect r = new Rect(806f - w * 0.5f, 640f, w, 34f);
        Round(r, new Color(.02f, .04f, .07f, .9f), 17f);
        Outline(r, selected != null ? BattleGui.Alpha(Gold, .9f) : BattleGui.Alpha(EnemyRed, .8f), 1.8f, 17f);
        Text(r, hint, 15, Color.white, TextAnchor.MiddleCenter, true);
    }

    private static string Description(BattleCard card)
    {
        string line = card.Power > 0 ? card.EffectivePower.ToString("0.0") + "x damage. " : "";
        if (card.Heal > 0) line += "Heal " + Mathf.RoundToInt(card.EffectiveHeal) + ". ";
        if (!string.IsNullOrEmpty(card.Status)) line += StatusLabel(card.Status) + " " + card.Duration + "t. ";
        if (card.Draw > 0) line += "Draw " + card.Draw + ". ";
        if (card.TransferEp) line += "Donate remaining EP. ";
        if (card.EpGain > 0) line += "Give " + card.EpGain + " EP. ";
        if (card.ApGain > 0) line += "Give " + card.ApGain + " AP. ";
        return line + (card.Target == BattleTarget.Enemy || card.Target == BattleTarget.Ally ? "Choose a target." : "");
    }

    private void DrawToast()
    {
        float age = Time.time - toastStart;
        if (age > 3.2f || string.IsNullOrEmpty(toast)) return;
        float a = age < 0.15f ? age / 0.15f : age > 2.6f ? (3.2f - age) / 0.6f : 1f;
        float w = Mathf.Min(1000f, 60f + toast.Length * 9.4f);
        Rect r = new Rect(800f - w * 0.5f, 112f + (1f - a) * -10f, w, 36f);
        Round(r, new Color(.02f, .04f, .07f, .88f * a), 18f);
        Outline(r, BattleGui.Alpha(Ice, .6f * a), 1.6f, 18f);
        Text(r, toast, 16, new Color(1, 1, 1, a), TextAnchor.MiddleCenter, true);
    }

    private void DrawTooltip()
    {
        if (Event.current.type != EventType.Repaint || hoverUnit == null || selected != null || showLog || confirmWithdraw) return;
        BattleUnit u = hoverUnit;
        SlotInfo s = Slot(u);
        List<string> lines = new List<string>();
        lines.Add(u.Name);
        lines.Add(u.Element + "  -  " + u.Role + (u.Taunting ? "  -  Taunt" : ""));
        lines.Add("HP " + u.Hp + " / " + u.MaxHp + (u.Enemy ? "" : "     AP " + u.Ap + "  EP " + u.Ep));
        if (!u.Enemy && u != battle.Summoner) lines.Add("Ultimate " + u.Ultimate + "%   Stress " + u.Stress + "%" + (u.CollapseRounds > 0 ? "  BREAKDOWN" : ""));
        for (int i = 0; i < u.Statuses.Count; i++)
            lines.Add((IsBuff(u.Statuses[i].Name) ? "+ " : "- ") + StatusLabel(u.Statuses[i].Name) + " (" + u.Statuses[i].Turns + " rounds)");
        IntentInfo info;
        if (u.Enemy && intents.TryGetValue(u, out info) && !Busy)
            lines.Add("Next: " + info.Card.Name + (info.Damage > 0 ? " for about " + info.Damage : "") + (info.Target != null && info.Card.Target == BattleTarget.Enemy ? " on " + info.Target.Name.Split(' ')[0] : ""));
        float h = 14f + lines.Count * 21f;
        float x = u.Enemy ? s.Foot.x - s.W * 0.5f - 262f : s.Foot.x + s.W * 0.5f + 10f;
        if (u == battle.Summoner) x = s.Foot.x + 130f;
        if (u == battle.EnemySummoner) x = s.Foot.x - 290f;
        x = Mathf.Clamp(x, 8f, VW - 258f);
        float y = Mathf.Clamp(s.Foot.y - s.H * 0.8f, 110f, 620f - h);
        Rect r = new Rect(x, y, 250f, h);
        Round(r, new Color(.02f, .04f, .07f, .95f), 12f);
        Outline(r, BattleGui.Alpha(u.Enemy ? EnemyRed : Ice, .8f), 1.8f, 12f);
        for (int i = 0; i < lines.Count; i++)
            Text(new Rect(r.x + 12f, r.y + 8f + i * 21f, r.width - 20f, 20f), lines[i], i == 0 ? 16 : 13,
                i == 0 ? (u.Enemy ? EnemyRed : Gold) : (lines[i].StartsWith("- ") ? EnemyRed : lines[i].StartsWith("+ ") ? new Color(.5f, .95f, .8f) : Color.white),
                TextAnchor.MiddleLeft, i == 0);
    }

    // ---- cut-in, result, modals --------------------------------------------------------------

    private void DrawCutIn()
    {
        if (cutUnit == null || cutCard == null) return;
        float t = (fx - cutStart) / cutLen;
        if (t < 0f || t > 1f) return;
        float a = t < 0.12f ? t / 0.12f : t > 0.86f ? (1f - t) / 0.14f : 1f;
        Color accent = ElementColor(cutUnit.Element);
        if (ultPlayer != null && ultPlayer.clip == UltClip(cutUnit, cutCard) && ultTexture != null)
        {
            // Cinematic ultimate: letterboxed video, quick white pop in/out, name plate bottom-left.
            float va = t < 0.06f ? t / 0.06f : t > 0.93f ? (1f - t) / 0.07f : 1f;
            Rect cover = new Rect(-VW, -VH, VW * 3f, VH * 3f);   // past the virtual canvas, for non-16:9 screens
            Fill(cover, new Color(0, 0, 0, va));
            Color was = GUI.color; GUI.color = new Color(1, 1, 1, va);
            GUI.DrawTexture(new Rect(0, 0, VW, VH), ultTexture, ScaleMode.ScaleAndCrop, false);
            GUI.color = was;
            float pop = Mathf.Max(0f, 1f - t / 0.05f) + Mathf.Max(0f, (t - 0.95f) / 0.05f);
            if (pop > 0f) Fill(cover, new Color(1, 1, 1, pop * 0.85f));
            Fill(new Rect(-VW, -VH, VW * 3f, VH + 70f), new Color(0, 0, 0, va));
            Fill(new Rect(-VW, VH - 70f, VW * 3f, VH + 70f), new Color(0, 0, 0, va));
            float nx = Mathf.Lerp(-500f, 60f, EaseOut(Mathf.Clamp01((t - 0.08f) / 0.2f)));
            Text(new Rect(nx, VH - 190f, 900f, 34f), cutCard.Kind == BattleCardKind.Awakening ? "AWAKENING" : "ULTIMATE", 24, BattleGui.Alpha(Gold, va), TextAnchor.MiddleLeft, true, false, 2f);
            Text(new Rect(nx, VH - 160f, 1100f, 80f), cutCard.Name.ToUpperInvariant(), 58, new Color(1, 1, 1, va), TextAnchor.MiddleLeft, true, false, 4f);
            Text(new Rect(nx, VH - 92f, 900f, 30f), cutUnit.Name.ToUpperInvariant(), 22, BattleGui.Alpha(accent, va), TextAnchor.MiddleLeft, true, false, 2f);
            return;
        }
        Fill(new Rect(0, 0, VW, VH), new Color(0, 0, .02f, .66f * a));
        DrawSpin(Rays, new Vector2(800f, 450f), 1900f, Time.time * 14f, BattleGui.Alpha(Color.Lerp(accent, Color.white, .35f), .55f * a));
        DrawSpin(Rays, new Vector2(800f, 450f), 1500f, -Time.time * 9f, BattleGui.Alpha(accent, .35f * a));
        Matrix4x4 old = GUI.matrix;
        RotateAround(-7f, new Vector2(800f, 450f));
        Fill(new Rect(-300f, 340f, 2200f, 210f), BattleGui.Alpha(Color.Lerp(accent, Color.black, .55f), .92f * a));
        Fill(new Rect(-300f, 336f, 2200f, 5f), BattleGui.Alpha(Color.white, .9f * a));
        Fill(new Rect(-300f, 549f, 2200f, 5f), BattleGui.Alpha(accent, .95f * a));
        GUI.matrix = old;
        Texture2D art = Art("FullCards/" + cutUnit.Id);
        float slide = EaseOut(Mathf.Min(1f, t / 0.28f));
        float x = Mathf.Lerp(-560f, 120f, slide) + t * 46f;
        if (art != null)
        {
            Color before = GUI.color; GUI.color = new Color(1, 1, 1, a);
            Rect ar = new Rect(x, 30f, 520f, 880f);
            RotateAround(-7f, new Vector2(800f, 450f));
            GUI.DrawTexture(ar, art, ScaleMode.ScaleAndCrop, true);
            Outline(ar, BattleGui.Alpha(Color.white, .85f * a), 4f, 6f);
            GUI.matrix = old;
            GUI.color = before;
        }
        float tx = Mathf.Lerp(1500f, 700f, EaseOut(Mathf.Min(1f, t / 0.3f)));
        Text(new Rect(tx, 372f, 900f, 40f), cutCard.Kind == BattleCardKind.Awakening ? "AWAKENING" : "ULTIMATE", 26, BattleGui.Alpha(Gold, a), TextAnchor.MiddleLeft, true, false, 2f);
        Text(new Rect(tx, 408f, 900f, 90f), cutCard.Name.ToUpperInvariant(), 66, new Color(1, 1, 1, a), TextAnchor.MiddleLeft, true, false, 4f);
        Text(new Rect(tx, 496f, 900f, 34f), cutUnit.Name.ToUpperInvariant(), 24, BattleGui.Alpha(accent, a), TextAnchor.MiddleLeft, true, false, 2f);
    }

    private void DrawResult()
    {
        Fill(new Rect(0, 0, VW, VH), new Color(0, 0, .02f, .72f));
        Color c = battle.Victory ? Gold : EnemyRed;
        float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 2.4f);
        DrawSpin(Rays, new Vector2(800f, 340f), 1800f, Time.time * 10f, BattleGui.Alpha(c, .5f));
        DrawGlow(new Vector2(800f, 340f), 420f, BattleGui.Alpha(c, .22f * pulse));
        DrawStreak(new Vector2(800f, 340f), 1100f, 34f, 0f, BattleGui.Alpha(c, .5f));
        Text(new Rect(0, 270f, VW, 130f), battle.Victory ? "VICTORY" : battle.Withdrawn ? "WITHDRAWN" : "DEFEAT", 110, c, TextAnchor.MiddleCenter, true, false, 5f);
        Text(new Rect(300f, 404f, 1000f, 50f), battle.Victory ? reward + " gold will return to the Tower." : battle.LastMessage, 26, Color.white, TextAnchor.MiddleCenter, true, false, 2f);
        Rect button = new Rect(620f, 500f, 360f, 70f);
        if (MiniButton(button, ReturnLabel, true, true, c, -1f, 24)) ExitBattle();
    }

    private void DrawLog()
    {
        modalDrawing = true;
        Fill(new Rect(0, 0, VW, VH), new Color(0, 0, 0, .58f));
        Rect box = new Rect(379f, 160f, 842f, 470f);
        Round(box, new Color(.02f, .04f, .07f, .98f), 18f);
        Outline(box, BattleGui.Alpha(Gold, .8f), 2f, 18f);
        Text(new Rect(box.x + 26f, box.y + 12f, 500f, 44f), "BATTLE LOG", 28, Gold, TextAnchor.MiddleLeft, true);
        if (MiniButton(new Rect(box.xMax - 140f, box.y + 16f, 116f, 36f), "CLOSE", true, false, Ice, -1f, 15)) showLog = false;
        float height = Mathf.Max(370f, battle.Log.Count * 27f + 10f);
        logScroll = GUI.BeginScrollView(new Rect(box.x + 20f, box.y + 66f, 796f, 380f), logScroll, new Rect(0, 0, 765f, height));
        for (int i = 0; i < battle.Log.Count; i++)
            Text(new Rect(8f, i * 27f, 744f, 26f), battle.Log[i], 16, Color.white);
        GUI.EndScrollView();
        modalDrawing = false;
    }

    private void DrawWithdrawConfirm()
    {
        modalDrawing = true;
        Fill(new Rect(0, 0, VW, VH), new Color(0, 0, 0, .62f));
        Rect box = new Rect(490f, 310f, 620f, 250f);
        Round(box, new Color(.02f, .04f, .07f, .98f), 18f);
        Outline(box, BattleGui.Alpha(EnemyRed, .85f), 2f, 18f);
        Text(new Rect(box.x + 30f, box.y + 25f, 560f, 75f), "Withdraw from this fight?", 30, Gold, TextAnchor.MiddleCenter, true);
        Text(new Rect(box.x + 30f, box.y + 100f, 560f, 49f), "The party returns to the Tower without a reward.", 18, Color.white, TextAnchor.MiddleCenter);
        if (MiniButton(new Rect(box.x + 40f, box.y + 172f, 240f, 55f), "KEEP FIGHTING", true, false, Ice, -1f, 17)) confirmWithdraw = false;
        if (MiniButton(new Rect(box.x + 340f, box.y + 172f, 240f, 55f), "WITHDRAW", true, false, EnemyRed, -1f, 17))
        { confirmWithdraw = false; battle.Forfeit(); ExitBattle(); }
        modalDrawing = false;
    }
}
