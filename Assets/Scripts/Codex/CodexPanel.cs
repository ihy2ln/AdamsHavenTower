using System;
using System.Collections.Generic;
using AdamsHaven.Tower;
using UnityEngine;
using static BattleGui;

// The BESTIARY + CODEX screen: every monster form, party fighter, roster character and lair boss as a card.
//   BESTIARY     the 98 forms by family, in evolution order; beasts not met yet are silhouettes
//   FIGHTERS     the party and JD: kit, ultimates and awakening on the back
//   ROSTER       every hero and resident the Tower can summon
//   LAIR BOSSES  one per region; shown once that land has been walked
// Tap a card to see it large; tap the large card (or FLIP) to turn it over. The front is the art with rank, element,
// name and numbers; the back is the move list (a monster's painted move card has its moves written into its panels).
// Cards are drawn live from the game data (CodexCards), so new monster art shows the next time the codex opens.
// IMGUI on the 1600x900 canvas, above everything (GUI.depth) and swallowing input while open, like MediaPanel.
public sealed class CodexPanel : MonoBehaviour
{
    private const float VW = 1600f, VH = 900f;
    private const float CardW = 344f, CardH = 516f, GridW = 132f, GridH = 198f, GridGap = 16f;
    private static readonly Color Gold = new Color(.97f, .82f, .48f), Ice = new Color(.60f, .86f, 1f), Red = new Color(1f, .55f, .50f),
        Mint = new Color(.45f, 1f, .78f), Dim = new Color(.70f, .76f, .85f), Faint = new Color(.48f, .53f, .60f);
    private static readonly string[] Tabs = { "BESTIARY", "FIGHTERS", "ROSTER", "LAIR BOSSES" };

    public static bool IsOpen { get { return instance != null && instance.open; } }
    private static CodexPanel instance;

    private Action onClosed;
    private bool open;
    private int tab;
    private CodexContext context = CodexContext.Everything;
    private readonly List<CodexEntry>[] lists = new List<CodexEntry>[4];
    private CodexEntry selected;
    private int viewRank;                          // the rank a monster's numbers are shown at
    private List<CodexMove> moves;                 // the selected card's back
    private bool flipped; private float flip;      // flip: 0 front .. 1 back, eased toward flipped
    private float gridScroll;
    private bool dragging, dragMoved; private float dragStartY, dragStartScroll;
    private Vector2 mouse;
    private GUIStyle measure;

    // Opens the codex on a tab ("BESTIARY", "FIGHTERS", "ROSTER", "LAIR BOSSES") with a card selected (entry id).
    // context: what the guild has seen (CodexContext.From(rules)); null shows everything. onClosed runs on close.
    public static void Show(string tabName = null, string select = null, CodexContext context = null, Action onClosed = null)
    {
        if (instance == null)
        {
            var host = new GameObject("Codex Panel");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<CodexPanel>();
        }
        CodexCards.Forget();
        var p = instance;
        p.open = true;
        p.onClosed = onClosed;
        p.context = context ?? CodexContext.Everything;
        p.lists[0] = CodexCards.Monsters();
        p.lists[1] = CodexCards.Fighters();
        p.lists[2] = CodexCards.Roster();
        p.lists[3] = CodexCards.Bosses();
        if (tabName != null) { int t = Array.IndexOf(Tabs, tabName.ToUpperInvariant()); if (t >= 0) p.tab = t; }
        p.gridScroll = 0f;
        p.Select(select != null ? p.lists[p.tab].Find(e => e.Id == select) : null, true);
    }

    // The card for a unit on the battlefield: a fighter, a beast (by its form) or a lair boss (by its title).
    public static void ShowFor(BattleUnit unit, CodexContext context = null, Action onClosed = null)
    {
        context = context ?? CodexContext.Everything;
        if (unit == null) { Show(null, null, context, onClosed); return; }
        if (!unit.Enemy) { Show("FIGHTERS", unit.Id, context, onClosed); return; }
        if (unit.Boss)
        {
            var lair = CodexCards.Bosses().Find(b => b.Name == unit.Name);
            if (lair != null)
            {
                // Standing in its lair counts as having found it.
                if (context.Regions != null && !context.Knows(lair))
                    context = new CodexContext { Beasts = context.Beasts, Regions = new List<string>(context.Regions) { lair.Lair.region },
                        Conquered = context.Conquered, Recruited = context.Recruited };
                Show("LAIR BOSSES", lair.Id, context, onClosed);
                return;
            }
        }
        var form = BattleBestiary.Form(unit.Species);
        if (form == null) foreach (var f in BattleBestiary.Families) foreach (var x in f.forms) if (x.name == unit.Name) form = x;
        // A beast on the field has been met, journal or not (Tower skirmishes keep no journal).
        if (form != null && context.Beasts != null && !context.Beasts.Contains(form.id))
            context = new CodexContext { Beasts = new List<string>(context.Beasts) { form.id }, Regions = context.Regions,
                Conquered = context.Conquered, Recruited = context.Recruited };
        Show("BESTIARY", form != null ? form.id : null, context, onClosed);
    }

