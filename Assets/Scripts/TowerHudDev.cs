using System;
using AdamsHaven.Tower;
using UnityEngine;
using UnityEngine.UI;

// DEV panel (MENU > DEV): cheats for testing progression. Every button runs a TowerRules.Dev* action through
// tower.Apply, so results land in the log and the toast like any other action, and the Tower saves.
public sealed partial class TowerHud
{
    private Image devOverlay;
    private Text devStatus;
    private Text devInstantLabel;
    private const string InstantKey = "AdamsHaven.Dev.InstantBuild";

    private void BuildDev()
    {
        TowerRules.InstantConstruction = PlayerPrefs.GetInt(InstantKey, 0) == 1;
        devOverlay = Rect("Dev overlay", safeRoot, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, new Color(0.01f, 0.02f, 0.04f, 0.92f));
        var card = Rect("Dev card", devOverlay.transform,
            new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f), Vector2.zero, Vector2.zero, Panel);
        TowerUiSkin.ApplyPanel(card, Glass, true);
        card.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;   // swallow taps
        var c = card.transform;
        TextAt(c, "Dev title", "DEV MODE", 24, 14, 300, 40, 26, Gold);
        devStatus = TextAt(c, "Dev status", "", 220, 14, 760, 40, 13, TowerUiSkin.TextDim);
        var close = ButtonAt(c, "Close dev", "CLOSE", 0, 14, 120, 40, () => { devOverlay.gameObject.SetActive(false); Refresh(); },
            Alert, 15).GetComponent<RectTransform>();
        close.anchorMin = close.anchorMax = new Vector2(1, 1);
        close.offsetMin = new Vector2(-140, -54); close.offsetMax = new Vector2(-20, -14);

