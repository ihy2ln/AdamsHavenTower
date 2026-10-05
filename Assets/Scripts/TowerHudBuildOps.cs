using AdamsHaven.Tower;
using UnityEngine;
using UnityEngine.UI;

// TT 10.4.1: sell back, deconstruct and cancel (TowerSellBack.cs). A construction site tapped in the cutaway gets a
// card with CANCEL; foundation and floor work in the Floors popup cancel from their own buttons; the room card's
// DEMOLISH button reads SELL, UNDO UPGRADE or DECONSTRUCT depending on the sell-back window.
public sealed partial class TowerHud
{
    private Image sitePanel;
    private Text siteText;
    private Button siteCancel;
    private TowerWork shownSite;
    private object cancelArmed;
    private float cancelArmedUntil;

    private void BuildSiteCard()
    {
        sitePanel = Box("Site card", safeRoot, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 142),
            new Vector2(600, 40), new Color(0.10f, 0.07f, 0.03f, 0.92f));
        TowerUiSkin.ApplyPanel(sitePanel, new Color(1f, 0.85f, 0.55f), true);
        siteText = TextAt(sitePanel.transform, "Site text", "", 14, 0, 380, 40, 13, Cream);
        siteCancel = ButtonAt(sitePanel.transform, "Site cancel", "CANCEL", 400, 5, 150, 30, CancelShownSite, Alert, 12);
        ButtonAt(sitePanel.transform, "Site close", "×", 556, 5, 34, 30, () => { shownSite = null; Refresh(); }, Teal, 18);
        sitePanel.gameObject.SetActive(false);
    }

    // The cutaway calls this when a construction site is tapped.
    public void ShowSite(TowerWork work)
    {
        shownSite = work;
        cancelArmed = null;
        Refresh();
    }

    private void CancelShownSite()
    {
        if (shownSite == null) return;
        if (!Armed(shownSite)) { LabelOf(siteCancel).text = "TAP TO CONFIRM"; return; }
        tower.Apply(tower.Rules.CancelWork(shownSite));
        shownSite = null;
    }

    private void RefreshSiteCard()
    {
        if (sitePanel == null) return;
        bool show = shownSite != null && tower.Rules.State.works.Contains(shownSite) && !tower.Placing && !tower.Moving;
        if (!show) shownSite = null;
        sitePanel.gameObject.SetActive(show);
        if (!show) return;
        var w = shownSite;
        string what = w.kind == "room" ? TowerCatalog.Get(w.type).displayName : w.kind == "deconstruct" ? "Taking down " +
            TowerCatalog.Get(w.type).displayName : w.kind == "wing" ? (w.side < 0 ? "West" : "East") + " foundation" : "New floor";
        siteText.text = what.ToUpperInvariant() + "  ·  " + TowerRules.Clock(w.remaining) + " left";
        if (cancelArmed != (object)w || Time.unscaledTime > cancelArmedUntil)
            LabelOf(siteCancel).text = w.kind == "deconstruct" ? "STOP" :
                "CANCEL  +" + TowerRules.RefundText(w.gold, w.wood, w.stone, w.celestium).Replace(" gold", "g").Replace(" wood", "w")
                    .Replace(" stone", "s").Replace(" Celestium", "C");
    }

    // A second tap within three seconds on the same thing confirms it.
    private bool Armed(object target)
    {
        if (cancelArmed == target && Time.unscaledTime <= cancelArmedUntil) { cancelArmed = null; return true; }
        cancelArmed = target;
        cancelArmedUntil = Time.unscaledTime + 3f;
        return false;
    }

    // Floors popup: a wing under construction cancels from its own button.
    private void TapWing(int side)
    {
        var work = tower.Rules.WingWork(tower.FocusFloor, side);
        if (work == null) { ExpandFocused(side); return; }
        if (!Armed(work)) { LabelOf(side < 0 ? floorWest : floorEast).text = "TAP TO CANCEL"; return; }
        tower.Apply(tower.Rules.CancelWork(work));
    }

    private void TapOpenFloor(int dir)
    {
        var rules = tower.Rules;
        if (rules.IsLair && rules.LairFounded) { tower.Apply(dir > 0 ? rules.LairDigFloor() : rules.LairAddLivingFloor()); return; }
        var work = rules.FloorWork(tower.FocusFloor + dir);
        if (work == null) { tower.Apply(rules.OpenFloor(tower.FocusFloor + dir)); return; }
        if (!Armed(work)) { LabelOf(dir > 0 ? floorOpenAbove : floorOpenBelow).text = "TAP TO CANCEL"; return; }
        tower.Apply(rules.CancelWork(work));
    }

    // After RefreshFloors: work in progress stays tappable (to cancel), and the ground floor says which side is next.
    private void RefreshFloorWork(TowerState state)
    {
        var rules = tower.Rules;
        int focus = tower.FocusFloor;
        var here = rules.Floor(focus);
        if (rules.FloorWork(focus + 1) != null) { floorOpenAbove.interactable = true; if (cancelArmed == null) LabelOf(floorOpenAbove).text = "BUILDING " + TowerRules.Clock(rules.FloorWork(focus + 1).remaining) + "  (tap: cancel)"; }
        if (rules.FloorWork(focus - 1) != null) { floorOpenBelow.interactable = true; if (cancelArmed == null) LabelOf(floorOpenBelow).text = "BUILDING " + TowerRules.Clock(rules.FloorWork(focus - 1).remaining) + "  (tap: cancel)"; }
        if (here == null) return;
        for (int side = -1; side <= 1; side += 2)
        {
            var button = side < 0 ? floorWest : floorEast;
            var work = rules.WingWork(focus, side);
            if (work != null)
            {
                button.interactable = true;
                if (cancelArmed != (object)work) LabelOf(button).text = "BUILDING " + TowerRules.Clock(work.remaining) + " (tap: cancel)";
                continue;
            }
            int cells = rules.WingCellsOf(focus, side);
            string name = side < 0 ? "< WEST" : "EAST >";
            if (cells >= TowerRules.WingCells) { button.interactable = false; LabelOf(button).text = name + "  FULL"; continue; }
            if (focus == 0 && cells > rules.WingCellsOf(0, -side))
            {
                button.interactable = false;
                LabelOf(button).text = name + "  (" + (side < 0 ? "EAST" : "WEST") + " FIRST)";
                continue;
            }
            button.interactable = true;
            LabelOf(button).text = name + "  " + rules.ExpandCost(focus, side) + "C   (" + cells + "/" + TowerRules.WingCells + ")";
        }
    }

    // The room card's DEMOLISH button.
    private string DemolishLabel(TowerRoom room)
    {
        var rules = tower.Rules;
        if (rules.DeconstructWork(room.uid) != null) return "STOP TEARDOWN";
        if (rules.SellShare(room) > 0)
            return (rules.SellingUndoesUpgrade(room) ? "UNDO UPGRADE +" : "SELL +") + rules.DemolishRefund(room) + "g " +
                TowerRules.Clock(rules.SellSecondsLeft(room));
        return "DECONSTRUCT " + TowerRules.Clock(rules.DeconstructSeconds(room));
    }
}
