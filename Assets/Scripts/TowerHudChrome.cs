using System;
using System.Collections.Generic;
using AdamsHaven.Tower;
using UnityEngine;
using UnityEngine.UI;

// Live-service style chrome for the Tower HUD (Wuthering Waves / Genshin): round line-icon buttons with small
// captions and red badges, compact resource chips, and a full-screen menu (the Genshin "Paimon menu" pattern)
// that gathers every system on one screen.
public sealed partial class TowerHud
{
    private static readonly Color Violet = new Color(0.45f, 0.33f, 0.72f);
    private static readonly Color Muted = new Color(0.32f, 0.36f, 0.40f);

    private readonly Dictionary<Button, Image> badges = new Dictionary<Button, Image>();
    private Image heartGlow;

    // resource chips: firewood, food, water, gold, celestium, sigils
    private static readonly string[] ChipNames = { "FIREWOOD", "FOOD", "WATER", "GOLD", "CELESTIUM", "SIGILS" };
    private static readonly Color[] ChipColors = {
        new Color(1f, 0.55f, 0.28f), new Color(0.62f, 0.86f, 0.42f), new Color(0.40f, 0.74f, 1f),
        new Color(1f, 0.83f, 0.36f), new Color(0.48f, 0.92f, 0.94f), new Color(0.80f, 0.62f, 1f) };
    private readonly Text[] chipValues = new Text[6];
    private Text peopleText;

    // full-screen menu
    private Text menuDay, menuStats, menuValues, menuRun;
    private Button menuSteward;

    // ---------------------------------------------------------------- widgets

    private static Image Pill(Image image, float alpha)
    {
        image.sprite = TowerUiSkin.Rounded;
        image.type = Image.Type.Sliced;
        float h = image.rectTransform.sizeDelta.y;
        image.pixelsPerUnitMultiplier = Mathf.Max(1.6f, 40f / Mathf.Max(8f, h));
        var c = TowerUiSkin.GlassFill;
        image.color = new Color(c.r, c.g, c.b, alpha);
        return image;
    }

