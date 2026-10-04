using AdamsHaven.Tower;
using UnityEngine;
using UnityEngine.UI;

// GDD 9.6 (TT 10.30.1): while a siege gathers, a banner under the top bar counts it down and offers to meet it in
// Battle Mode with the heroes at home. Ignoring it lets the guards auto-defend when the timer runs out.
public sealed partial class TowerHud
{
    private Image siegePanel;
    private Text siegeText;
    private Button siegeFight;

    private void BuildSiegeBanner()
    {
        siegePanel = Box("Siege banner", safeRoot, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -120),
            new Vector2(680, 46), new Color(0.16f, 0.04f, 0.03f, 0.92f));
        TowerUiSkin.ApplyPanel(siegePanel, Alert, true);
        siegeText = TextAt(siegePanel.transform, "Siege text", "", 28, 0, 428, 46, 14, Cream);
        siegeFight = ButtonAt(siegePanel.transform, "Siege battle", "DEFEND IN BATTLE", 464, 5, 204, 36,
            () => tower.LaunchSiegeBattle(), Alert, 14);
        siegePanel.gameObject.SetActive(false);
    }

    private void RefreshSiegeBanner(TowerState state)
    {
        var rules = tower.Rules;
        bool show = state.introPhase == "complete" && rules.SiegeComing && !state.siegeBattle && !state.defeated;
        siegePanel.gameObject.SetActive(show);
        if (!show) return;
        int heroes = rules.SiegeFighters().Count;
        if (rules.IsLair)
        {
            siegeText.text = "<color=#ff8a70>ELITE TEAM IN " + TowerRules.Clock(state.siegeWarning) + "</color>   notoriety " +
                Mathf.RoundToInt(state.lair.notoriety) + (heroes == 0 ? "\n<size=12>No battle-ready hero: traps and monsters must hold.</size>" :
                    "\n<size=12>Meet them with " + heroes + " hero" + (heroes == 1 ? "" : "es") + ", or let the dungeon do it.</size>");
            siegeFight.interactable = heroes > 0;
            return;
        }
        siegeText.text = "<color=#ff8a70>SIEGE IN " + TowerRules.Clock(state.siegeWarning) + "</color>   wave " +
            Mathf.RoundToInt(rules.SiegeWave()) + " vs defence " + Mathf.RoundToInt(rules.SiegeDefence()) +
            (heroes == 0 ? "\n<size=12>No battle-ready hero at home: the guards hold alone.</size>" :
                "\n<size=12>Lead " + heroes + " hero" + (heroes == 1 ? "" : "es") + " out, or let the guards hold.</size>");
        siegeFight.interactable = heroes > 0;
    }
}