    public static void Hide()
    {
        if (instance == null || !instance.open) return;
        instance.open = false;
        instance.moves = null;
        CodexCards.Forget();
        Resources.UnloadUnusedAssets();
        var done = instance.onClosed; instance.onClosed = null;
        done?.Invoke();
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    private List<CodexEntry> Current { get { return lists[tab] ?? new List<CodexEntry>(); } }

    private void Select(CodexEntry e, bool scrollTo = false)
    {
        if (e == null)
        {
            var list = Current;
            e = list.Find(x => context.Knows(x)) ?? (list.Count > 0 ? list[0] : null);
        }
        if (e != selected) { flipped = false; flip = 0f; }
        selected = e;
        moves = e != null ? CodexCards.Moves(e) : null;
        viewRank = e != null && e.Kind == CodexKind.Monster ? e.Form.minRank : 0;
        if (scrollTo && e != null) pendingScroll = e;
    }

    private CodexEntry pendingScroll;

    private void Step(int dir)
    {
        var list = Current;
        if (list.Count == 0) return;
        int i = selected == null ? -1 : list.IndexOf(selected);
        Select(list[(i + dir + list.Count) % list.Count], true);
        Click();
    }

    private void Flip()
    {
        if (selected == null || !context.Knows(selected)) return;
        flipped = !flipped;
        if (TowerAudio.Has("Sfx/card_draw")) TowerAudio.Play("Sfx/card_draw", .6f);
    }

    private static void Click() { if (TowerAudio.Has("click")) TowerAudio.Play("click", .35f); }

    // ---- frame ---------------------------------------------------------------------------------------------

    private void OnGUI()
    {
        if (!open) return;
        GUI.depth = -100;
        BattleGui.Build();
        Event e = Event.current;
        float scale = Mathf.Min(Screen.width / VW, Screen.height / VH);
        Vector2 offset = new Vector2((Screen.width - VW * scale) * .5f, (Screen.height - VH * scale) * .5f);
        mouse = (e.mousePosition - offset) / scale;
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(new Vector3(offset.x, offset.y, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.Escape) { Hide(); e.Use(); GUI.matrix = old; return; }
            if (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.UpArrow) { Step(-1); e.Use(); }
            else if (e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.DownArrow) { Step(1); e.Use(); }
            else if (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.F) { Flip(); e.Use(); }
        }
        if (e.type == EventType.Repaint) flip = Mathf.MoveTowards(flip, flipped ? 1f : 0f, Time.unscaledDeltaTime / .32f);

        Fill(new Rect(-VW, -VH, VW * 3f, VH * 3f), new Color(0, 0, .02f, .84f));
        Rect box = new Rect(40f, 28f, 1520f, 844f);
        Round(box, new Color(.02f, .04f, .07f, .985f), 18f);
        Outline(box, Alpha(Gold, .8f), 2f, 18f);
        Text(new Rect(box.x + 28f, box.y + 12f, 600f, 44f), "CODEX", 30, Gold, TextAnchor.MiddleLeft, true, false, 2f);
        Text(new Rect(box.x + 30f, box.y + 54f, 1100f, 22f),
            "Every beast, fighter, Tower character and lair boss as a card. Tap a card to look closer; tap it again to turn it over.", 13, Dim);
        if (Btn(new Rect(box.xMax - 160f, box.y + 16f, 136f, 42f), "CLOSE", true, Red)) { Hide(); GUI.matrix = old; return; }
        for (int i = 0; i < Tabs.Length; i++)
            if (Btn(new Rect(box.x + 28f + i * 214f, box.y + 88f, 204f, 40f), Tabs[i] + "   " + TabCount(i), true, Ice, tab == i, 14))
            { tab = i; gridScroll = 0f; Select(null, true); Click(); }

        Rect grid = new Rect(box.x + 28f, box.y + 144f, 772f, box.height - 168f);
        DrawGrid(grid);
        DrawDetail(new Rect(grid.xMax + 24f, grid.y, box.xMax - 28f - grid.xMax - 24f, grid.height));

        // Nothing underneath gets the pointer while the codex is up.
        if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.MouseDrag || e.type == EventType.ScrollWheel) e.Use();
        GUI.matrix = old;
    }

    private string TabCount(int i)
    {
        var list = lists[i];
        if (list == null) return "";
        if (i == 1) return list.Count.ToString();
        int n = 0;
        foreach (var e in list) if (i == 2 ? context.Recruits(e) : context.Knows(e)) n++;
        if (i == 2 && context.Recruited == null) return list.Count.ToString();
        return n + "/" + list.Count;
    }

    private bool Btn(Rect r, string label, bool enabled, Color accent, bool active = false, int size = 13)
    {
        return MediaBrowser.Button(r, label, enabled, accent, mouse, active, size);
    }

    // ---- grid ----------------------------------------------------------------------------------------------

    private struct Slot { public CodexEntry Entry; public Rect Rect; public string Header, Note; public bool Arrow; }

