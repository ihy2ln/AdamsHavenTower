using System.Collections.Generic;
using AdamsHaven.Tower;
using UnityEngine;
using UnityEngine.UI;

// Outposts (TT 10.30.0): every conquered region offers a site; found it, station residents there, and caravans
// bring its goods home. Reached from the Guild board and the Expeditions dock button's hold menu.
public sealed partial class TowerHud
{
    private const int OutpostRows = 7, StaffButtons = 6;
    private Image popupOutposts;
    private Text outpostTitle, outpostDetail, outpostStation, outpostReport;
    private Button outpostFound, outpostStationButton, outpostUpgrade, outpostCaravan, outpostAbandon;
    private readonly Button[] outpostRows = new Button[OutpostRows];
    private readonly Button[] outpostStaff = new Button[StaffButtons];
    private string outpostChosen = "";
    private float abandonArmedUntil;

    private void BuildOutpostsPopup()
    {
        popupOutposts = Box("Outposts", safeRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10),
            new Vector2(720, 480), Glass);
        TowerUiSkin.ApplyPanel(popupOutposts, Glass, true);
        var t = popupOutposts.transform;
        outpostTitle = TextAt(t, "Outposts title", "OUTPOSTS", 16, 10, 640, 30, 19, Gold);
        CloseButton(t, 680, 12, CloseAllPopups);
        Divider(t, 12, 46, 696);
        for (int i = 0; i < OutpostRows; i++)
        {
            int row = i;
            outpostRows[i] = ButtonAt(t, "Outpost row " + i, "", 16, 56 + i * 58, 230, 52, () => ChooseOutpost(row), Teal, 12);
        }
        outpostDetail = TextAt(t, "Outpost detail", "", 260, 56, 444, 132, 13, Cream, TextAnchor.UpperLeft);
        outpostFound = ButtonAt(t, "Found outpost", "FOUND", 260, 194, 444, 46,
            () => tower.Apply(tower.Rules.FoundOutpost(outpostChosen)), Teal, 15);
        outpostStation = TextAt(t, "Outpost station hint", "", 260, 194, 300, 40, 12, Cream);
        outpostStationButton = ButtonAt(t, "Station resident", "STATION", 566, 194, 138, 40, StationSelected, Teal, 13);
        for (int i = 0; i < StaffButtons; i++)
        {
            int slot = i;
            outpostStaff[i] = ButtonAt(t, "Outpost staff " + i, "", 260 + (i % 3) * 150, 240 + (i / 3) * 44, 144, 38,
                () => RecallStaff(slot), Muted, 12);
        }
        outpostUpgrade = ButtonAt(t, "Upgrade outpost", "UPGRADE", 260, 332, 216, 40,
            () => tower.Apply(tower.Rules.UpgradeOutpost(outpostChosen)), Teal, 13);
        outpostCaravan = ButtonAt(t, "Send caravan", "SEND CARAVAN", 488, 332, 216, 40,
            () => tower.Apply(tower.Rules.SendCaravan(outpostChosen)), Teal, 13);
        outpostAbandon = ButtonAt(t, "Abandon outpost", "ABANDON", 260, 378, 216, 34, ConfirmAbandon, Alert, 12);
        outpostReport = TextAt(t, "Outpost report", "", 260, 418, 444, 54, 12, Cream, TextAnchor.UpperLeft);
        popupOutposts.gameObject.SetActive(false);
    }

    private void OpenOutposts()
    {
        string error = tower.Rules.GuildRequired();
        if (error != null) { tower.Apply(error); return; }
        TogglePopup(popupOutposts);
    }

    // Outposts first, then the open sites, in region order.
    private List<string> OutpostEntries()
    {
        var list = new List<string>();
        var rules = tower.Rules;
        foreach (var region in TowerRules.Regions) if (rules.Outpost(region.id) != null) list.Add(region.id);
        foreach (string site in rules.OutpostSites()) list.Add(site);
        return list;
    }

    private void ChooseOutpost(int row)
    {
        var entries = OutpostEntries();
        if (row < entries.Count) outpostChosen = entries[row];
        Refresh();
    }

    private void StationSelected()
    {
        var person = tower.SelectedPerson;
        tower.Apply(person == null ? "Select a resident in PEOPLE first." : tower.Rules.Station(person.id, outpostChosen));
    }

    private void RecallStaff(int slot)
    {
        var outpost = tower.Rules.Outpost(outpostChosen);
        if (outpost != null && slot < outpost.staff.Count) tower.Apply(tower.Rules.Unstation(outpost.staff[slot]));
    }

    private void ConfirmAbandon()
    {
        if (Time.unscaledTime > abandonArmedUntil)
        {
            abandonArmedUntil = Time.unscaledTime + 3f;
            LabelOf(outpostAbandon).text = "TAP TO CONFIRM";
            return;
        }
        abandonArmedUntil = 0;
        tower.Apply(tower.Rules.AbandonOutpost(outpostChosen));
    }

    private static string Produces(TowerOutpostSpec spec)
    { return spec.primary + " and some " + spec.secondary + " (" + spec.stat + " counts)"; }

    private void RefreshOutposts(TowerState state)
    {
        var rules = tower.Rules;
        var entries = OutpostEntries();
        outpostTitle.text = "OUTPOSTS   " + state.outposts.Count + " / " + TowerRules.MaxOutposts(state.heartRank) +
            "   •   " + state.regionsConquered.Count + " regions conquered";
        if (!entries.Contains(outpostChosen)) outpostChosen = entries.Count > 0 ? entries[0] : "";
        for (int i = 0; i < OutpostRows; i++)
        {
            var row = outpostRows[i];
            row.gameObject.SetActive(i < entries.Count);
            if (i >= entries.Count) continue;
            var outpost = rules.Outpost(entries[i]);
            var spec = TowerRules.OutpostSpec(entries[i]);
            LabelOf(row).text = (outpost == null ? "SITE  " : TowerTiers.Tier(outpost.level) + "  ") + spec.name.ToUpperInvariant() +
                "\n" + TowerRules.Region(entries[i]).name + (outpost == null ? "  •  unfounded" : "  •  staff " + outpost.staff.Count +
                    "/" + TowerRules.OutpostSlots(outpost.level));
            row.GetComponent<Image>().color = entries[i] == outpostChosen ? Gold : outpost == null ? Muted : Teal;
        }
        bool none = outpostChosen.Length == 0;
        var chosen = none ? null : rules.Outpost(outpostChosen);
        var chosenSpec = none ? null : TowerRules.OutpostSpec(outpostChosen);
        outpostFound.gameObject.SetActive(!none && chosen == null);
        outpostStation.gameObject.SetActive(chosen != null);
        outpostStationButton.gameObject.SetActive(chosen != null);
        outpostUpgrade.gameObject.SetActive(chosen != null);
        outpostCaravan.gameObject.SetActive(chosen != null);
        outpostAbandon.gameObject.SetActive(chosen != null);
        foreach (var button in outpostStaff) button.gameObject.SetActive(false);
        if (none)
        {
            outpostDetail.text = "No sites yet.\n\nConquer a region on a played expedition (clear its lair) and a site for an outpost " +
                "opens there. Outposts are staffed colonies: they make that region's goods and send them home by caravan.";
            outpostReport.text = "";
            return;
        }
        if (chosen == null)
        {
            outpostDetail.text = chosenSpec.name.ToUpperInvariant() + "  in " + TowerRules.Region(outpostChosen).name +
                "\nMakes " + Produces(chosenSpec) + ".\nStaffed by residents you station there (villagers welcome). " +
                "Caravans come home every half day; raiders come for outposts left weak.";
            LabelOf(outpostFound).text = "FOUND   " + rules.FoundCost(outpostChosen) + "g  " + TowerRules.FoundWood + " wood  " +
                TowerRules.FoundStone + " stone";
            outpostFound.interactable = state.outposts.Count < TowerRules.MaxOutposts(state.heartRank);
            outpostReport.text = state.outposts.Count >= TowerRules.MaxOutposts(state.heartRank) ?
                "Raise the Celestium Heart to hold more outposts." : "";
            return;
        }
        string daily = "";
        foreach (string resource in new[] { chosenSpec.primary, chosenSpec.secondary })
            daily += (daily.Length > 0 ? ",  " : "") + rules.OutpostDaily(chosen, resource).ToString("0.#") + " " + resource;
        string cargo = "";
        foreach (string resource in new[] { chosenSpec.primary, chosenSpec.secondary })
            cargo += (cargo.Length > 0 ? ",  " : "") + Mathf.FloorToInt(rules.OutpostCargo(chosen, resource)) + " " + resource;
        outpostDetail.text = chosenSpec.name.ToUpperInvariant() + "   rank " + TowerTiers.Tier(chosen.level) +
            "\n" + TowerRules.Region(outpostChosen).name + "  •  condition " + Mathf.RoundToInt(chosen.condition) + "%  •  threat " +
            Mathf.RoundToInt(chosen.threat) + "  •  defence " + Mathf.RoundToInt(rules.OutpostDefence(chosen)) +
            "\nPer day: " + (chosen.staff.Count == 0 ? "nothing until someone is stationed" : daily) +
            "\nWaiting for the caravan: " + cargo + "  (next in " + TowerRules.Clock(TowerRules.CaravanSeconds - chosen.caravanSeconds) + ")" +
            "\nAmbush risk on the road " + Mathf.RoundToInt(rules.AmbushChance(chosen) * 100) + "%";
        var person = tower.SelectedPerson;
        outpostStation.text = person == null ? "Select a resident in PEOPLE to station them here." :
            "Station " + person.name + " (" + chosenSpec.stat + " " + person.Stat(chosenSpec.stat) + ")";
        outpostStationButton.interactable = person != null && rules.CanStation(person) &&
            chosen.staff.Count < TowerRules.OutpostSlots(chosen.level);
        for (int i = 0; i < StaffButtons; i++)
        {
            bool has = i < chosen.staff.Count;
            outpostStaff[i].gameObject.SetActive(has);
            if (!has) continue;
            var staff = rules.Resident(chosen.staff[i]);
            LabelOf(outpostStaff[i]).text = "× " + (staff == null ? "?" : staff.name) +
                (staff != null && staff.postedSeconds >= TowerRules.HomesickAfter ? "  (homesick)" : "");
        }
        bool top = chosen.level >= TowerTiers.MaxRank;
        LabelOf(outpostUpgrade).text = top ? "RANK SSR" : chosen.level >= rules.RankCap() ? "HEART " + TowerTiers.Tier(chosen.level + 1) + "+" :
            "TO " + TowerTiers.Tier(chosen.level + 1) + "  " + rules.OutpostUpgradeGold(chosen) + "g";
        outpostUpgrade.interactable = !top && chosen.level < rules.RankCap();
        if (abandonArmedUntil > 0 && Time.unscaledTime > abandonArmedUntil) abandonArmedUntil = 0;
        if (abandonArmedUntil <= 0) LabelOf(outpostAbandon).text = "ABANDON";
        outpostReport.text = chosen.report;
    }
}
