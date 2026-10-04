using System.Collections.Generic;
using AdamsHaven.Tower;
using UnityEngine;
using UnityEngine.UI;

// Dungeon Mode HUD (TT 10.4.0, GDD 20.7): the founding choice, the DUNGEON popup (raid reports, monsters, the dungeon
// itself), the Monster banner on the Heart's SUMMON tab and the mode switch on the menu. Built only in Dungeon Mode,
// except the switch button, which the Tower shows too.
public sealed partial class TowerHud
{
    private Image popupLair;
    private Button lairTabRaids, lairTabMonsters, lairTabDungeon, lairDig, lairLiving, lairPrevReport, lairNextReport;
    private Button lairPrevPage, lairNextPage;
    private Text lairText, lairDetail, lairPageText;
    private string lairTab = "raids";
    private int lairReport, lairPage, lairReleaseArmed;
    private float lairReleaseUntil;
    private const int MonsterRows = 6;
    private readonly Text[] monsterTexts = new Text[MonsterRows];
    private readonly Button[] monsterAssign = new Button[MonsterRows], monsterGuard = new Button[MonsterRows],
        monsterRelease = new Button[MonsterRows];
    private Button foundSummit, foundDepths;
    private bool lairBanner;               // the Heart's SUMMON tab shows the Monster banner
    private Button monsterBannerTab;
    private Button menuSwitch;
    private float switchArmedUntil;

    private static bool LairHud { get { return TowerModes.IsLair; } }

    // ---------------------------------------------------------------- founding

    private void BuildLairFounding(Transform panel)
    {
        if (!LairHud) return;
        foundSummit = ButtonAt(panel, "Found summit", "HEART AT THE SUMMIT", 22, 44, 280, 44, () => FoundLair(true), Teal, 15);
        foundDepths = ButtonAt(panel, "Found depths", "HEART IN THE DEPTHS", 310, 44, 280, 44, () => FoundLair(false), Violet, 15);
    }

    private void FoundLair(bool summit)
    {
        string error = tower.Rules.LairFound(summit);
        tower.Apply(error);
        if (error == null) tower.FocusOnFloor(tower.Rules.HeartFloor);
    }

    // Returns true when it handled the panel (the founding step of a dungeon).
    private bool RefreshLairFounding(string phase)
    {
        if (foundSummit == null) return false;
        bool site = phase == "lair_site";
        foundSummit.gameObject.SetActive(site);
        foundDepths.gameObject.SetActive(site);
        if (!site) return false;
        tutorialTitle.fontSize = 20;
        tutorialTitle.rectTransform.sizeDelta = new Vector2(592, 38);
        tutorialTitle.text = "Where does the Heart sleep?";
        tutorialAction.gameObject.SetActive(false);
        foreach (var button in heroButtons) button.gameObject.SetActive(false);
        return true;
    }

    // ---------------------------------------------------------------- menu switch (both modes)

    private void BuildModeSwitch(Transform card)
    {
        menuSwitch = ButtonAt(card, "Menu mode switch", TowerModes.IsLair ? "TOWER MODE" : "DUNGEON MODE", 24, 412, 292, 40,
            ConfirmModeSwitch, Violet, 14);
    }

    private void ConfirmModeSwitch()
    {
        string label = TowerModes.IsLair ? "TOWER MODE" : "DUNGEON MODE";
        if (Time.unscaledTime > switchArmedUntil)
        {
            string blocked = TowerModes.CanSwitch(tower);
            if (blocked != null) { tower.Apply(blocked); return; }
            switchArmedUntil = Time.unscaledTime + 3f;
            LabelOf(menuSwitch).text = "TAP AGAIN: SWITCH TO " + label;
            return;
        }
        switchArmedUntil = 0;
        LabelOf(menuSwitch).text = label;
        string error = TowerModes.SwitchTo(TowerModes.Other, tower);
        if (error != null) tower.Apply(error);
    }

    private void TickModeSwitch()
    {
        if (menuSwitch != null && switchArmedUntil > 0 && Time.unscaledTime > switchArmedUntil)
        { switchArmedUntil = 0; LabelOf(menuSwitch).text = TowerModes.IsLair ? "TOWER MODE" : "DUNGEON MODE"; }
    }

    // ---------------------------------------------------------------- DUNGEON popup