    // Card positions in the grid's own space (top 0): the bestiary goes family by family, the rest in rows of five.
    private List<Slot> Layout(float width, out float height)
    {
        var slots = new List<Slot>();
        var list = Current;
        float y = 8f;
        int perRow = Mathf.Max(1, Mathf.FloorToInt((width - 24f + GridGap) / (GridW + GridGap)));
        float left = (width - (perRow * GridW + (perRow - 1) * GridGap)) * .5f;
        if (tab == 0)
        {
            const float arrow = 30f;
            foreach (var family in BattleBestiary.Families)
            {
                int seen = 0;
                foreach (var f in family.forms) if (context.Beasts == null || context.Beasts.Contains(f.id)) seen++;
                slots.Add(new Slot { Header = seen > 0 ? family.name.ToUpperInvariant() : "UNKNOWN FAMILY",
                    Note = (seen > 0 ? family.element + "  ·  " : "") + seen + "/" + family.forms.Length + " met", Rect = new Rect(14f, y, width - 28f, 26f) });
                y += 32f;
                float x = 18f;
                for (int i = 0; i < family.forms.Length; i++)
                {
                    var entry = list.Find(en => en.Form == family.forms[i]);
                    if (entry == null) continue;
                    if (i > 0) { slots.Add(new Slot { Arrow = true, Rect = new Rect(x - arrow, y + GridH * .5f - 12f, arrow, 24f) }); }
                    slots.Add(new Slot { Entry = entry, Rect = new Rect(x, y, GridW, GridH) });
                    x += GridW + arrow;
                }
                y += GridH + 22f;
            }
        }
        else
        {
            string lastHeader = null;
            int col = 0;
            foreach (var entry in list)
            {
                string header = tab == 2 ? (entry.Unit.IsHero ? "HEROES" : "RESIDENTS") : null;
                if (header != lastHeader)
                {
                    if (col > 0) { y += GridH + GridGap; col = 0; }
                    slots.Add(new Slot { Header = header, Rect = new Rect(14f, y, width - 28f, 26f) });
                    y += 32f;
                    lastHeader = header;
                }
                slots.Add(new Slot { Entry = entry, Rect = new Rect(left + col * (GridW + GridGap), y, GridW, GridH) });
                if (++col == perRow) { col = 0; y += GridH + GridGap; }
            }
            if (col > 0) y += GridH + GridGap;
        }
        height = y + 8f;
        return slots;
    }

    private void DrawGrid(Rect r)
    {
        Event e = Event.current;
        Round(r, new Color(.03f, .05f, .09f), 12f);
        float content;
        var slots = Layout(r.width - 10f, out content);
        float max = Mathf.Max(0f, content - r.height);
        if (pendingScroll != null)
        {
            foreach (var s in slots)
                if (s.Entry == pendingScroll && (s.Rect.y < gridScroll || s.Rect.yMax > gridScroll + r.height))
                    gridScroll = s.Rect.y - 40f;
            pendingScroll = null;
        }
        if (e.type == EventType.ScrollWheel && r.Contains(mouse)) gridScroll += e.delta.y * 36f;
        if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(mouse)) { dragging = true; dragMoved = false; dragStartY = mouse.y; dragStartScroll = gridScroll; }
        if (e.type == EventType.MouseDrag && dragging) { if (Mathf.Abs(mouse.y - dragStartY) > 8f) dragMoved = true; gridScroll = dragStartScroll - (mouse.y - dragStartY); }
        bool tap = e.type == EventType.MouseUp && dragging && !dragMoved && r.Contains(mouse);
        if (e.type == EventType.MouseUp) dragging = false;
        gridScroll = Mathf.Clamp(gridScroll, 0f, max);

