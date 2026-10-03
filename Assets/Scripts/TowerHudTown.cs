using System.Collections.Generic;
using AdamsHaven.Tower;
using UnityEngine;
using UnityEngine.UI;

// TOWER_MODE_GDD 19.2: the TOWN view's HUD. A TOWN button switches views; while the town shows, the Tower HUD hides
// and a full-screen overlay catches input (so the Tower's camera ignores it) and holds the town's own controls:
// back to the Tower, the camera angle, a preview of the ring at other Heart ranks, AREA (drag a box instead of
// panning), and the lot panel. The town runs itself; the lot panel is the player's override (owner, 2026-10-03):
// pick a building type and rank and BUILD it on the selected lots (they become yours), hand lots back to the TOWN,
// or LOCK them as they are.
public sealed partial class TowerHud
{
    private TowerTownView townView;
    private Image townOverlay, lotPanel;
    private Text townTitle, townInfo, townSelection, lotTypeText, lotRankText;
    private Button townButton, townAngle, townArea, lotBuild, lotAuto, lotLock;
    private int lotType = 0, lotRank = 1, shownSelectionStamp = -1;   // lotType -1 = keep vacant

    public bool TownShowing { get { return townView != null && townView.Active; } }

    private void BuildTownButton(Transform parent)
    {
        townButton = ButtonAt(parent, "Town view", "TOWN", 0, 66, 90, 24, ToggleTown, Gold, 11);
    }