    private void BuildLairPopup()
    {
        if (!LairHud) return;
        popupLair = Box("Dungeon popup", safeRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10),
            new Vector2(720, 450), Glass);
        TowerUiSkin.ApplyPanel(popupLair, Glass, true);
        TowerUiFlow.Add(popupLair, new Vector2(0, -18));
        popupLair.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;   // swallow taps
        var t = popupLair.transform;
        TextAt(t, "Dungeon title", "DUNGEON", 16, 10, 130, 30, 19, Gold);
        lairTabRaids = ButtonAt(t, "Tab raids", "RAIDS", 150, 10, 150, 34, () => { lairTab = "raids"; Refresh(); }, Teal, 14);
        lairTabMonsters = ButtonAt(t, "Tab monsters", "MONSTERS", 306, 10, 150, 34, () => { lairTab = "monsters"; Refresh(); }, Teal, 14);
        lairTabDungeon = ButtonAt(t, "Tab dungeon", "DUNGEON", 462, 10, 150, 34, () => { lairTab = "dungeon"; Refresh(); }, Teal, 14);
        CloseButton(t, 680, 12, CloseAllPopups);
        Divider(t, 12, 50, 696);
        lairText = TextAt(t, "Dungeon text", "", 16, 62, 688, 120, 14, Cream, TextAnchor.UpperLeft);
        lairDetail = TextAt(t, "Dungeon detail", "", 16, 188, 688, 200, 13, Cream, TextAnchor.UpperLeft);
        lairPrevReport = ButtonAt(t, "Report previous", "◀ NEWER", 16, 398, 150, 38, () => { lairReport--; Refresh(); }, Teal, 13);
        lairNextReport = ButtonAt(t, "Report next", "OLDER ▶", 554, 398, 150, 38, () => { lairReport++; Refresh(); }, Teal, 13);
        lairDig = ButtonAt(t, "Dig floor", "DIG DUNGEON FLOOR", 16, 398, 336, 38,
            () => tower.Apply(tower.Rules.LairDigFloor()), Alert, 14);
        lairLiving = ButtonAt(t, "Living floor", "ADD LIVING FLOOR", 368, 398, 336, 38,
            () => tower.Apply(tower.Rules.LairAddLivingFloor()), Teal, 14);
        for (int i = 0; i < MonsterRows; i++)
        {
            int row = i;
            float y = 92 + i * 48;
            monsterTexts[i] = TextAt(t, "Monster " + i, "", 16, y, 420, 44, 13, Cream);
            monsterAssign[i] = ButtonAt(t, "Monster assign " + i, "TO LAIR", 442, y + 4, 86, 36, () => MonsterAction(row, "assign"), Teal, 12);
            monsterGuard[i] = ButtonAt(t, "Monster guard " + i, "GUARD", 532, y + 4, 80, 36, () => MonsterAction(row, "guard"), Violet, 12);
            monsterRelease[i] = ButtonAt(t, "Monster release " + i, "RELEASE", 616, y + 4, 88, 36, () => MonsterAction(row, "release"), Alert, 12);
        }
        lairPrevPage = ButtonAt(t, "Monsters previous", "‹", 16, 398, 56, 38, () => { lairPage = Mathf.Max(0, lairPage - 1); Refresh(); }, Teal, 20);
        lairPageText = TextAt(t, "Monsters page", "", 80, 398, 560, 38, 13, Cream, TextAnchor.MiddleCenter);
        lairNextPage = ButtonAt(t, "Monsters next", "›", 648, 398, 56, 38, () => { lairPage++; Refresh(); }, Teal, 20);
        popupLair.gameObject.SetActive(false);
    }

    private void OpenLair()
    {
        if (popupLair == null) return;
        if (tower.Rules.State.introPhase != "complete") { tower.Apply("Found the dungeon first."); return; }
        TogglePopup(popupLair);
    }

    private void MonsterAction(int row, string action)
    {
        var list = tower.Rules.State.lair.monsters;
        int index = lairPage * MonsterRows + row;
        if (index >= list.Count) return;
        var monster = list[index];
        var rules = tower.Rules;
        if (action == "assign")
        {
            var room = tower.SelectedRoom;
            if (room == null || room.type != "monster_lair")
                room = rules.State.rooms.Find(r => r.type == "monster_lair" && rules.MonstersIn(r.uid).Count < rules.LairCapacity(r));
            tower.Apply(room == null ? "Every Monster Lair is full. Build or upgrade one." : rules.AssignMonster(monster.id, room.uid));
        }
        else if (action == "guard") tower.Apply(rules.UnassignMonster(monster.id));
        else
        {
            if (lairReleaseArmed != monster.id || Time.unscaledTime > lairReleaseUntil)
            {
                lairReleaseArmed = monster.id;
                lairReleaseUntil = Time.unscaledTime + 3f;
                tower.Apply("Tap RELEASE again to let " + monster.name + " go for " + rules.ReleaseRefund(monster) + " Essence.");
                return;
            }
            lairReleaseArmed = 0;
            tower.Apply(rules.ReleaseMonster(monster.id));
        }
    }

    private void RefreshLair(TowerState state)
    {
        var rules = tower.Rules;
        var lair = state.lair;
        lairTabRaids.GetComponent<Image>().color = lairTab == "raids" ? Gold : Teal;
        lairTabMonsters.GetComponent<Image>().color = lairTab == "monsters" ? Gold : Teal;
        lairTabDungeon.GetComponent<Image>().color = lairTab == "dungeon" ? Gold : Teal;
        bool raids = lairTab == "raids", monsters = lairTab == "monsters", dungeon = lairTab == "dungeon";
        lairPrevReport.gameObject.SetActive(raids); lairNextReport.gameObject.SetActive(raids);
        lairDig.gameObject.SetActive(dungeon); lairLiving.gameObject.SetActive(dungeon);
        lairPrevPage.gameObject.SetActive(monsters); lairNextPage.gameObject.SetActive(monsters);
        lairPageText.gameObject.SetActive(monsters);
        lairDetail.gameObject.SetActive(!monsters);
        lairText.rectTransform.sizeDelta = new Vector2(688, monsters ? 28 : 120);
        for (int i = 0; i < MonsterRows; i++)
        {
            monsterTexts[i].gameObject.SetActive(false);
            monsterAssign[i].gameObject.SetActive(false);
            monsterGuard[i].gameObject.SetActive(false);
            monsterRelease[i].gameObject.SetActive(false);
        }
        if (raids) RefreshLairRaids(rules, lair);
        else if (monsters) RefreshLairMonsters(rules, lair);
        else RefreshLairDungeon(rules, state);
    }

    private void RefreshLairRaids(TowerRules rules, TowerLairState lair)
    {
        var lines = new List<string>();
        lines.Add("NOTORIETY " + Mathf.RoundToInt(lair.notoriety) + "   FAME " + Mathf.RoundToInt(lair.fame) +
            "   next party in " + TowerRules.Clock(Mathf.Max(0, lair.nextPartySeconds)) + "   kills " + lair.kills +
            "   escapes " + lair.escapes + "   breaches " + lair.breaches);
        if (lair.parties.Count == 0) lines.Add("No adventurers inside.");
        foreach (var party in lair.parties)
        {
            var hp = new List<string>();
            foreach (var m in party.members)
                hp.Add(m.Alive ? m.name + " " + Mathf.CeilToInt(m.hp) + "/" + Mathf.CeilToInt(m.maxHp) :
                    "<color=#8a8a8a>" + m.name + " " + m.status + "</color>");
            lines.Add((party.kind == "elite" ? "<color=#ff8a70>ELITE</color> " : "") + party.name + "  ·  " +
                rules.FloorLabel(party.floor) + "\n   <size=12>" + string.Join(", ", hp.ToArray()) + "</size>");
        }
        lairText.text = string.Join("\n", lines.ToArray());
        lairReport = Mathf.Clamp(lairReport, 0, Mathf.Max(0, lair.reports.Count - 1));
        lairPrevReport.interactable = lairReport > 0;
        lairNextReport.interactable = lairReport < lair.reports.Count - 1;
        if (lair.reports.Count == 0) { lairDetail.text = "RAID REPORTS\nNo raid has ended yet."; return; }
        var r = lair.reports[lairReport];
        string outcome = r.outcome == "wiped" ? "<color=#8fe39a>WIPED OUT</color>" :
            r.outcome == "breached" ? "<color=#ff7060>BREACHED THE HEART</color>" : "<color=#ffd45c>FLED</color>";
        var body = new List<string>(r.lines);
        if (body.Count > 9) body = body.GetRange(body.Count - 9, 9);
        lairDetail.text = "REPORT " + (lairReport + 1) + "/" + lair.reports.Count + "   " + r.partyName + "  ·  day " + r.day + "  ·  " +
            outcome + "\n<size=12>" + r.kills + " killed, " + r.captured + " captured, +" + r.goldGained + " gold, -" + r.goldLost +
            " gold, Heart -" + Mathf.RoundToInt(r.heartDamage) + ", notoriety " + Signed(r.notorietyDelta) + ", fame " +
            Signed(r.fameDelta) + "</size>\n<size=12>" + string.Join("\n", body.ToArray()) + "</size>";
    }

    private static string Signed(float value) { int v = Mathf.RoundToInt(value); return (v >= 0 ? "+" : "") + v; }

    private void RefreshLairMonsters(TowerRules rules, TowerLairState lair)
    {
        var list = lair.monsters;
        int pages = Mathf.Max(1, Mathf.CeilToInt(list.Count / (float)MonsterRows));
        lairPage = Mathf.Clamp(lairPage, 0, pages - 1);
        var selected = tower.SelectedRoom;
        lairText.text = "MONSTERS " + list.Count + "/" + rules.MonsterCap() + "   " +
            (selected != null && selected.type == "monster_lair" ? "TO LAIR sends them to the selected lair (" +
                rules.MonstersIn(selected.uid).Count + "/" + rules.LairCapacity(selected) + ")" :
                "TO LAIR fills the first lair with room") + "   ·   summon more at the Heart";
        lairPageText.text = "PAGE " + (lairPage + 1) + " / " + pages + (list.Count == 0 ? "   ·   no monsters yet" : "");
        for (int i = 0; i < MonsterRows; i++)
        {
            int index = lairPage * MonsterRows + i;
            if (index >= list.Count) continue;
            var m = list[index];
            var room = rules.Room(m.lairRoom);
            string where = room != null && room.type == "monster_lair" ? "lair " + rules.FloorLabel(room.floor) : "guards the Heart";
            string health = m.woundSeconds > 0 ? "<color=#ff8a70>wounded " + TowerRules.Clock(m.woundSeconds) + "</color>" :
                "hp " + Mathf.RoundToInt(m.hp * 100) + "%";
            monsterTexts[i].text = RankTag(m.rank) + " " + m.name + "  <color=#b7c7d6>" + (m.element.Length > 0 ? m.element : "Neutral") +
                " · lv " + m.level + "</color>\n<size=12>power " + Mathf.RoundToInt(TowerRules.MonsterPower(m)) + "  ·  " + health +
                "  ·  " + where + "  ·  kills " + m.kills + "</size>";
            monsterTexts[i].gameObject.SetActive(true);
            monsterAssign[i].gameObject.SetActive(true);
            monsterGuard[i].gameObject.SetActive(true);
            monsterRelease[i].gameObject.SetActive(true);
            monsterGuard[i].interactable = m.lairRoom != 0;
        }
    }

    private void RefreshLairDungeon(TowerRules rules, TowerState state)
    {
        var lair = state.lair;
        lairText.text = "NOTORIETY " + Mathf.RoundToInt(lair.notoriety) + " / 100  (" + rules.ThreatLabel() + "): " +
            TowerRules.NotorietyHint(lair.notoriety) + ".\n<size=12>Kills raise it; parties that flee or breach lower it; it fades 1 a minute. " +
            "At " + Mathf.RoundToInt(TowerRules.SiegeThreat) + " the guilds send an elite team.</size>" +
            "\nFAME " + Mathf.RoundToInt(lair.fame) + " / 100\n<size=12>Survivors spread it. More fame: bigger, stronger, more frequent parties " +
            "(next in " + TowerRules.Clock(Mathf.Max(0, lair.nextPartySeconds)) + ", every ~" + TowerRules.Clock(rules.PartyInterval()) +
            "). Wipe parties and it falls.</size>";
        var floors = new List<string>();
        var numbers = new List<int>();
        foreach (var f in state.floors) numbers.Add(f.number);
        numbers.Sort();
        if (rules.LairDir > 0) numbers.Reverse();
        foreach (int n in numbers)
        {
            int traps = state.rooms.FindAll(r => r.floor == n && TowerRules.IsLairRoom(r)).Count;
            floors.Add(rules.FloorLabel(n) + " <color=#b7c7d6>(" + rules.FloorKind(n) + (traps > 0 ? ", " + traps + " dungeon rooms" : "") + ")</color>");
        }
        lairDetail.text = "FLOORS  " + string.Join("  ·  ", floors.ToArray()) +
            "\n\n<size=12>Dungeon floors hold traps, snares, lairs and vaults; the living floors hold the residents' rooms. Parties " +
            "zig-zag across every dungeon floor: stairs alternate ends.</size>";
        LabelOf(lairDig).text = "DIG DUNGEON FLOOR  " + rules.LairDigCost() + "C  (" + rules.DungeonFloorCount + "/" + rules.LairDigCap() + ")";
        LabelOf(lairLiving).text = "ADD LIVING FLOOR  " + rules.LairLivingCost() + "C  (" + lair.livingFloors + "/" + rules.LairLivingCap() + ")";
    }

    // ---------------------------------------------------------------- Monster banner (Heart SUMMON tab)

    private void BuildMonsterBannerTab(Transform summonTab)
    {
        if (!LairHud) return;
        for (int i = 0; i < bannerTabs.Length; i++)
        {
            var rect = bannerTabs[i].GetComponent<RectTransform>();
            rect.offsetMin = new Vector2(16 + i * 138, rect.offsetMin.y);
            rect.offsetMax = new Vector2(16 + i * 138 + 132, rect.offsetMax.y);
            LabelOf(bannerTabs[i]).fontSize = 12;
            LabelOf(bannerTabs[i]).rectTransform.sizeDelta = new Vector2(124, LabelOf(bannerTabs[i]).rectTransform.sizeDelta.y);
            bannerTabs[i].onClick.AddListener(() => lairBanner = false);
        }
        monsterBannerTab = ButtonAt(summonTab, "Banner monster", "MONSTER", 16 + 4 * 138, 4, 132, 32,
            () => { lairBanner = true; Refresh(); }, Alert, 12);
    }

    // Runs after the normal summon refresh and replaces what it showed.
    private void RefreshMonsterBanner(TowerState state)
    {
        if (monsterBannerTab == null) return;
        monsterBannerTab.GetComponent<Image>().color = lairBanner ? Gold : Alert;
        if (!lairBanner) return;
        var rules = tower.Rules;
        foreach (var tab in bannerTabs) tab.GetComponent<Image>().color = Teal;
        pickPrev.gameObject.SetActive(false);
        pickNext.gameObject.SetActive(false);
        bannerInfo.rectTransform.anchoredPosition = new Vector2(16, -40);
        bannerInfo.rectTransform.sizeDelta = new Vector2(688, 28);
        bannerInfo.alignment = TextAnchor.MiddleLeft;
        bannerInfo.text = "Bestiary monsters for your lairs.  <color=#b7c7d6>" + state.lair.monsters.Count + "/" + rules.MonsterCap() +
            " in the dungeon</color>";
        int toGuarantee = Mathf.Max(1, LairBalance.MonsterPityAt - state.lair.monsterPity);
        sigilText.text = "SIGILS " + state.sigils + "     A OR BETTER IN " + toGuarantee + " PULL" + (toGuarantee == 1 ? "" : "S");
        pityFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(state.lair.monsterPity / (float)LairBalance.MonsterPityAt), 1);
        pityFill.color = new Color(1f, 0.45f, 0.4f);
        string rates = "RATES  ";
        var bannerRates = TowerRules.BannerRates(TowerRules.Banner("resident"));
        for (int i = 0; i < bannerRates.Length; i++) rates += RankTag(i + 1) + " " + bannerRates[i].ToString("0.#") + "%   ";
        ratesText.text = rates + "\nA random bestiary family that lives at the rolled rank. Duplicates add 2 levels. Every 10-pull holds a " +
            "B or better. With the dungeon full, new monsters are released for Essence.";
        LabelOf(summonOne).text = "SUMMON ×1   " + LairBalance.MonsterPullCost + " SIGILS";
        LabelOf(summonTen).text = "SUMMON ×10   " + LairBalance.MonsterTenCost + " SIGILS";
        summonOne.interactable = state.sigils >= LairBalance.MonsterPullCost;
        summonTen.interactable = state.sigils >= LairBalance.MonsterTenCost;
        summonOne.GetComponent<Image>().color = Teal;
        var pulls = rules.LastMonsters;
        if (pulls == null || pulls.Count == 0) { summonResults.text = "The Heart growls, waiting for Sigils."; return; }
        var lines = new List<string>();
        foreach (var pull in pulls)
            lines.Add(RankTag(pull.rank) + "   " + pull.name + "   <color=#b7c7d6>" + (pull.element ?? "") + "  •  " +
                (pull.fused ? "duplicate: now level " + pull.level : pull.released ? "no room: released for Essence" : "joins the dungeon") + "</color>");
        summonResults.text = string.Join(pulls.Count > 5 ? "\n" : "\n\n", lines.ToArray());
    }

    // The top bar's short line: notoriety, fame and where the parties are.
    private string LairTopShort(TowerState state)
    {
        var lair = state.lair;
        string where = lair.parties.Count == 0 ? "" : "  ·  " + lair.parties.Count + " IN " + tower.Rules.FloorLabel(lair.parties[0].floor);
        return tower.Rules.ThreatLabel().ToUpperInvariant() + "  ·  FAME " + Mathf.RoundToInt(lair.fame) + where;
    }
}