    // A round icon button with a caption under it. The caption is the button's first Text, so LabelOf()
    // still finds it. Gold tint = active, Alert = danger, Muted = off; the rim and icon show it.
    private Button IconButtonAt(Transform parent, string name, string sheet, string state, string caption,
        float x, float y, float size, Action tap, Color tint)
    {
        var image = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(x, -y - size), new Vector2(x + size, -y), tint);
        var button = image.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
        colors.pressedColor = new Color(0.80f, 0.80f, 0.80f);
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
        colors.fadeDuration = 0.06f;
        button.colors = colors;
        var label = TextAt(image.transform, name + " label", caption, -24, size + 2, size + 48, 15, 11,
            TowerUiSkin.TextMain, TextAnchor.UpperCenter);
        label.fontStyle = FontStyle.Bold;
        var outline = label.gameObject.AddComponent<Shadow>();
        outline.effectColor = new Color(0, 0, 0, 0.75f);
        outline.effectDistance = new Vector2(0, -1);
        var fx = image.gameObject.AddComponent<TowerButtonFx>();
        fx.iconMode = true;
        TowerUiSkin.Apply(image, tint);
        var sprite = TowerUiSkin.Icon(sheet, state);
        var iconImage = Box(name + " icon", image.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(size * 0.56f, size * 0.56f), Color.white);
        iconImage.sprite = sprite; iconImage.preserveAspect = true; iconImage.raycastTarget = false;
        iconImage.enabled = sprite != null;
        fx.icon = iconImage;
        fx.Restyle();
        if (sprite == null)   // no art: fall back to the caption's first letter inside the circle
            TextAt(image.transform, name + " glyph", caption.Substring(0, 1), 0, 0, size, size, (int)(size * 0.4f),
                TowerUiSkin.TextMain, TextAnchor.MiddleCenter);
        if (tap != null) button.onClick.AddListener(() => tap());
        return button;
    }

    private void SetIcon(Button button, string sheet, string state)
    {
        var fx = button.GetComponent<TowerButtonFx>();
        if (fx == null || fx.icon == null) return;
        var sprite = TowerUiSkin.Icon(sheet, state);
        if (sprite != null) fx.icon.sprite = sprite;
    }

    // Press-and-hold on an icon button opens a flyout of related shortcuts; a thin bar fills while holding.
    private void AttachHold(Button button, Func<FlyItem[]> items, bool flyUp, Action tap)
    {
        var rect = button.GetComponent<RectTransform>();
        float size = rect.sizeDelta.x;
        var hold = button.gameObject.AddComponent<TowerHoldButton>();
        var bar = Rect("Hold progress", button.transform, Vector2.zero, Vector2.zero, Vector2.zero,
            Vector2.zero, TowerUiSkin.Accent);
        bar.raycastTarget = false;
        var barRect = bar.rectTransform;
        barRect.pivot = Vector2.zero; barRect.anchoredPosition = new Vector2(size * 0.2f, -5);
        barRect.sizeDelta = new Vector2(0, 2);
        hold.progress = barRect; hold.fullWidth = size * 0.6f;
        hold.onHold = () => ShowFlyout(rect, flyUp, items());
        button.onClick.AddListener(() => { if (hold.Consume()) return; tap(); });
    }

    // Red notification badge on the button's top-right; count 0 hides it, count < 0 shows a plain dot.
    private void SetBadge(Button button, int count)
    {
        Image badge;
        if (!badges.TryGetValue(button, out badge))
        {
            badge = Box("Badge", button.transform, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-5, -5),
                new Vector2(16, 16), new Color(0.90f, 0.27f, 0.22f));
            badge.sprite = TowerUiSkin.Pip("pip_alert") ?? TowerUiSkin.Circle; badge.raycastTarget = false;
            if (badge.sprite != TowerUiSkin.Circle) badge.color = Color.white;
            TextAt(badge.transform, "Badge count", "", 0, 0, 16, 16, 10, Color.white, TextAnchor.MiddleCenter)
                .fontStyle = FontStyle.Bold;
            badges[button] = badge;
        }
        badge.gameObject.SetActive(count != 0);
        badge.rectTransform.sizeDelta = count < 0 ? new Vector2(9, 9) : new Vector2(16, 16);
        var text = badge.GetComponentInChildren<Text>();
        text.text = count > 0 ? (count > 9 ? "9+" : count.ToString()) : "";
    }

    // Section title in the live-service style: a short gold accent bar, then the title.
    private Text TitleAt(Transform parent, string name, string value, float x, float y, float width, int size = 17)
    {
        var bar = Rect(name + " accent", parent, new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(x, -y - 21), new Vector2(x + 3, -y - 5), TowerUiSkin.Accent);
        bar.raycastTarget = false;
        var text = TextAt(parent, name, value, x + 11, y, width - 11, 26, size, TowerUiSkin.TextMain);
        text.fontStyle = FontStyle.Bold;
        return text;
    }

    // ---------------------------------------------------------------- top HUD

    private void BuildTopChrome()
    {
        var shadeImage = Rect("Top shade", safeRoot, new Vector2(0, 1), new Vector2(1, 1),
            new Vector2(0, -96), new Vector2(0, 0), new Color(0.02f, 0.03f, 0.05f, 0.62f));
        shadeImage.sprite = TowerUiSkin.Shade; shadeImage.raycastTarget = false;

        // Top-left: day and clock, a thin threat line, population.
        var left = Box("Top left", safeRoot, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -8),
            new Vector2(270, 64), Color.clear);
        left.raycastTarget = false;
        clockText = TextAt(left.transform, "Day", "", 0, 0, 270, 24, 19, TowerUiSkin.TextMain);
        clockText.fontStyle = FontStyle.Bold;
        var bar = Rect("Threat bar", left.transform, new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(0, -32), new Vector2(96, -28), new Color(1, 1, 1, 0.14f));
        bar.raycastTarget = false;
        threatFill = Rect("Threat fill", bar.transform, Vector2.zero, new Vector2(0.3f, 1),
            Vector2.zero, Vector2.zero, Teal);
        threatFill.raycastTarget = false;
        threatText = TextAt(left.transform, "Threat label", "", 104, 22, 170, 16, 12, HudLabel);
        peopleText = TextAt(left.transform, "People", "", 0, 40, 270, 20, 14, TowerUiSkin.TextMain);
        peopleText.fontStyle = FontStyle.Bold;
        foreach (var text in left.GetComponentsInChildren<Text>())   // stays readable over bright sky
            Legible(text);

        // Top centre: resource chips (tap any for the full materials list).
        const float chipW = 104, gap = 6;
        var strip = Box("Resource strip", safeRoot, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-20, -9),
            new Vector2(6 * chipW + 5 * gap, 36), Color.clear);
        var stripButton = strip.gameObject.AddComponent<Button>();
        stripButton.transition = Selectable.Transition.None;
        stripButton.onClick.AddListener(() =>
        {
            materialsPanel.gameObject.SetActive(!materialsPanel.gameObject.activeSelf);
            materialsTimer = 7f; Refresh();
        });
        for (int i = 0; i < ChipNames.Length; i++)
        {
            var chip = Box("Chip " + ChipNames[i], strip.transform, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(i * (chipW + gap), 0), new Vector2(chipW, 40), Color.white);
            bool art = TowerUiSkin.ApplyChip(chip, 40);
            if (!art) Pill(chip, 0.72f);
            chip.raycastTarget = false;
            float socket = art ? 23 : 13, textX = art ? 44 : 24;   // the art has a diamond socket on its left end
            var gem = Box("Gem", chip.transform, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(socket, 0),
                new Vector2(11, 11), ChipColors[i]);
            gem.sprite = TowerUiSkin.Diamond; gem.raycastTarget = false;
            var chipName = TextAt(chip.transform, "Name", ChipNames[i], textX, 5, chipW - textX - 6, 13,
                ChipNames[i].Length > 7 ? 9 : 10, HudLabel);
            chipName.fontStyle = FontStyle.Bold;
            chipName.horizontalOverflow = HorizontalWrapMode.Overflow;   // CELESTIUM must not be cut off
            Legible(chipName);
            chipValues[i] = TextAt(chip.transform, "Value", "", textX, 17, chipW - textX - 6, 18, 14, TowerUiSkin.TextMain);
            chipValues[i].fontStyle = FontStyle.Bold;
            Legible(chipValues[i]);
            chipValues[i].supportRichText = true;
        }
        resources = TextAt(strip.transform, "Resources", "", 0, 0, 1, 1, 1, Color.clear);   // kept for callers
        resources.gameObject.SetActive(false);

        materialsPanel = Box("Materials", safeRoot, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(-20, -50), new Vector2(560, 30), Color.white);
        Pill(materialsPanel, 0.92f).raycastTarget = false;
        materialsText = TextAt(materialsPanel.transform, "Materials text", "", 8, 0, 544, 30, 12, TowerUiSkin.TextMain,
            TextAnchor.MiddleCenter);
        materialsPanel.gameObject.SetActive(false);

        // Top-right: Alerts, Goals, Steward, Speed, Menu.
        var right = Box("Top right", safeRoot, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -8),
            new Vector2(5 * 50, 100), Color.clear);
        right.raycastTarget = false;
        var t = right.transform;
        alertsButton = IconButtonAt(t, "Alerts", "icons_utility", "alerts", "Alerts", 5, 0, 40,
            () => TogglePopup(popupAlerts), Teal);
        goalsButton = IconButtonAt(t, "Goals", "icons_utility", "goals", "Goals", 55, 0, 40, null, Teal);
        AttachHold(goalsButton, TaskItems, false, () => TogglePopup(popupTasks));
        stewardTop = IconButtonAt(t, "Steward toggle", "icons_utility", "steward", "Steward", 105, 0, 40,
            () => tower.Apply(tower.Rules.SetSteward(!tower.Rules.State.steward)), Teal);
        timeButton = IconButtonAt(t, "Time", "icons_utility", "speed_x1", "1×", 155, 0, 40, null, Teal);
        AttachHold(timeButton, TimeItems, false, () => tower.SetSpeed(tower.Speed == 0 ? restoreSpeed : 0));
        var menu = IconButtonAt(t, "Menu", "icons_utility", "menu", "Menu", 205, 0, 40, null, Teal);
        AttachHold(menu, MenuItems, false, () => TogglePopup(popupMenu));