    private void BuildTownOverlay()
    {
        // Built on the canvas itself, not the safe root, so it stays up while the Tower HUD hides.
        townOverlay = Rect("Town overlay", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0, 0, 0, 0));
        townOverlay.raycastTarget = true;   // the input catcher: the Tower's camera sees the pointer as over UI
        var top = Box("Town top", townOverlay.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10),
            new Vector2(760, 64), Glass);
        TowerUiSkin.ApplyPanel(top, Glass, true);
        townTitle = TextAt(top.transform, "Town title", "", 16, 6, 728, 26, 18, Gold, TextAnchor.MiddleCenter);
        townInfo = TextAt(top.transform, "Town info", "", 16, 34, 728, 24, 13, Cream, TextAnchor.MiddleCenter);

        var bar = Box("Town controls", townOverlay.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14),
            new Vector2(760, 64), Glass);
        TowerUiSkin.ApplyPanel(bar, Glass, true);
        ButtonAt(bar.transform, "Back to Tower", "◀ TOWER", 10, 8, 130, 48, ToggleTown, Gold, 15);
        townAngle = ButtonAt(bar.transform, "Town angle", "ANGLE", 150, 8, 190, 48,
            () => { townView.CycleAngle(); RefreshTown(); }, Teal, 14);
        ButtonAt(bar.transform, "Town rank down", "◀ RANK", 350, 8, 120, 48,
            () => { townView.StepPreview(-1); RefreshTown(); }, Violet, 14);
        ButtonAt(bar.transform, "Town rank up", "RANK ▶", 480, 8, 120, 48,
            () => { townView.StepPreview(1); RefreshTown(); }, Violet, 14);
        townArea = ButtonAt(bar.transform, "Town area", "AREA", 610, 8, 140, 48,
            () => { townView.BoxMode = !townView.BoxMode; RefreshTown(); }, Teal, 14);

        // Lot panel: what is selected, and the override controls.
        lotPanel = Box("Town lot panel", townOverlay.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 84),
            new Vector2(760, 92), Glass);
        TowerUiSkin.ApplyPanel(lotPanel, Glass, true);
        var p = lotPanel.transform;
        townSelection = TextAt(p, "Town selection", "", 12, 6, 736, 26, 13, Cream, TextAnchor.MiddleCenter);
        ButtonAt(p, "Lot type prev", "◀", 10, 38, 40, 44, () => StepLotType(-1), Violet, 16);
        lotTypeText = TextAt(p, "Lot type", "", 54, 38, 200, 44, 14, Gold, TextAnchor.MiddleCenter);
        ButtonAt(p, "Lot type next", "▶", 258, 38, 40, 44, () => StepLotType(1), Violet, 16);
        ButtonAt(p, "Lot rank down", "-", 306, 38, 36, 44, () => StepLotRank(-1), Violet, 18);
        lotRankText = TextAt(p, "Lot rank", "", 344, 38, 50, 44, 15, Cream, TextAnchor.MiddleCenter);
        ButtonAt(p, "Lot rank up", "+", 396, 38, 36, 44, () => StepLotRank(1), Violet, 18);
        lotBuild = ButtonAt(p, "Lot build", "BUILD", 440, 38, 140, 44, BuildLots, Gold, 14);
        lotAuto = ButtonAt(p, "Lot auto", "TOWN", 588, 38, 78, 44, () => HandLots(true), Teal, 13);
        lotLock = ButtonAt(p, "Lot lock", "LOCK", 672, 38, 78, 44, () => HandLots(false), Teal, 13);
        townOverlay.gameObject.SetActive(false);
    }

    private void ToggleTown()
    {
        if (tower == null || tower.Rules == null) return;
        if (TownShowing)
        {
            townView.Exit();
            townOverlay.gameObject.SetActive(false);
            safeRoot.gameObject.SetActive(true);
            Refresh();
            return;
        }
        if (tower.Rules.State.introPhase != "complete") { tower.Apply("Found the Tower first."); return; }
        if (townView == null)
        {
            townView = gameObject.AddComponent<TowerTownView>();
            townView.Initialize(tower, townOverlay.gameObject);
        }
        HidePopups();
        safeRoot.gameObject.SetActive(false);
        townOverlay.gameObject.SetActive(true);
        townView.Enter();
        RefreshTown();
    }

    // ---------------------------------------------------------------- lot override controls

    private List<Vector2Int> SelectedLots()
    {
        var lots = new List<Vector2Int>();
        foreach (var t in townView.Selected)
            if (TowerTownMap.Buildable(t.x, t.y, tower.Rules.State.heartRank)) lots.Add(t);
        return lots;
    }

    private string LotTypeId { get { return lotType < 0 ? "" : TowerRules.TownBuildingDefs[lotType].id; } }

    private void StepLotType(int step)
    {
        int n = TowerRules.TownBuildingDefs.Length;
        lotType = (lotType + step + n + 2) % (n + 1) - 1;   // -1 (vacant), 0 .. n-1
        RefreshTown();
    }

    private void StepLotRank(int step)
    {
        lotRank = Mathf.Clamp(lotRank + step, 1, tower.Rules.State.heartRank);
        RefreshTown();
    }

    private void BuildLots()
    {
        var lots = SelectedLots();
        string error = lots.Count == 1 ? tower.Rules.SetLot(lots[0].x, lots[0].y, LotTypeId, lotRank) :
            tower.Rules.SetLots(lots, LotTypeId, lotRank);
        tower.Apply(error);
        townView.RefreshLots();
        RefreshTown();
    }

    private void HandLots(bool auto)
    {
        int changed = tower.Rules.SetLotsAuto(SelectedLots(), auto);
        tower.Apply(changed == 0 ? (auto ? "Those lots are already the town's." : "Those lots are already yours.") : null);
        townView.RefreshLots();
        RefreshTown();
    }

    // A new selection starts the panel from the first lot's type and rank.
    private void SyncLotPanel()
    {
        shownSelectionStamp = townView.SelectionStamp;
        var lots = SelectedLots();
        if (lots.Count == 0) return;
        var lot = tower.Rules.Lot(lots[0].x, lots[0].y);
        if (lot != null && lot.type.Length > 0)
        {
            lotType = System.Array.FindIndex(TowerRules.TownBuildingDefs, d => d.id == lot.type);
            lotRank = lot.rank;
        }
        else lotRank = Mathf.Clamp(lotRank, 1, tower.Rules.State.heartRank);
    }

    private void RefreshTown()
    {
        if (!TownShowing) return;
        var rules = tower.Rules;
        var state = rules.State;
        // Anything that needs the Tower's own screens brings the player back to it.
        if (state.defeated || rules.SiegeComing)
        {
            ToggleTown();
            if (rules.SiegeComing) tower.Apply("A siege is gathering at the Gate.");
            return;
        }
        if (townView.SelectionStamp != shownSelectionStamp) SyncLotPanel();
        int rank = townView.ShownRank;
        bool preview = townView.PreviewRank > 0;
        townTitle.text = "TOWN  •  SILVERBROOK" + (preview ? "   <color=#ffd45c>(PREVIEW)</color>" : "");
        townInfo.text = "Heart rank " + RankTag(rank) + (preview ? " (yours: " + RankTag(state.heartRank) + ")" : "") +
            "   •   ring " + TowerTownMap.Radius(rank) + " tiles   •   " + (preview ? townView.Buildings + " preview buildings" :
                rules.TownBuiltLots() + " / " + rules.TownTargetLots() + " buildings") +
            "   •   " + TowerRules.Compact(state.gold) + " gold   •   " + townView.AngleLabel;
        LabelOf(townAngle).text = "ANGLE: " + townView.AngleLabel;
        LabelOf(townArea).text = townView.BoxMode ? "AREA: ON" : "AREA";
        townArea.GetComponent<Image>().color = townView.BoxMode ? Gold : Teal;

        var lots = SelectedLots();
        bool canEdit = !preview && lots.Count > 0;
        if (townView.Selected.Count == 0)
            townSelection.text = townView.BoxMode ? "AREA: drag a box over the town to select a group of lots." :
                "Tap a lot to change it, or AREA to select a group. The town builds on its own; your lots carry a flag.";
        else if (townView.Selected.Count == 1)
            townSelection.text = "(" + townView.Selected[0].x + ", " + townView.Selected[0].y + ")   " + townView.Describe(townView.Selected[0]);
        else
        {
            int mine = 0;
            foreach (var t in lots) { var l = rules.Lot(t.x, t.y); if (l != null && l.manual) mine++; }
            townSelection.text = lots.Count + " lots selected   •   " + mine + " yours, " + (lots.Count - mine) + " the town's";
        }
        string typeName = lotType < 0 ? "VACANT" : TowerRules.TownBuildingDefs[lotType].name.ToUpperInvariant();
        string district = lotType < 0 ? "keep empty" : TowerRules.TownBuildingDefs[lotType].district;
        lotTypeText.text = typeName + "\n<size=11><color=#b7c7d6>" + district + "</color></size>";
        lotRankText.text = RankTag(lotRank);
        int cost = 0;
        foreach (var t in lots)
        {
            var l = rules.Lot(t.x, t.y);
            if (l == null || l.type != LotTypeId || l.rank != lotRank) cost += rules.LotCost(LotTypeId, lotRank);
        }
        LabelOf(lotBuild).text = lotType < 0 ? "CLEAR" : "BUILD  " + cost + "g";
        lotBuild.interactable = canEdit && state.gold >= cost;
        lotAuto.interactable = lotLock.interactable = canEdit;
        lotPanel.gameObject.SetActive(!preview);
    }

    // Called from TowerHud.Update: the town's own pointer handling runs outside Refresh, so keep the panel current.
    private void TickTown()
    {
        if (TownShowing && townSelection != null && townView.SelectionStamp != shownSelectionStamp) RefreshTown();
    }
}
