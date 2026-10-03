using System.Collections.Generic;
using AdamsHaven.Tower;
using UnityEngine;
using UnityEngine.UI;

// GDD 10.3 auto expeditions (TT 10.30.0): pick an opened region and up to three heroes, see the predicted result,
// send them off on the real clock. The Expeditions dock button's hold menu reaches this, the map and the outposts.
public sealed partial class TowerHud
{
    private const int AutoHeroSlots = 6;
    private Image popupAuto;
    private Text autoTitle, autoRegion, autoInfo, autoPrediction, autoReport, autoPageText;
    private Button autoSend, autoRecall, autoPrev, autoNext;
    private readonly Button[] autoHeroes = new Button[AutoHeroSlots];
    private readonly List<int> autoParty = new List<int>();
    private int autoRegionIndex, autoPage;

    private FlyItem[] ExpeditionItems()
    {
        return new[] {
            new FlyItem("EXPEDITION MAP", OpenGuildBoard),
            new FlyItem("AUTO EXPEDITION", OpenAuto),
            new FlyItem("OUTPOSTS", OpenOutposts)
        };
    }

    private void BuildAutoPopup()
    {
        popupAuto = Box("Auto expedition", safeRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10),
            new Vector2(720, 480), Glass);
        TowerUiSkin.ApplyPanel(popupAuto, Glass, true);
        var t = popupAuto.transform;
        autoTitle = TextAt(t, "Auto title", "AUTO EXPEDITION", 16, 10, 640, 30, 19, Gold);
        CloseButton(t, 680, 12, CloseAllPopups);
        Divider(t, 12, 46, 696);
        autoPrev = ButtonAt(t, "Auto region previous", "‹", 16, 56, 56, 40, () => { autoRegionIndex--; Refresh(); }, Teal, 22);
        autoRegion = TextAt(t, "Auto region", "", 80, 56, 560, 40, 17, Cream, TextAnchor.MiddleCenter);
        autoNext = ButtonAt(t, "Auto region next", "›", 648, 56, 56, 40, () => { autoRegionIndex++; Refresh(); }, Teal, 22);
        autoInfo = TextAt(t, "Auto info", "", 16, 102, 688, 44, 13, Cream, TextAnchor.UpperLeft);
        TextAt(t, "Auto party title", "PARTY  (tap up to three heroes)", 16, 150, 400, 24, 14, Gold);
        for (int i = 0; i < AutoHeroSlots; i++)
        {
            int slot = i;
            autoHeroes[i] = ButtonAt(t, "Auto hero " + i, "", 16 + (i % 3) * 230, 178 + (i / 3) * 52, 222, 46,
                () => ToggleAutoHero(slot), Teal, 13);
        }
        ButtonAt(t, "Auto heroes previous", "‹", 16, 286, 56, 30, () => { autoPage = Mathf.Max(0, autoPage - 1); Refresh(); }, Teal, 20);
        autoPageText = TextAt(t, "Auto heroes page", "", 80, 286, 560, 30, 13, Cream, TextAnchor.MiddleCenter);
        ButtonAt(t, "Auto heroes next", "›", 648, 286, 56, 30, () => { autoPage++; Refresh(); }, Teal, 22);
        autoPrediction = TextAt(t, "Auto prediction", "", 16, 322, 688, 44, 15, Cream, TextAnchor.UpperLeft);
        autoSend = ButtonAt(t, "Auto send", "SEND", 16, 370, 452, 48, SendAuto, Teal, 17);
        autoRecall = ButtonAt(t, "Auto recall", "RECALL", 476, 370, 228, 48,
            () => tower.Apply(tower.Rules.RecallAuto()), Alert, 15);
        autoReport = TextAt(t, "Auto report", "", 16, 424, 688, 48, 13, Cream, TextAnchor.UpperLeft);
        popupAuto.gameObject.SetActive(false);
    }

    private void OpenAuto()
    {
        string error = tower.Rules.GuildRequired();
        if (error != null) { tower.Apply(error); return; }
        TogglePopup(popupAuto);
    }

    private List<TowerRegionDef> AutoRegions()
    {
        var list = new List<TowerRegionDef>();
        foreach (var region in TowerRules.Regions) if (tower.Rules.RegionUnlocked(region.id)) list.Add(region);
        return list;
    }

    private List<TowerResident> AutoHeroes()
    {
        return tower.Rules.State.residents.FindAll(r => r.origin == "hero" && r.ageStage == 0);
    }

    private void ToggleAutoHero(int slot)
    {
        var heroes = AutoHeroes();
        int index = autoPage * AutoHeroSlots + slot;
        if (index >= heroes.Count) return;
        int id = heroes[index].id;
        if (autoParty.Contains(id)) autoParty.Remove(id);
        else if (autoParty.Count < 3 && tower.Rules.CanJoinAuto(heroes[index])) autoParty.Add(id);
        Refresh();
    }

    private void SendAuto()
    {
        var regions = AutoRegions();
        if (regions.Count == 0) return;
        string error = tower.Rules.SendAuto(regions[Mathf.Clamp(autoRegionIndex, 0, regions.Count - 1)].id, new List<int>(autoParty));
        if (error == null) autoParty.Clear();
        tower.Apply(error);
    }

    private void RefreshAuto(TowerState state)
    {
        var rules = tower.Rules;
        var run = rules.AutoRun;
        var regions = AutoRegions();
        autoReport.text = string.IsNullOrEmpty(state.autoReport) ? "No party has come back yet." : "Last return: " + state.autoReport;
        autoRecall.gameObject.SetActive(run != null);
        autoSend.gameObject.SetActive(run == null);
        autoPrev.interactable = autoNext.interactable = run == null && regions.Count > 1;
        if (run != null)
        {
            var region = TowerRules.Region(run.region);
            string names = "";
            foreach (int id in run.party) { var hero = rules.Resident(id); if (hero != null) names += (names.Length > 0 ? ", " : "") + hero.name; }
            long left = rules.AutoSecondsLeft();
            autoTitle.text = "AUTO EXPEDITION  •  OUT";
            autoRegion.text = (region == null ? run.region : region.name).ToUpperInvariant();
            autoInfo.text = names + " set out " + Mathf.RoundToInt(rules.AutoProgress() * 100) + "% of the way through. Back in " +
                (left / 3600) + "h " + (left % 3600 / 60).ToString("00") + "m (real time, also while the game is closed).";
            autoPrediction.text = "Expected: " + TowerRules.AutoTiers[TowerRules.AutoTier(run.power / Mathf.Max(1, run.danger))].ToUpperInvariant() +
                "   (power " + Mathf.RoundToInt(run.power) + " vs danger " + Mathf.RoundToInt(run.danger) + ")";
            foreach (var button in autoHeroes) button.gameObject.SetActive(false);
            autoPageText.text = "";
            return;
        }
        autoTitle.text = "AUTO EXPEDITION";
        if (regions.Count == 0)
        {
            autoRegion.text = "NO REGION OPEN";
            autoInfo.text = "Conquer Brook Edge on a played expedition first.";
            autoSend.interactable = false;
            foreach (var button in autoHeroes) button.gameObject.SetActive(false);
            autoPrediction.text = autoPageText.text = "";
            return;
        }
        autoRegionIndex = (autoRegionIndex % regions.Count + regions.Count) % regions.Count;
        var chosen = regions[autoRegionIndex];
        autoRegion.text = chosen.name.ToUpperInvariant() + "   (" + (autoRegionIndex + 1) + " / " + regions.Count + ")";
        autoInfo.text = "Danger " + Mathf.RoundToInt(TowerRules.AutoDangerOf(chosen.id)) + "   •   " +
            TowerRules.AutoHoursOf(chosen.id).ToString("0.#") + " h   •   rewards x" + chosen.reward.ToString("0.0") +
            "\nCosts " + TowerRules.AutoFood + " food and " + TowerRules.AutoWater + " water per hero. Auto runs never conquer a region.";
        var heroes = AutoHeroes();
        autoParty.RemoveAll(id => !heroes.Exists(h => h.id == id && rules.CanJoinAuto(h)));
        int pages = Mathf.Max(1, Mathf.CeilToInt(heroes.Count / (float)AutoHeroSlots));
        autoPage = Mathf.Clamp(autoPage, 0, pages - 1);
        autoPageText.text = heroes.Count + " heroes  •  page " + (autoPage + 1) + " of " + pages;
        for (int i = 0; i < AutoHeroSlots; i++)
        {
            int index = autoPage * AutoHeroSlots + i;
            var button = autoHeroes[i];
            button.gameObject.SetActive(index < heroes.Count);
            if (index >= heroes.Count) continue;
            var hero = heroes[index];
            bool ready = rules.CanJoinAuto(hero), picked = autoParty.Contains(hero.id);
            button.interactable = ready || picked;
            button.GetComponent<Image>().color = picked ? Gold : ready ? Teal : Muted;
            LabelOf(button).text = hero.name + "  " + TowerTiers.Tier(Mathf.Max(1, hero.rank)) + "\n" +
                (ready ? "power " + Mathf.RoundToInt(TowerRules.HeroPower(hero)) :
                    TowerRules.IsPosted(hero) ? "away" : hero.away ? "on the map" : hero.downed ? "down" : "injured");
        }
        string prediction = rules.AutoPrediction(chosen.id, autoParty);
        autoPrediction.text = autoParty.Count == 0 ? "Pick heroes to see the expected result." :
            "Party power " + Mathf.RoundToInt(rules.PartyPower(autoParty)) + " vs danger " +
            Mathf.RoundToInt(TowerRules.AutoDangerOf(chosen.id)) + "   →   " + prediction.ToUpperInvariant() +
            "   (Tank + Support +10%, Controller +5%)";
        autoSend.interactable = rules.CanSendAuto(chosen.id, autoParty) == null;
        LabelOf(autoSend).text = "SEND " + autoParty.Count + " TO " + chosen.name.ToUpperInvariant();
    }
}