#if UNITY_EDITOR || DEBUG
        // Debug shortcut (editor and development builds only): open the expedition map without a Guild.
        var test = ButtonAt(right.transform, "Expedition test", "DEBUG: EXPEDITION MAP", 96, 66, 150, 24,
            () => { CloseAllPopups(); TowerRules.DebugIgnoreGuild = true; tower.OpenExpedition(); }, Teal, 10);
        test.GetComponent<TowerButtonFx>().Restyle();
#endif
    }

    // Light label colour and a thin dark outline so small HUD text stays readable over bright sky.
    private static readonly Color HudLabel = new Color(0.88f, 0.92f, 0.98f);

    private static void Legible(Text text)
    {
        if (text == null) return;
        var shadow = text.GetComponent<Shadow>() ?? text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.85f);
        shadow.effectDistance = new Vector2(0, -1.5f);
        if (text.GetComponent<Outline>() == null)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.02f, 0.03f, 0.05f, 0.7f);
            outline.effectDistance = new Vector2(1f, -1f);
        }
    }

    private void RefreshTopChrome(TowerState state)
    {
        bool live = state.introPhase == "complete";
        chipValues[0].text = Mathf.CeilToInt(state.firewood) + Arrow("firewood", live);
        chipValues[1].text = Mathf.CeilToInt(state.food) + Arrow("food", live);
        chipValues[2].text = Mathf.CeilToInt(state.water) + Arrow("water", live);
        chipValues[3].text = state.gold.ToString();
        chipValues[4].text = state.celestium.ToString();
        chipValues[5].text = state.sigils.ToString();
        peopleText.text = "PEOPLE " + tower.Rules.BiologicalPopulation() + "/" + tower.Rules.PopulationCap() +
            "   " + SatisfactionLabel(state);
        materialsText.text = TrendLine() + "WOOD " + state.wood + "   STONE " + state.stone + "   ORE " + state.ore +
            "   TONICS " + state.tonics + "   ESSENCE " + state.essence;
        LabelOf(timeButton).text = tower.Speed == 0 ? "Paused" : tower.Speed + "×";
        SetIcon(timeButton, "icons_utility", tower.Speed == 0 ? "pause" : tower.Speed >= 2 ? "speed_x2" : "speed_x1");
        timeButton.GetComponent<Image>().color = tower.Speed == 0 ? Gold : tower.Speed >= 4 ? Violet : Teal;
    }

    // Falling stock shows a red down arrow with its rate; rising shows green.
    private string Arrow(string resource, bool live)
    {
        if (!live) return "";
        float net = tower.Rules.NetPerMinute(resource);
        if (Mathf.Abs(net) < 0.05f) return "";
        return net < 0 ? " <size=10><color=#ff8a70>↓" + (-net).ToString("0.0") + "</color></size>" :
            " <size=10><color=#8fe39a>↑" + net.ToString("0.0") + "</color></size>";
    }

    private string TrendLine()
    {
        if (tower.Rules.State.introPhase != "complete") return "";
        return "PER MIN  firewood" + Trend("firewood") + "  food" + Trend("food") + "  water" + Trend("water") + "     ";
    }

    // ---------------------------------------------------------------- dock

    private void BuildDockChrome()
    {
        var shade = Rect("Bottom shade", safeRoot, new Vector2(0, 0), new Vector2(1, 0),
            Vector2.zero, new Vector2(0, 110), new Color(0.02f, 0.03f, 0.05f, 0.55f));
        shade.sprite = TowerUiSkin.Shade; shade.raycastTarget = false;
        shade.rectTransform.localScale = new Vector3(1, -1, 1);   // fade upward from the bottom edge
        var dock = Box("Dock", safeRoot, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 22),
            new Vector2(440, 72), Color.clear);
        dock.raycastTarget = false;
        var t = dock.transform;
        dockBuild = IconButtonAt(t, "Dock build", "icons_dock", "build", "Build", 14, 14, 52, null, Teal);
        AttachHold(dockBuild, BuildItems, true, () => TogglePopup(popupBuild));
        dockPeople = IconButtonAt(t, "Dock people", "icons_dock", "people", "People", 100, 14, 52, null, Teal);
        AttachHold(dockPeople, PeopleItems, true, TogglePeople);
        // The Heart is the larger centre button; a gold ring breathes while a summon or upgrade is waiting.
        heartGlow = Box("Heart glow", t, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(220, -35),
            new Vector2(86, 86), new Color(1f, 0.85f, 0.5f, 0));
        heartGlow.sprite = TowerUiSkin.Flare("ring_bloom") ?? TowerUiSkin.Ring; heartGlow.raycastTarget = false;
        if (heartGlow.sprite != TowerUiSkin.Ring) heartGlow.rectTransform.sizeDelta = new Vector2(118, 98);
        dockHeart = IconButtonAt(t, "Dock heart", "icons_dock", "heart", "Heart", 186, 1, 68, OpenHeart, Violet);
        dockExpeditions = IconButtonAt(t, "Dock expeditions", "icons_dock", "map", "Expeditions", 288, 14, 52,
            OpenGuildBoard, Teal);
        dockBattle = IconButtonAt(t, "Dock battle", "icons_dock", "battle", "Battle", 374, 14, 52,
            () => { CloseAllPopups(); tower.LaunchBattleExpedition(); }, Alert);
    }

    private void PulseHeart()
    {
        if (heartGlow == null) return;
        float glow = HeartReady() ? 0.35f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.5f)) : 0;
        heartGlow.color = new Color(1f, 0.85f, 0.5f, glow);
        float s = 1f + 0.06f * glow;
        heartGlow.rectTransform.localScale = new Vector3(s, s, 1);
    }

    // ---------------------------------------------------------------- full-screen menu

    private struct MenuTile
    {
        public string sheet, state, caption; public Action action; public Color tint;
        public MenuTile(string sheet, string state, string caption, Color tint, Action action)
        { this.sheet = sheet; this.state = state; this.caption = caption; this.tint = tint; this.action = action; }
    }

    private void BuildMainMenu()
    {
        popupMenu = Rect("Menu popup", safeRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0.02f, 0.03f, 0.05f, 0.86f));
        var catcher = popupMenu.gameObject.AddComponent<Button>();
        catcher.transition = Selectable.Transition.None;
        catcher.onClick.AddListener(CloseAllPopups);

        // Left: a profile card, like the traveler card in the Paimon menu.
        var card = Box("Menu profile", popupMenu.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(64, 0), new Vector2(340, 380), Color.white);
        TowerUiSkin.ApplyPanel(card, Glass, true);
        card.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;   // swallow taps
        var c = card.transform;
        TitleAt(c, "Menu title", "CELESTIUM TOWER", 22, 22, 300, 19);
        menuDay = TextAt(c, "Menu day", "", 24, 56, 296, 30, 24, TowerUiSkin.TextMain);
        menuDay.fontStyle = FontStyle.Bold;
        var line = Rect("Menu line", c, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -98),
            new Vector2(316, -97), new Color(1, 1, 1, 0.12f));
        line.raycastTarget = false;
        menuStats = TextAt(c, "Menu stats", "Heart rank\nThreat\nPeople\nRegions\nSigils\nCelestium\nGold",
            24, 110, 116, 170, 14, TowerUiSkin.TextDim, TextAnchor.UpperLeft);
        menuStats.lineSpacing = 1.3f;
        menuValues = TextAt(c, "Menu values", "", 140, 110, 180, 170, 14, TowerUiSkin.TextMain, TextAnchor.UpperLeft);
        menuValues.lineSpacing = 1.3f;
        menuValues.fontStyle = FontStyle.Bold;
        menuRun = TextAt(c, "Menu run", "", 24, 266, 296, 36, 12, TowerUiSkin.TextDim, TextAnchor.UpperLeft);
        ButtonAt(c, "Menu save", "SAVE NOW", 24, 316, 140, 40, () => { tower.SaveNow(); CloseAllPopups(); }, Gold, 14);
        ButtonAt(c, "Menu checkpoints", "CHECKPOINTS", 176, 316, 140, 40,
            () => { CloseAllPopups(); saveOverlay.gameObject.SetActive(true); }, Teal, 13);

        // Right: every system as an icon tile.
        var tiles = new[] {
            new MenuTile("icons_dock", "build", "Build", Teal, () => OpenBuild("all")),
            new MenuTile("icons_dock", "people", "People", Teal, () => { HidePopups(); OpenPeople("work"); }),
            new MenuTile("icons_dock", "heart", "Heart", Violet, OpenHeart),
            new MenuTile("icons_dock", "map", "Expeditions", Teal, OpenGuildBoard),
            new MenuTile("icons_dock", "battle", "Battle", Alert, () => { CloseAllPopups(); tower.LaunchBattleExpedition(); }),
            new MenuTile("icons_utility", "goals", "Goals", Teal, () => TogglePopup(popupTasks)),
            new MenuTile("icons_utility", "alerts", "Alerts", Teal, () => TogglePopup(popupAlerts)),
            new MenuTile("icons_utility", "drawer_arrow", "Floors", Teal, () => TogglePopup(popupFloors)),
            new MenuTile("icons_utility", "steward", "Steward", Teal,
                () => tower.Apply(tower.Rules.SetSteward(!tower.Rules.State.steward))),
            new MenuTile("icons_utility", "collect", "Recruit", Teal, () => tower.Apply(tower.Rules.RecruitVisitor())),
            new MenuTile("icons_utility", "rush", "Auto-assign", Teal, () => tower.Apply(tower.Rules.AutoAssignIdle())),
            new MenuTile("icons_utility", "pause", "Pause", Teal, () => { SetTime(0); CloseAllPopups(); })
        };
        var grid = Box("Menu grid", popupMenu.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-64, -10), new Vector2(4 * 128, 3 * 120), Color.clear);
        grid.raycastTarget = false;
        for (int i = 0; i < tiles.Length; i++)
        {
            var tile = tiles[i];
            var b = IconButtonAt(grid.transform, "Menu tile " + tile.caption, tile.sheet, tile.state, tile.caption,
                (i % 4) * 128 + 30, (i / 4) * 120 + 8, 68, tile.action, tile.tint);
            var label = LabelOf(b);
            label.fontSize = 13;
            label.rectTransform.sizeDelta = new Vector2(120, 18);
            label.rectTransform.anchoredPosition = new Vector2(-26, -72);
            if (tile.caption == "Steward") menuSteward = b;
        }
        // The close button sits exactly over the top-right Menu button, so the same spot opens and closes it.
        var close = ButtonAt(popupMenu.transform, "Menu close", "×", 0, 0, 40, 40, CloseAllPopups, Teal, 22)
            .GetComponent<RectTransform>();
        close.anchorMin = close.anchorMax = close.pivot = new Vector2(1, 1);
        close.anchoredPosition = new Vector2(-17, -8);
        popupMenu.gameObject.SetActive(false);
    }

    private void RefreshMainMenu(TowerState state)
    {
        var rules = tower.Rules;
        float hour = rules.Hour();
        menuDay.text = "Day " + state.day + "  ·  " + ((int)hour).ToString("00") + ":" +
            ((int)((hour % 1f) * 60f)).ToString("00");
        menuValues.text = RankTag(state.heartRank) + "   <color=#9aa3ad>HP</color> " + Mathf.CeilToInt(state.heartHp) +
            "\n" + rules.ThreatLabel() +
            "\n" + rules.BiologicalPopulation() + " / " + rules.PopulationCap() +
            "\n" + state.regionsConquered.Count + " / " + TowerRules.Regions.Length + " conquered" +
            "\n" + state.sigils + "\n" + state.celestium + "\n" + state.gold;
        menuRun.text = "Run " + (state.runs + 1) + "   ·   Legacy " + state.legacyRank + "   ·   Steward " + (state.steward ? "on" : "off") +
            "\nHold a dock or top button for shortcuts.";
        if (menuSteward != null) menuSteward.GetComponent<Image>().color = state.steward ? Gold : Teal;
    }
}
