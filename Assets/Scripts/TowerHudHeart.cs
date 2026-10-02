using System.Collections.Generic;
using AdamsHaven.Tower;
using UnityEngine;
using UnityEngine.UI;

// TOWER_MODE_GDD 12.3-12.5 and 8: the five-slot dock's Heart hub (Summon, Upgrade, Status), the
// top-right alert tray, and the shared rank colours.
public sealed partial class TowerHud
{
    private Image popupHeart, popupAlerts;
    private Image heartSummonTab, heartUpgradeTab, heartStatusTab, pityFill;
    private Button tabSummon, tabUpgrade, tabStatus, summonOne, summonTen, heartUpgradeButton;
    private Text sigilText, ratesText, summonResults, heartRankText, heartUpgradeText, heartStatusText;
    private string heartTab = "summon";
    private readonly Button[] alertJumps = new Button[4];
    private readonly Text[] alertTexts = new Text[4];
    private Text alertsEmpty;

    // F grey up through SSR violet-gold; rank is always written as text too, never colour alone.
    public static string RankHex(int rank)
    {
        string[] hex = { "#a9a9a9", "#8fd18f", "#6fd6c8", "#79a8ff", "#b48cff", "#ffad5c", "#ffd45c", "#ff8fd0", "#e9b8ff" };
        return hex[Mathf.Clamp(rank, 1, 9) - 1];
    }

    private static string RankTag(int rank)
    { return "<color=" + RankHex(rank) + ">" + TowerTiers.Tier(rank) + "</color>"; }

    private bool HeartReady()
    {
        var rules = tower == null ? null : tower.Rules;
        if (rules == null || rules.State.introPhase != "complete") return false;
        return rules.FreeSummonReady || rules.State.sigils >= TowerRules.PullCost || ResearchAvailable() ||
            rules.State.heartRank < TowerTiers.MaxRank && rules.State.celestium >= rules.HeartUpgradeCelestium() &&
            rules.State.gold >= rules.HeartUpgradeGold();
    }

    // ---------------------------------------------------------------- Heart hub

    private void BuildHeartPopup()
    {
        popupHeart = Box("Heart hub", safeRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10),
            new Vector2(720, 450), Glass);
        TowerUiSkin.ApplyPanel(popupHeart, Glass, true);
        var t = popupHeart.transform;
        TextAt(t, "Heart title", "HEART", 16, 10, 120, 30, 19, Gold);
        tabSummon = ButtonAt(t, "Tab summon", "SUMMON", 150, 10, 120, 34, () => { heartTab = "summon"; Refresh(); }, Teal, 14);
        tabResearch = ButtonAt(t, "Tab research", "RESEARCH", 276, 10, 120, 34, () => { heartTab = "research"; Refresh(); }, Teal, 14);
        tabUpgrade = ButtonAt(t, "Tab upgrade", "UPGRADE", 402, 10, 120, 34, () => { heartTab = "upgrade"; Refresh(); }, Teal, 14);
        tabStatus = ButtonAt(t, "Tab status", "STATUS", 528, 10, 120, 34, () => { heartTab = "status"; Refresh(); }, Teal, 14);
        CloseButton(t, 680, 12, CloseAllPopups);
        Divider(t, 12, 50, 696);

