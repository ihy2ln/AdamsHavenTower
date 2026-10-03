using AdamsHaven.Tower;
using UnityEngine;
using UnityEngine.UI;

// TOWER_MODE_GDD 19.2: the TOWN view's HUD. A TOWN button switches views; while the town shows, the Tower HUD hides
// and a full-screen overlay catches input (so the Tower's camera ignores it) and holds the town's own controls:
// back to the Tower, the camera angle, and a preview of the ring at other Heart ranks (greybox, TT 10.3.2).
public sealed partial class TowerHud
{
    private TowerTownView townView;
    private Image townOverlay;
    private Text townTitle, townInfo, townSelection;
    private Button townButton, townAngle;

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
        ButtonAt(bar.transform, "Back to Tower", "◀ TOWER", 10, 8, 170, 48, ToggleTown, Gold, 16);
        townAngle = ButtonAt(bar.transform, "Town angle", "ANGLE", 190, 8, 220, 48,
            () => { townView.CycleAngle(); RefreshTown(); }, Teal, 15);
        ButtonAt(bar.transform, "Town rank down", "◀ RANK", 420, 8, 160, 48,
            () => { townView.StepPreview(-1); RefreshTown(); }, Violet, 15);
        ButtonAt(bar.transform, "Town rank up", "RANK ▶", 590, 8, 160, 48,
            () => { townView.StepPreview(1); RefreshTown(); }, Violet, 15);
        var tap = Box("Town tap", townOverlay.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 84),
            new Vector2(560, 28), Glass);
        TowerUiSkin.ApplyPanel(tap, Glass, false);
        townSelection = TextAt(tap.transform, "Town selection", "", 10, 0, 540, 28, 13, Cream, TextAnchor.MiddleCenter);
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

    private void RefreshTown()
    {
        if (!TownShowing) return;
        var state = tower.Rules.State;
        // Anything that needs the Tower's own screens brings the player back to it.
        if (state.defeated || tower.Rules.SiegeComing)
        {
            ToggleTown();
            if (tower.Rules.SiegeComing) tower.Apply("A siege is gathering at the Gate.");
            return;
        }
        int rank = townView.ShownRank;
        bool preview = townView.PreviewRank > 0;
        townTitle.text = "TOWN  •  SILVERBROOK" + (preview ? "   <color=#ffd45c>(PREVIEW)</color>" : "");
        townInfo.text = "Heart rank " + RankTag(rank) + (preview ? " (yours: " + RankTag(state.heartRank) + ")" : "") +
            "   •   ring " + TowerTownMap.Radius(rank) + " tiles   •   " + TowerTownMap.LotCount(rank) + " lots   •   " +
            townView.Buildings + " placeholder buildings   •   " + townView.AngleLabel;
        LabelOf(townAngle).text = "ANGLE: " + townView.AngleLabel;
        townSelection.text = townView.Selection.Length > 0 ? "TAPPED  " + townView.Selection : "Tap a tile to inspect it. Drag to pan, pinch or scroll to zoom.";
    }

    // Called from TowerHud.Update: the town's own pointer handling runs outside Refresh, so keep the tapped line current.
    private void TickTown()
    {
        if (TownShowing && townSelection != null && townView.Selection != shownSelection)
        { shownSelection = townView.Selection; RefreshTown(); }
    }

    private string shownSelection = "";
}