        Func<TowerRules> r = () => tower.Rules;
        Row(c, 0, "RESOURCES", new (string, Action)[] {
            ("+10K GOLD", () => Do(r().DevGrant("gold", 10000))),
            ("+1M GOLD", () => Do(r().DevGrant("gold", 1000000))),
            ("+1K CELESTIUM", () => Do(r().DevGrant("celestium", 1000))),
            ("+100 SIGILS", () => Do(r().DevGrant("sigils", 100))),
            ("+1K MATS", () => Do(r().DevGrant("materials", 1000))),
            ("+1K FOOD", () => Do(r().DevGrant("supplies", 1000))),
            ("+20 TONICS", () => Do(r().DevGrant("tonics", 20))),
            ("MAX ALL", MaxEverything) });
        var heroes = new (string, Action)[TowerRules.SummonHeroIds.Length + 1];
        for (int i = 0; i < TowerRules.SummonHeroIds.Length; i++)
        {
            string id = TowerRules.SummonHeroIds[i];
            heroes[i] = ("+ " + TowerRules.SummonHeroNames[i].ToUpperInvariant(), () => Do(r().DevAddHero(id)));
        }
        heroes[heroes.Length - 1] = ("+ NEXT HERO", () => Do(r().DevAddRosterHero()));
        Row(c, 1, "HEROES", heroes);
        Row(c, 2, "RESIDENTS", new (string, Action)[] {
            ("+ RESIDENT", () => Do(r().DevAddResident())),
            ("+ NORMAL BODY", () => Do(r().DevAddResident("normal"))),
            ("+ SHORT BODY", () => Do(r().DevAddResident("short"))),
            ("+ TALL BODY", () => Do(r().DevAddResident("tall"))),
            ("+ MUSCLE", () => Do(r().DevAddResident("muscle"))),
            ("+ HOURGLASS", () => Do(r().DevAddResident("hourglass"))),
            ("ALL LEVEL +5", () => Do(r().DevLevelUpAll(5))),
            ("HEAL ALL", () => Do(r().DevHealAll())) });
        Row(c, 3, "BUILDINGS", new (string, Action)[] {
            ("FINISH BUILDS", () => Do(r().DevFinishConstruction())),
            ("", ToggleInstant),
            ("ROOMS READY", () => Do(r().DevReadyRooms())),
            ("ROOMS RANK +1", () => Do(r().DevRoomsRankUp())),
            ("HEART RANK +1", () => Do(r().DevHeartRankUp())),
            ("BLUEPRINTS", () => Do(r().DevUnlockBlueprints())),
            ("END RESEARCH", () => Do(r().DevFinishResearch())),
            ("ALL RESEARCH", () => Do(r().DevResearchAll())) });
        Row(c, 4, "TIME & TUTORIAL", new (string, Action)[] {
            ("+1 HOUR", () => Do(r().DevSkipTime(TowerRules.DaySeconds / 24f))),
            ("+1 DAY", () => Do(r().DevSkipTime(TowerRules.DaySeconds))),
            ("SKIP INTRO", () => Do(r().DevSkipIntro())),
            ("LESSON -1", () => Do(r().DevTutorialStep(-1))),
            ("LESSON +1", () => Do(r().DevTutorialStep(1))),
            ("END LESSONS", () => Do(r().DevTutorialStep(7))),
            ("NEW GAME", () => { tower.NewGame(); devOverlay.gameObject.SetActive(false); Refresh(); }) });
        if (TowerModes.IsLair)
            Row(c, 5, "DUNGEON", new (string, Action)[] {
                ("+ PARTY NOW", () => Do(r().DevLairParty())),
                ("+ ELITE NOW", () => Do(r().DevLairElite())),
                ("+25 NOTORIETY", () => Do(r().DevLairNotoriety(25))),
                ("+5 MONSTERS", () => Do(r().DevLairMonsters(5))),
                ("DIG FLOOR", () => Do(r().LairDigFloor())) });
        devOverlay.gameObject.SetActive(false);
    }

    // One labelled row of eight buttons; a blank caption marks the instant-build toggle.
    private void Row(Transform card, int row, string title, (string caption, Action action)[] buttons)
    {
        float top = 70 + row * (TowerModes.IsLair ? 92 : 108);   // Dungeon Mode adds a sixth row
        TextAt(card, "Dev row " + title, title, 24, top, 400, 22, 14, Gold);
        for (int i = 0; i < buttons.Length; i++)
        {
            var b = buttons[i];
            var button = ButtonAt(card, "Dev " + title + " " + i, b.caption, 24 + i * 138, top + 28, 130, 54, b.action,
                b.caption.StartsWith("+", StringComparison.Ordinal) ? Teal : b.caption == "NEW GAME" ? Alert : Gold, 12);
            if (b.caption.Length == 0) devInstantLabel = LabelOf(button);
        }
    }

    private void Do(string error)
    {
        tower.Apply(error);
        RefreshDev();
    }

    private void ToggleInstant()
    {
        TowerRules.InstantConstruction = !TowerRules.InstantConstruction;
        PlayerPrefs.SetInt(InstantKey, TowerRules.InstantConstruction ? 1 : 0);
        PlayerPrefs.Save();
        if (TowerRules.InstantConstruction) tower.Rules.DevFinishConstruction();
        tower.Apply(null);
        RefreshDev();
    }

    private void MaxEverything()
    {
        var r = tower.Rules;
        if (r.State.introPhase != "complete") r.DevSkipIntro();
        r.DevGrant("gold", 10000000); r.DevGrant("celestium", 100000); r.DevGrant("sigils", 5000);
        r.DevGrant("materials", 100000); r.DevGrant("supplies", 100000); r.DevGrant("tonics", 200);
        r.DevUnlockBlueprints(); r.DevResearchAll();
        while (r.State.heartRank < TowerTiers.MaxRank && r.DevHeartRankUp() == null) { }
        r.DevFinishConstruction(); r.DevHealAll(); r.DevTutorialStep(7);
        Do(null);
    }

    // NEW GAME wipes save slot 0, so the menu button asks for a second tap within three seconds.
    private Button menuNewGame;
    private float newGameArmedUntil;

    private void ConfirmNewGame()
    {
        if (Time.unscaledTime > newGameArmedUntil)
        {
            newGameArmedUntil = Time.unscaledTime + 3f;
            LabelOf(menuNewGame).text = "TAP TO CONFIRM";
            return;
        }
        newGameArmedUntil = 0;
        LabelOf(menuNewGame).text = "NEW GAME";
        tower.NewGame();
        CloseAllPopups();
    }

    private void TickNewGameConfirm()
    {
        if (menuNewGame != null && newGameArmedUntil > 0 && Time.unscaledTime > newGameArmedUntil)
        { newGameArmedUntil = 0; LabelOf(menuNewGame).text = "NEW GAME"; }
    }

    public void OpenDev()
    {
        HidePopups();
        devOverlay.gameObject.SetActive(true);
        RefreshDev();
    }

    private void RefreshDev()
    {
        if (devOverlay == null || !devOverlay.gameObject.activeSelf || tower.Rules == null) return;
        var s = tower.Rules.State;
        devStatus.text = "Slot " + tower.CurrentSlot + "  ·  " + s.introPhase + (s.introPhase == "complete" ? " · lesson " + s.tutorialStep : "") +
            "  ·  Heart " + TowerTiers.Tier(s.heartRank) + "  ·  " + s.residents.Count + " people  ·  " + s.works.Count + " building\n" +
            (tower.CurrentMessage ?? "");
        if (devInstantLabel != null)
            devInstantLabel.text = "INSTANT: " + (TowerRules.InstantConstruction ? "ON" : "OFF");
    }
}