        heartSummonTab = Rect("Summon tab", t, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -58), Color.clear);
        heartSummonTab.raycastTarget = false;
        var s = heartSummonTab.transform;
        sigilText = TextAt(s, "Sigils", "", 16, 6, 688, 24, 16, Cream);
        var pity = Rect("Pity bar", s, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -44), new Vector2(704, -34),
            new Color(0.13f, 0.16f, 0.2f, 1));
        pity.raycastTarget = false;
        pityFill = Rect("Pity fill", pity.transform, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero,
            new Color(0.82f, 0.62f, 1f));
        pityFill.raycastTarget = false;
        ratesText = TextAt(s, "Rates", "", 16, 50, 688, 44, 12, Cream);
        summonOne = ButtonAt(s, "Summon one", "SUMMON ×1", 16, 100, 336, 56, () => DoSummon(1), Teal, 18);
        summonTen = ButtonAt(s, "Summon ten", "SUMMON ×10", 368, 100, 336, 56, () => DoSummon(10), Teal, 18);
        summonResults = TextAt(s, "Results", "", 16, 166, 688, 214, 15, Cream, TextAnchor.UpperLeft);

        heartUpgradeTab = Rect("Upgrade tab", t, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -58), Color.clear);
        heartUpgradeTab.raycastTarget = false;
        heartRankText = TextAt(heartUpgradeTab.transform, "Heart rank", "", 16, 10, 688, 44, 28, Gold, TextAnchor.MiddleCenter);
        heartUpgradeText = TextAt(heartUpgradeTab.transform, "Upgrade info", "", 16, 64, 688, 170, 15, Cream, TextAnchor.UpperLeft);
        heartUpgradeButton = ButtonAt(heartUpgradeTab.transform, "Raise Heart", "RAISE THE HEART", 192, 260, 336, 60,
            () => tower.Apply(tower.Rules.UpgradeHeart()), Teal, 18);

        heartStatusTab = Rect("Status tab", t, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -58), Color.clear);
        heartStatusTab.raycastTarget = false;
        heartStatusText = TextAt(heartStatusTab.transform, "Status", "", 16, 10, 688, 360, 15, Cream, TextAnchor.UpperLeft);
        BuildResearchTab(t);
        popupHeart.gameObject.SetActive(false);
    }

    private void OpenHeart()
    {
        if (tower.Rules.State.introPhase != "complete") { tower.Apply("Found the Tower first."); return; }
        heartTab = tower.Rules.FreeSummonReady || tower.Rules.State.sigils >= TowerRules.PullCost ? "summon" : heartTab;
        TogglePopup(popupHeart);
    }

    private void DoSummon(int count)
    {
        string error = tower.Rules.Summon(count);
        tower.Apply(error);
        Refresh();
    }

    private void RefreshHeart(TowerState state)
    {
        var rules = tower.Rules;
        heartSummonTab.gameObject.SetActive(heartTab == "summon");
        heartUpgradeTab.gameObject.SetActive(heartTab == "upgrade");
        heartStatusTab.gameObject.SetActive(heartTab == "status");
        heartResearchTab.gameObject.SetActive(heartTab == "research");
        tabResearch.GetComponent<Image>().color = heartTab == "research" ? Gold : Teal;
        LabelOf(tabResearch).text = rules.Researching ? "RESEARCH " + Mathf.FloorToInt(rules.ResearchProgress() * 100) + "%" : "RESEARCH";
        if (heartTab == "research") RefreshResearch(state);
        tabSummon.GetComponent<Image>().color = heartTab == "summon" ? Gold : Teal;
        tabUpgrade.GetComponent<Image>().color = heartTab == "upgrade" ? Gold : Teal;
        tabStatus.GetComponent<Image>().color = heartTab == "status" ? Gold : Teal;

        // Summon
        sigilText.text = "SIGILS " + state.sigils + "     SSR PITY " + state.summonPity + " / " + TowerRules.HardPity +
            "     NEXT SSR CHANCE " + rules.SsrChance().ToString("0.#") + "%" +
            (state.heartWaiting.Count > 0 ? "     WAITING IN HEART " + state.heartWaiting.Count : "");
        pityFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(state.summonPity / (float)TowerRules.HardPity), 1);
        string rates = "RATES  ";
        for (int i = 0; i < TowerRules.SummonRates.Length; i++)
            rates += RankTag(i + 1) + " " + TowerRules.SummonRates[i].ToString("0.#") + "%   ";
        ratesText.text = rates + "\nSoft pity from pull " + rules.SoftPityStart() + ", SSR guaranteed by pull " +
            TowerRules.HardPity + ". Every 10-pull holds a B or better. " + Mathf.RoundToInt(TowerRules.HeroShare * 100) +
            "% heroes, the rest residents. Duplicate heroes fuse to raise rank or level.";
        bool free = rules.FreeSummonReady;
        LabelOf(summonOne).text = free ? "FREE SUMMON  (B or better)" : "SUMMON ×1   " + TowerRules.PullCost + " SIGILS";
        LabelOf(summonTen).text = "SUMMON ×10   " + TowerRules.TenPullCost + " SIGILS";
        summonOne.interactable = free || state.sigils >= TowerRules.PullCost;
        summonTen.interactable = state.sigils >= TowerRules.TenPullCost;
        summonOne.GetComponent<Image>().color = free ? Gold : Teal;
        var pulls = rules.LastSummon;
        if (pulls == null || pulls.Count == 0)
            summonResults.text = "The Heart hums, waiting. Sigils come from goals, the daily board, expeditions and Heart rank-ups.";
        else
        {
            var lines = new List<string>();
            foreach (var pull in pulls)
                lines.Add(RankTag(pull.rank) + "   " + pull.name + "   <color=#b7c7d6>" +
                    (pull.kind == "hero" ? "hero  •  " : "resident  •  ") + (pull.fused ? "duplicate fused" :
                        pull.waiting ? "waits in the Heart (no free bed)" : "joins the Tower") + "</color>");
            summonResults.text = string.Join(pulls.Count > 5 ? "\n" : "\n\n", lines.ToArray());
        }

        // Upgrade
        bool top = state.heartRank >= TowerTiers.MaxRank;
        heartRankText.text = "RANK " + RankTag(state.heartRank) + (top ? "   (MAX)" : "   →   " + RankTag(state.heartRank + 1));
        heartUpgradeText.text = top ? "The Heart is at its peak. Every building may reach rank SSR." :
            "Cost: " + rules.HeartUpgradeCelestium() + " Celestium and " + TowerRules.Compact(rules.HeartUpgradeGold()) + " gold" +
            "   (you have " + state.celestium + " C, " + TowerRules.Compact(state.gold) + " g)" +
            (state.legacyRank > 0 ? "\nLegacy rank " + state.legacyRank + ": Heart upgrades cost " +
                Mathf.RoundToInt(TowerRules.LegacyHeartDiscount(state.legacyRank) * 100) + "% less." : "") +
            "\n\nNow: buildings up to rank " + RankTag(rules.RankCap()) + ", Heart HP " +
            Mathf.RoundToInt(TowerRules.HeartMaxHp(state.heartRank)) +
            "\nNext: buildings up to rank " + RankTag(TowerTiers.BuildingCap(state.heartRank + 1)) + ", Heart HP " +
            Mathf.RoundToInt(TowerRules.HeartMaxHp(state.heartRank + 1)) + " (fully restored on upgrade)";
        heartUpgradeButton.interactable = !top;

        // Status
        string stage = rules.HeartStage();
        string stageColor = stage == "stable" ? "#8fe39a" : stage == "strained" ? "#ffd45c" : "#ff7060";
        int returning = state.legacyHeroes == null ? 0 : state.legacyHeroes.Count;
        heartStatusText.text = "HEART HP  " + Mathf.CeilToInt(state.heartHp) + " / " +
            Mathf.RoundToInt(TowerRules.HeartMaxHp(state.heartRank)) +
            "     STAGE  <color=" + stageColor + ">" + stage.ToUpperInvariant() + "</color>" +
            "\nThreat " + Mathf.RoundToInt(state.threat) + "  (" + rules.ThreatLabel() + ")" +
            "\nIncidents now: " + state.incidents.Count +
            "\nRun " + (state.runs + 1) + "   •   Legacy rank " + state.legacyRank + (returning > 0 ?"   •   " + returning + " heroes will return when the Tower is founded" : "") +
            "\n\nIf the Heart falls, this run ends. Heroes, research, Sigils and summon pity carry into the next run; " +
            "the tower itself starts over. Keep guards at the Gate, Tonics in stock and fight incidents early.";
    }

    // ---------------------------------------------------------------- alert tray

    private void BuildAlertsPopup()
    {
        popupAlerts = MakePopup("Alerts popup", 420, 64 + alertTexts.Length * 52, true);
        var t = popupAlerts.transform;
        TextAt(t, "Alerts title", "ALERTS", 16, 10, 300, 28, 17, Gold);
        CloseButton(t, 378, 10, CloseAllPopups);
        alertsEmpty = TextAt(t, "No alerts", "All quiet in the Tower.", 16, 52, 380, 30, 15, Cream);
        for (int i = 0; i < alertTexts.Length; i++)
        {
            int index = i;
            alertTexts[i] = TextAt(t, "Alert " + i, "", 16, 50 + i * 52, 290, 44, 14, Cream);
            alertJumps[i] = ButtonAt(t, "Alert jump " + i, "GO", 316, 52 + i * 52, 88, 40, () => JumpToAlert(index), Alert, 15);
        }
    }

    private void JumpToAlert(int index)
    {
        var incidents = tower.Rules.State.incidents;
        if (index >= incidents.Count) return;
        var room = tower.Rules.Room(incidents[index].roomUid);
        if (room == null) return;
        HidePopups();
        tower.FocusOnFloor(room.floor);
        tower.SelectRoomById(room.uid);
    }

    private void RefreshAlerts(TowerState state)
    {
        alertsEmpty.gameObject.SetActive(state.incidents.Count == 0);
        for (int i = 0; i < alertTexts.Length; i++)
        {
            bool has = i < state.incidents.Count;
            alertTexts[i].gameObject.SetActive(has);
            alertJumps[i].gameObject.SetActive(has);
            if (!has) continue;
            var incident = state.incidents[i];
            var room = tower.Rules.Room(incident.roomUid);
            alertTexts[i].text = TowerRules.IncidentName(incident.kind).ToUpperInvariant() + "\n" +
                (room == null ? "unknown room" : TowerCatalog.Get(room.type).displayName + "  •  floor " + room.floor);
        }
    }

    // Average resident mood as the GDD's five-tier Satisfaction face.
    private string SatisfactionLabel(TowerState state)
    {
        float total = 0; int count = 0;
        foreach (var r in state.residents) if (r.origin != "body") { total += r.happiness; count++; }
        if (count == 0) return "SATISFACTION —";
        int value = Mathf.RoundToInt(total / count);
        string[] faces = { "ANGRY", "UNHAPPY", "NEUTRAL", "HAPPY", "ECSTATIC" };
        string[] colors = { "#ff7060", "#ffad5c", "#d8d8d8", "#8fe39a", "#e9b8ff" };
        int tier = Mathf.Clamp(value / 20, 0, 4);
        return "SATISFACTION <color=" + colors[tier] + ">" + value + " " + faces[tier] + "</color>";
    }
}