        GUI.BeginGroup(r);
        Vector2 local = mouse - r.position + new Vector2(0f, gridScroll);
        CodexEntry picked = null;
        foreach (var s in slots)
        {
            Rect at = new Rect(s.Rect.x, s.Rect.y - gridScroll, s.Rect.width, s.Rect.height);
            if (at.yMax < -4f || at.y > r.height + 4f) continue;
            if (s.Header != null)
            {
                Text(at, s.Header, 15, Gold, TextAnchor.MiddleLeft, true);
                if (s.Note != null) Text(new Rect(at.x, at.y, at.width - 8f, at.height), s.Note, 12, Dim, TextAnchor.MiddleRight);
                Fill(new Rect(at.x, at.yMax + 1f, at.width, 1f), Alpha(Gold, .18f));
                continue;
            }
            if (s.Arrow) { Text(at, ">", 20, Alpha(Gold, .55f), TextAnchor.MiddleCenter, true); continue; }
            bool hover = !dragging && s.Rect.Contains(local) && r.Contains(mouse);
            bool sel = s.Entry == selected;
            if (sel || hover) DrawGlow(new Rect(at.x - 18f, at.y - 18f, at.width + 36f, at.height + 36f), Alpha(sel ? Gold : Ice, sel ? .42f : .22f));
            DrawFront(at, s.Entry, true);
            if (sel) Outline(new Rect(at.x - 3f, at.y - 3f, at.width + 6f, at.height + 6f), Gold, 2f, 13f);
            if (tap && s.Rect.Contains(local)) picked = s.Entry;
        }
        GUI.EndGroup();
        if (max > 0f)
        {
            float h = Mathf.Max(40f, r.height * r.height / content);
            float y = r.y + (r.height - h) * (gridScroll / max);
            Round(new Rect(r.xMax - 7f, y, 4f, h), Alpha(Ice, .35f), 2f);
        }
        if (picked != null) { Select(picked); Click(); }
    }

    // ---- detail --------------------------------------------------------------------------------------------

    private void DrawDetail(Rect r)
    {
        if (selected == null) { Text(r, "Nothing here yet.", 16, Dim, TextAnchor.MiddleCenter); return; }
        Event e = Event.current;
        bool known = context.Knows(selected);
        Rect card = new Rect(r.x + 4f, r.y + 6f, CardW, CardH);

        // The flip: squeeze the card to an edge, swap sides, open it again.
        float t = Ease(flip), sx = Mathf.Abs(Mathf.Cos(t * Mathf.PI));
        DrawGlow(new Rect(card.x - 40f, card.y - 30f, card.width + 80f, card.height + 60f), Alpha(Accent(selected), .20f + .1f * Mathf.Sin(Time.unscaledTime * 2f)));
        Matrix4x4 m = GUI.matrix;
        Vector2 pivot = card.center;
        GUI.matrix = m * Matrix4x4.TRS(pivot, Quaternion.identity, new Vector3(Mathf.Max(.02f, sx), 1f, 1f)) * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
        if (t < .5f) DrawFront(card, selected, false); else DrawBack(card, selected);
        GUI.matrix = m;
        if (known && e.type == EventType.MouseDown && e.button == 0 && card.Contains(mouse)) { Flip(); e.Use(); }

        float by = card.yMax + 14f;
        if (Btn(new Rect(card.x, by, 92f, 44f), "PREV", true, Ice)) Step(-1);
        if (Btn(new Rect(card.x + 102f, by, 140f, 44f), flipped ? "SHOW FRONT" : "FLIP CARD", known, Gold, flipped)) Flip();
        if (Btn(new Rect(card.xMax - 92f, by, 92f, 44f), "NEXT", true, Ice)) Step(1);

        Rect info = new Rect(card.xMax + 24f, r.y + 2f, r.xMax - card.xMax - 28f, r.height - 4f);
        DrawInfo(info, selected, known);
    }

    private void DrawInfo(Rect r, CodexEntry e, bool known)
    {
        float y = r.y;
        if (!known)
        {
            bool lair = e.Kind == CodexKind.Boss;
            Text(new Rect(r.x, y, r.width, 34f), lair ? "UNCHARTED LAIR" : "UNKNOWN BEAST", 24, Faint, TextAnchor.MiddleLeft, true); y += 40f;
            if (lair)
            {
                y = Para(r, y, "Something rules the lair of " + e.Region.name + ". Walk that land to learn what waits there.", 14, Dim);
                y = Para(r, y + 6f, "Danger " + e.Region.bossDepth + ".", 13, Faint);
                return;
            }
            var family = e.Form.Family;
            bool kin = false;
            foreach (var f in family.forms) if (context.Knows(new CodexEntry { Kind = CodexKind.Monster, Id = f.id })) kin = true;
            y = Para(r, y, "Meet it in battle to fill in its card.", 14, Dim);
            y = Para(r, y + 6f, "Rank " + BattleBestiary.Span(e.Form.minRank, e.Form.maxRank) + (e.Form.large ? "  ·  a large beast" : ""), 13, Faint);
            if (kin) y = Para(r, y + 4f, "A form of the " + family.name + ".", 13, Faint);
            string habitat = CodexCards.Habitat(family);
            if (habitat.Length > 0) y = Para(r, y + 4f, "Said to roam: " + habitat + ".", 13, Faint);
            return;
        }

        Text(new Rect(r.x, y, r.width, 34f), e.Name, 24, Color.white, TextAnchor.MiddleLeft, true); y += 34f;
        switch (e.Kind)
        {
            case CodexKind.Monster:
            {
                var form = e.Form; var family = form.Family;
                y = Para(r, y, family.name + "  ·  " + form.element + "  ·  " + form.role + (form.large ? "  ·  Large" : ""), 13, Dim);
                y += 6f;
                y = RankStepper(r, y, form);
                y = StatBars(r, y, CodexCards.Stats(e, viewRank));
                y = Section(r, y, "LORE", (family.lore ?? "") + (string.IsNullOrEmpty(form.flavor) ? "" : "  " + form.flavor));
                var ladder = new List<string>();
                foreach (var f in family.forms)
                    ladder.Add((context.Knows(new CodexEntry { Kind = CodexKind.Monster, Id = f.id }) ? f.name : "???") + " (" + BattleBestiary.Span(f.minRank, f.maxRank) + ")");
                y = Section(r, y, "EVOLUTION", string.Join("  >  ", ladder.ToArray()) + "\n" + CodexCards.EvolutionNote(form));
                if (form.drops != null && form.drops.Length > 0) y = Section(r, y, "DROPS", string.Join(", ", form.drops));
                string habitat = CodexCards.Habitat(family);
                if (habitat.Length > 0) y = Section(r, y, "HABITAT", Capitalise(habitat) + ".");
                break;
            }
            case CodexKind.Boss:
            {
                y = Para(r, y, e.Subtitle, 13, Dim);
                y = Para(r, y + 2f, "Rank " + BattleBestiary.RankName(e.Rank) + "  ·  " + e.Form.element + "  ·  Danger " + e.Region.bossDepth, 13, Dim);
                bool down = context.Defeated(e);
                y = Pill(r.x, y + 8f, down ? "DEFEATED" : "UNDEFEATED", down ? Mint : Red) + 10f;
                y = StatBars(r, y, CodexCards.Stats(e));
                y = Section(r, y, "THE LAIR", e.Region.blurb + " Its court: beasts of the " + e.Lair.theme + ".");
                y = Section(r, y, "LORE", e.Form.Family.lore);
                y = Section(r, y, "SIGNATURE", "Every few rounds it gathers its fury, then unleashes a blow on the whole party. Guard up when you see it coming.");
                break;
            }
            case CodexKind.Fighter:
            {
                y = Para(r, y, e.Subtitle + "  ·  " + e.Element, 13, Dim);
                y += 8f;
                y = StatBars(r, y, CodexCards.Stats(e));
                if (e.Id == "jd")
                    y = Section(r, y, "THE SUMMONER", "JD stands behind the line and plays the party's command cards with AP: draws, energy, banners and decrees. When JD falls, the battle is lost.");
                else
                {
                    var ults = BattleCatalog.Ultimates(e.Fighter); var names = new List<string>();
                    foreach (var u in ults) names.Add(u.Name);
                    var awaken = BattleCatalog.Awakening(e.Fighter);
                    y = Section(r, y, "KIT", BattleCatalog.FighterKit(e.Fighter).Count + " cards in the deck, " + names.Count + " ultimates (" + string.Join(", ", names.ToArray()) + ")"
                        + (awaken != null ? ", and " + awaken.Name + " when they rise from a breakdown." : "."));
                }
                y = Section(r, y, "CARD BACK", "Turn the card over for every move with its cost and effect.");
                break;
            }
            case CodexKind.Roster:
            {
                var u = e.Unit;
                y = Para(r, y, u.epithet, 14, Gold);
                y = Para(r, y + 2f, u.race + "  ·  " + (u.IsHero ? u.cls + "  ·  " + u.role + "  ·  " + u.element : "Resident  ·  " + u.room), 13, Dim);
                bool home = context.Recruits(e);
                if (context.Recruited != null) y = Pill(r.x, y + 8f, home ? "IN THE TOWER" : "NOT YET SUMMONED", home ? Mint : Faint) + 4f;
                y += 6f;
                y = StatBars(r, y, CodexCards.Stats(e));
                if (!string.IsNullOrEmpty(u.quote)) y = Para(r, y + 4f, "“" + u.quote + "”", 14, Gold);
                y = Section(r, y, "PERSONALITY", u.personality);
                y = Section(r, y, "ORIGIN", u.origin);
                break;
            }
        }
    }

    private float RankStepper(Rect r, float y, BestiaryForm form)
    {
        Text(new Rect(r.x, y, 140f, 30f), "SHOWN AT RANK", 12, Faint, TextAnchor.MiddleLeft, true);
        bool span = form.maxRank > form.minRank;
        float x = r.x + 128f;
        if (span && Btn(new Rect(x, y, 34f, 30f), "-", viewRank > form.minRank, Ice)) { viewRank--; Click(); }
        Color c = CodexCards.RankColor(viewRank, Time.unscaledTime);
        Rect gem = new Rect(x + 40f, y, 52f, 30f);
        Round(gem, new Color(.04f, .06f, .1f), 8f);
        Outline(gem, c, 1.5f, 8f);
        Text(gem, BattleBestiary.RankName(viewRank), 15, c, TextAnchor.MiddleCenter, true);
        if (span && Btn(new Rect(x + 98f, y, 34f, 30f), "+", viewRank < form.maxRank, Ice)) { viewRank++; Click(); }
        return y + 40f;
    }

    private float StatBars(Rect r, float y, List<CodexStat> stats)
    {
        foreach (var s in stats)
        {
            Text(new Rect(r.x, y, 46f, 20f), s.Label, 12, Faint, TextAnchor.MiddleLeft, true);
            Bar(new Rect(r.x + 50f, y + 6f, r.width - 106f, 8f), s.Fill, 0f, Color.Lerp(Ice, Gold, s.Fill), Color.clear);
            Text(new Rect(r.xMax - 50f, y, 50f, 20f), s.Value, 13, Color.white, TextAnchor.MiddleRight, true);
            y += 22f;
        }
        return y + 6f;
    }

    private float Section(Rect r, float y, string title, string body)
    {
        if (string.IsNullOrEmpty(body)) return y;
        if (y > r.yMax - 40f) return y;
        Text(new Rect(r.x, y + 6f, r.width, 18f), title, 12, Gold, TextAnchor.MiddleLeft, true);
        return Para(r, y + 26f, body, 13, Dim) + 2f;
    }

    // Wrapped text; returns the y below it.
    private float Para(Rect r, float y, string text, int size, Color c)
    {
        if (string.IsNullOrEmpty(text)) return y;
        float h = Measure(text, size, r.width);
        h = Mathf.Min(h, Mathf.Max(0f, r.yMax - y));
        Text(new Rect(r.x, y, r.width, h), text, size, c, TextAnchor.UpperLeft, false, true);
        return y + h;
    }

    private float Measure(string text, int size, float width)
    {
        if (measure == null) measure = new GUIStyle(GUI.skin.label) { wordWrap = true, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
        if (BattleGui.Display != null) measure.font = BattleGui.Display;
        measure.fontSize = size;
        return measure.CalcHeight(new GUIContent(text), width) + 2f;
    }

    private static float Pill(float x, float y, string label, Color c)
    {
        float w = label.Length * 8.2f + 24f;
        Rect r = new Rect(x, y, w, 24f);
        Round(r, Alpha(c, .16f), 12f);
        Outline(r, Alpha(c, .8f), 1.2f, 12f);
        Text(r, label, 11, c, TextAnchor.MiddleCenter, true);
        return r.yMax;
    }

    private static string Capitalise(string s) { return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1); }

    // ---- cards ---------------------------------------------------------------------------------------------

    private Color Accent(CodexEntry e)
    {
        if (e.Kind == CodexKind.Boss) return new Color(1f, .42f, .36f);
        return e.Rank > 0 ? CodexCards.RankColor(e.Rank, Time.unscaledTime) : Gold;
    }

    // The front: art, then rank gem, element tag and name plate over it. compact: the small grid copy (name only).
    private void DrawFront(Rect r, CodexEntry e, bool compact)
    {
        float s = r.width / CardW;
        bool known = context.Knows(e);
        Color accent = known ? Accent(e) : Faint;
        Color element = CodexCards.ElementColor(e.Element);
        float radius = compact ? 9f : 16f;
        Round(r, new Color(.025f, .035f, .06f), radius);
        Rect art = new Rect(r.x + 4f * s + 1f, r.y + 4f * s + 1f, r.width - 8f * s - 2f, r.height - 8f * s - 2f);
        float artRadius = radius - 3f;

        if (!known) DrawSilhouette(art, e, artRadius);
        else
        {
            var tex = CodexCards.Front(e, compact);
            if (tex != null) GUI.DrawTexture(art, tex, ScaleMode.ScaleAndCrop, false, 0f, Color.white, 0f, artRadius);
            else DrawStandIn(art, e, artRadius, element);
        }
        GradientDown(new Rect(art.x, art.y, art.width, art.height * .2f), new Color(0f, 0f, 0f, .55f));
        GradientUp(new Rect(art.x, art.yMax - art.height * (compact ? .34f : .38f), art.width, art.height * (compact ? .34f : .38f)), new Color(0f, 0f, 0f, .94f));

        // Rank gem (fighters have none: a role tag instead).
        float gemR = compact ? 15f : 24f;
        Vector2 gc = new Vector2(art.x + gemR + (compact ? 5f : 9f), art.y + gemR + (compact ? 5f : 9f));
        if (e.Rank > 0)
        {
            Color rc = known || e.Kind != CodexKind.Monster ? CodexCards.RankColor(e.Rank, Time.unscaledTime) : Faint;
            DrawGlow(gc, gemR * 1.8f, Alpha(rc, .35f));
            Disc(gc, gemR, new Color(.03f, .04f, .07f, .92f));
            Ring(gc, gemR, compact ? 1.6f : 2.4f, rc);
            string rn = e.Kind == CodexKind.Monster && !known ? "?" : BattleBestiary.RankName(e.Rank);
            Text(new Rect(gc.x - gemR, gc.y - gemR, gemR * 2f, gemR * 2f), rn, (compact ? 13 : 20) - (rn.Length > 1 ? (compact ? 3 : 5) : 0), rc, TextAnchor.MiddleCenter, true);
        }
        else if (!compact) Tag(new Rect(art.x + 9f, art.y + 10f, 104f, 26f), e.Id == "jd" ? "SUMMONER" : "FIGHTER", Gold, false);

        // Element tag, top right.
        if (known && !string.IsNullOrEmpty(e.Element))
        {
            float w = compact ? 52f : 92f, h = compact ? 18f : 26f;
            Tag(new Rect(art.xMax - w - (compact ? 5f : 9f), art.y + (compact ? 6f : 10f), w, h), e.Element.ToUpperInvariant(), element, compact);
        }
        else if (known && e.Kind == CodexKind.Roster)
        {
            float w = compact ? 64f : 104f, h = compact ? 18f : 26f;
            Tag(new Rect(art.xMax - w - (compact ? 5f : 9f), art.y + (compact ? 6f : 10f), w, h), "RESIDENT", new Color(.92f, .86f, .72f), compact);
        }

        if (e.Kind == CodexKind.Boss && known)
        {
            Rect ribbon = new Rect(r.center.x - (compact ? 40f : 70f), art.y + (compact ? 30f : 46f), compact ? 80f : 140f, compact ? 16f : 26f);
            Round(ribbon, new Color(.45f, .06f, .05f, .92f), 4f);
            Outline(ribbon, new Color(1f, .7f, .4f, .9f), 1.2f, 4f);
            Text(ribbon, "LAIR BOSS", compact ? 9 : 13, new Color(1f, .86f, .6f), TextAnchor.MiddleCenter, true);
        }

        // Name plate.
        string name = known ? e.Name : e.Kind == CodexKind.Boss ? "Uncharted lair" : "???";
        if (compact)
        {
            Text(new Rect(art.x + 6f, art.yMax - 46f, art.width - 12f, 40f), name, 12, known ? Color.white : Faint, TextAnchor.LowerCenter, true, true, 1f);
            if (known && context.Defeated(e)) Text(new Rect(art.x, art.yMax - 64f, art.width, 16f), "DEFEATED", 9, Mint, TextAnchor.MiddleCenter, true, false, 1f);
            if (known && context.Recruits(e)) Text(new Rect(art.x, art.yMax - 62f, art.width, 16f), "IN THE TOWER", 9, Mint, TextAnchor.MiddleCenter, true, false, 1f);
        }
        else
        {
            Text(new Rect(art.x + 14f, art.yMax - 120f, art.width - 28f, 34f), name, 23, known ? Color.white : Faint, TextAnchor.MiddleLeft, true, false, 1.5f);
            string sub = known ? e.Subtitle : e.Kind == CodexKind.Boss ? "Lair of " + e.Region.name : "Unknown beast  ·  rank " + BattleBestiary.Span(e.Form.minRank, e.Form.maxRank);
            Text(new Rect(art.x + 14f, art.yMax - 88f, art.width - 28f, 20f), sub, 13, Dim, TextAnchor.MiddleLeft, false, false, 1f);
            Fill(new Rect(art.x + 14f, art.yMax - 62f, art.width - 28f, 1f), Alpha(accent, .45f));
            if (known)
            {
                var stats = CodexCards.FrontStats(e);
                float cw = (art.width - 28f) / Mathf.Max(1, stats.Count);
                for (int i = 0; i < stats.Count; i++)
                {
                    Rect c = new Rect(art.x + 14f + i * cw, art.yMax - 56f, cw, 46f);
                    Text(new Rect(c.x, c.y, c.width, 16f), stats[i].Label, 11, accent, TextAnchor.MiddleCenter, true);
                    Text(new Rect(c.x, c.y + 16f, c.width, 26f), stats[i].Value, 20, Color.white, TextAnchor.MiddleCenter, true, false, 1f);
                }
            }
            else Text(new Rect(art.x + 14f, art.yMax - 56f, art.width - 28f, 44f), e.Kind == CodexKind.Boss ? "Walk its land to reveal it" : "Meet it in battle to reveal it",
                14, Faint, TextAnchor.MiddleCenter, true);
            if (known && (context.Defeated(e) || context.Recruits(e)))
            {
                string stamp = context.Defeated(e) ? "DEFEATED" : "IN THE TOWER";
                Rect st = new Rect(art.xMax - 130f, art.yMax - 150f, 116f, 24f);
                Round(st, new Color(.02f, .12f, .09f, .85f), 12f);
                Outline(st, Mint, 1.2f, 12f);
                Text(st, stamp, 11, Mint, TextAnchor.MiddleCenter, true);
            }
        }
        Outline(art, new Color(1f, 1f, 1f, .10f), 1f, artRadius);
        Outline(r, Alpha(accent, known ? .95f : .5f), compact ? 1.8f : 2.6f, radius);
        if (e.Kind == CodexKind.Boss && known) Outline(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), new Color(1f, .3f, .25f, .6f), 1.2f, radius + 3f);
    }

    private static void Tag(Rect r, string label, Color c, bool compact)
    {
        Round(r, new Color(c.r * .22f, c.g * .22f, c.b * .22f, .88f), r.height * .5f);
        Outline(r, Alpha(c, .9f), compact ? 1f : 1.4f, r.height * .5f);
        Text(r, label, compact ? 8 : 12, c, TextAnchor.MiddleCenter, true);
    }

    // An unmet beast: its shape in black against a dim fog, with a faint rim; a sigil while no clean shape exists.
    private static void DrawSilhouette(Rect art, CodexEntry e, float radius)
    {
        Round(art, new Color(.045f, .055f, .085f), radius);
        DrawGlow(new Rect(art.x - art.width * .1f, art.y + art.height * .12f, art.width * 1.2f, art.height * .7f), new Color(.35f, .45f, .65f, .22f));
        var shape = CodexCards.Silhouette(e);
        Color before = GUI.color;
        if (shape == null)
        {
            Vector2 c = new Vector2(art.center.x, art.y + art.height * .42f);
            float rr = art.width * .26f;
            Ring(c, rr, Mathf.Max(1.5f, rr * .06f), new Color(.38f, .46f, .62f, .55f));
            Text(new Rect(c.x - rr, c.y - rr, rr * 2f, rr * 2f), "?", Mathf.RoundToInt(rr * 1.1f), new Color(.42f, .5f, .66f, .8f), TextAnchor.MiddleCenter, true);
            return;
        }
        Rect fit = Fit(new Rect(art.x + art.width * .08f, art.y + art.height * .12f, art.width * .84f, art.height * .56f), shape);
        float rim = Mathf.Max(1.5f, art.width * .008f);
        GUI.color = new Color(.5f, .62f, .85f, .45f);
        GUI.DrawTexture(new Rect(fit.x - rim, fit.y - rim, fit.width + rim * 2f, fit.height + rim * 2f), shape, ScaleMode.StretchToFill, true);
        GUI.color = new Color(.01f, .012f, .02f, 1f);
        GUI.DrawTexture(fit, shape, ScaleMode.StretchToFill, true);
        GUI.color = before;
    }

    // A monster whose card is not painted yet (or a lair boss of one): its battle cutout over an element wash.
    private static void DrawStandIn(Rect art, CodexEntry e, float radius, Color element)
    {
        Round(art, new Color(element.r * .16f, element.g * .16f, element.b * .2f + .04f), radius);
        DrawGlow(new Rect(art.x - art.width * .2f, art.y, art.width * 1.4f, art.height * .9f), Alpha(element, .28f));
        var cut = CodexCards.Cutout(e);
        if (cut == null) return;
        Rect fit = Fit(new Rect(art.x + art.width * .06f, art.y + art.height * .1f, art.width * .88f, art.height * .6f), cut);
        GUI.DrawTexture(fit, cut, ScaleMode.StretchToFill, true);
    }

    private static Rect Fit(Rect box, Texture tex)
    {
        float aspect = (float)tex.width / Mathf.Max(1, tex.height);
        float w = box.width, h = w / aspect;
        if (h > box.height) { h = box.height; w = h * aspect; }
        return new Rect(box.center.x - w * .5f, box.yMax - h, w, h);
    }

    // The back: a monster's painted move card with its moves in the panels, or a drawn move list.
    private void DrawBack(Rect r, CodexEntry e)
    {
        Color accent = Accent(e);
        var rows = moves ?? CodexCards.Moves(e);
        var painted = CodexCards.MovesArt(e);
        if (painted != null && rows.Count > 0 && rows.Count <= 4)
        {
            GUI.DrawTexture(r, painted, ScaleMode.ScaleAndCrop, false, 0f, Color.white, 0f, 16f);
            GradientDown(new Rect(r.x, r.y, r.width, 70f), new Color(0f, 0f, 0f, .7f));
            Text(new Rect(r.x + 16f, r.y + 10f, r.width - 32f, 28f), e.Name.ToUpperInvariant(), 17, Color.white, TextAnchor.MiddleCenter, true, false, 1.5f);
            Text(new Rect(r.x + 16f, r.y + 36f, r.width - 32f, 18f), e.Kind == CodexKind.Boss ? "LAIR BOSS MOVES" : "MOVES", 11, accent, TextAnchor.MiddleCenter, true, false, 1f);
            var panels = CodexCards.Panels(e);
            for (int i = 0; i < rows.Count && i < panels.Length; i++)
            {
                Rect p = panels[i];
                PanelRow(new Rect(r.x + p.x * r.width, r.y + p.y * r.height, p.width * r.width, p.height * r.height), rows[i], accent, true);
            }
            Outline(r, Alpha(accent, .95f), 2.6f, 16f);
            return;
        }
        Color element = CodexCards.ElementColor(e.Element);
        Round(r, new Color(.03f + element.r * .05f, .04f + element.g * .05f, .07f + element.b * .06f), 16f);
        DrawGlow(new Rect(r.x - 40f, r.y - 60f, r.width + 80f, 220f), Alpha(element, .22f));
        Text(new Rect(r.x + 18f, r.y + 14f, r.width - 36f, 28f), e.Name.ToUpperInvariant(), 18, Color.white, TextAnchor.MiddleLeft, true, false, 1.5f);
        string what = e.Kind == CodexKind.Roster ? (e.Unit.IsHero ? "ARMS & ABILITIES" : "TRADE & TOOLS") : e.Kind == CodexKind.Fighter ? (e.Id == "jd" ? "COMMAND CARDS & DECREES" : "DECK, ULTIMATES & AWAKENING") : "MOVES";
        Text(new Rect(r.x + 18f, r.y + 42f, r.width - 36f, 18f), what, 11, accent, TextAnchor.MiddleLeft, true);
        Fill(new Rect(r.x + 18f, r.y + 64f, r.width - 36f, 1f), Alpha(accent, .4f));
        float top = r.y + 74f, bottom = r.yMax - 12f;
        if (rows.Count == 0) { Text(new Rect(r.x, top, r.width, 40f), "No moves on record.", 13, Dim, TextAnchor.MiddleCenter); }
        else
        {
            float gap = 6f, h = Mathf.Min(86f, (bottom - top - gap * (rows.Count - 1)) / rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                Rect row = new Rect(r.x + 12f, top + i * (h + gap), r.width - 24f, h);
                Round(row, new Color(0f, 0f, 0f, .38f), 8f);
                Outline(row, Alpha(TagColor(rows[i].Tag, accent), .35f), 1f, 8f);
                PanelRow(row, rows[i], accent, false);
            }
        }
        Outline(r, Alpha(accent, .95f), 2.6f, 16f);
    }

    // One move: name and kind on top, cost at the right, what it does below (shrunk to fit short panels).
    private void PanelRow(Rect p, CodexMove m, Color accent, bool painted)
    {
        float pad = painted ? 7f : 9f;
        Color tc = TagColor(m.Tag, accent);
        float costW = m.Cost >= 0 ? 40f : 0f;
        int nameSize = p.height < 44f ? 12 : 13;
        Text(new Rect(p.x + pad, p.y + 3f, p.width - pad * 2f - costW, 17f), m.Name, nameSize, Color.white, TextAnchor.MiddleLeft, true, false, painted ? 1f : 0f);
        float nameW = NameWidth(m.Name, nameSize);
        if (!string.IsNullOrEmpty(m.Tag) && nameW + 70f < p.width - pad * 2f - costW)
            Text(new Rect(p.x + pad + nameW + 8f, p.y + 3f, 80f, 17f), m.Tag.ToUpperInvariant(), 9, tc, TextAnchor.MiddleLeft, true, false, painted ? 1f : 0f);
        if (m.Cost >= 0)
        {
            Rect c = new Rect(p.xMax - pad - costW, p.y + 3f, costW, 17f);
            Text(c, m.Cost + " " + m.CostUnit, 11, m.CostUnit == "AP" ? Gold : Ice, TextAnchor.MiddleRight, true, false, painted ? 1f : 0f);
        }
        float textTop = p.y + 20f, room = p.yMax - textTop - 2f;
        if (room < 8f || string.IsNullOrEmpty(m.Text)) return;
        int size = 11;
        float w = p.width - pad * 2f;
        while (size > 8 && Measure(m.Text, size, w) > room + 2f) size--;
        Text(new Rect(p.x + pad, textTop, w, room), m.Text, size, new Color(.86f, .9f, .96f), TextAnchor.UpperLeft, false, true, painted ? 1f : 0f);
    }

    private float NameWidth(string name, int size)
    {
        if (measure == null) Measure(name, size, 100f);
        measure.fontSize = size;
        measure.fontStyle = FontStyle.Bold;
        float w = measure.CalcSize(new GUIContent(name)).x;
        measure.fontStyle = FontStyle.Normal;
        return w;
    }

    private static Color TagColor(string tag, Color fallback)
    {
        switch (tag)
        {
            case "Attack": return new Color(1f, .62f, .5f);
            case "Area": return new Color(1f, .74f, .38f);
            case "Control": return new Color(.78f, .6f, 1f);
            case "Guard": return new Color(.55f, .8f, 1f);
            case "Support": return new Color(.5f, 1f, .7f);
            case "Signature": case "Ultimate": case "Decree": return new Color(1f, .86f, .4f);
            case "Awakening": case "Charge": return new Color(1f, .5f, .6f);
            case "Passive": case "Perk": return new Color(.78f, .84f, .92f);
        }
        return fallback;
    }
}
