using AdamsHaven.Tower;
using UnityEngine;
using UnityEngine.UI;

// TOWER_MODE_GDD 8.2: the Heart hub's Research tab. Five branch columns of eight nodes, a detail panel for the selected
// node (cost, time, effect, why it is locked) and STUDY / RUSH. Locked buildings in the Build menu open it too.
public sealed partial class TowerHud
{
    private Image heartResearchTab;
    private Button tabResearch, researchStart, researchRush;
    private readonly Button[] researchNodes = new Button[40];
    private Text researchDetail;
    private string researchSelected = "CON-1";

    private static readonly Color ResearchDone = new Color(0.55f, 0.45f, 0.18f, 1f);
    private static readonly Color ResearchLocked = new Color(0.2f, 0.23f, 0.27f, 1f);
    private static readonly Color ResearchActive = new Color(0.52f, 0.36f, 0.82f, 1f);

    private void BuildResearchTab(Transform heart)
    {
        heartResearchTab = Rect("Research tab", heart, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -58), Color.clear);
        heartResearchTab.raycastTarget = false;
        var r = heartResearchTab.transform;
        for (int b = 0; b < TowerRules.Branches.Length; b++)
            TextAt(r, "Branch " + b, TowerRules.BranchNames[b].ToUpperInvariant(), 12 + b * 140, 0, 136, 20, 12, Gold,
                TextAnchor.MiddleCenter);
        for (int i = 0; i < TowerRules.ResearchTree.Length; i++)
        {
            var def = TowerRules.ResearchTree[i];
            int column = System.Array.IndexOf(TowerRules.Branches, def.branch);
            string id = def.id;
            researchNodes[i] = ButtonAt(r, "Node " + id, "", 12 + column * 140, 22 + (def.index - 1) * 30, 136, 27,
                () => { researchSelected = id; Refresh(); }, Teal, 11);
        }
        researchDetail = TextAt(r, "Research detail", "", 12, 266, 484, 120, 13, Cream, TextAnchor.UpperLeft);
        researchStart = ButtonAt(r, "Research start", "STUDY", 506, 270, 198, 52,
            () => { tower.Apply(tower.Rules.StartResearch(researchSelected)); Refresh(); }, Teal, 16);
        researchRush = ButtonAt(r, "Research rush", "RUSH", 506, 328, 198, 52,
            () => { tower.Apply(tower.Rules.RushResearch()); Refresh(); }, Alert, 15);
    }

    // Opens the Heart straight on a node, e.g. from a locked building in the Build menu.
    private void OpenResearch(string nodeId)
    {
        if (!string.IsNullOrEmpty(nodeId)) researchSelected = nodeId;
        heartTab = "research";
        HidePopups();
        popupHeart.gameObject.SetActive(true);
        popupHeart.transform.SetAsLastSibling();
        Refresh();
    }

    // Guard places at the Gate (a third with DEF-3 Gate guard post).
    private int GateSlots()
    {
        var gate = tower.Rules.State.rooms.Find(r => r.type == "gate");
        return gate == null ? 2 : tower.Rules.Capacity(gate);
    }

    private static string Duration(long seconds)
    {
        if (seconds >= 3600) return (seconds / 3600) + "h " + (seconds % 3600 / 60).ToString("00") + "m";
        if (seconds >= 60) return (seconds / 60) + "m " + (seconds % 60).ToString("00") + "s";
        return seconds + "s";
    }

    private bool ResearchAvailable()
    {
        var rules = tower.Rules;
        if (rules.Researching) return false;
        foreach (var def in TowerRules.ResearchTree) if (rules.CanResearch(def.id) == null) return true;
        return false;
    }

    private void RefreshResearch(TowerState state)
    {
        var rules = tower.Rules;
        for (int i = 0; i < TowerRules.ResearchTree.Length; i++)
        {
            var def = TowerRules.ResearchTree[i];
            bool done = rules.Researched(def.id), active = state.researching == def.id;
            bool open = !done && !active && rules.CanResearch(def.id) == null;
            string label = (def.id == researchSelected ? "> " : "") + def.index + "  " + def.name;
            if (active) label += "  " + Mathf.FloorToInt(rules.ResearchProgress() * 100) + "%";
            // The skinned button art mutes the tint, so the label colour carries the state too.
            var nodeLabel = LabelOf(researchNodes[i]);
            nodeLabel.text = label;
            nodeLabel.color = done ? new Color(1f, 0.84f, 0.4f) : active ? new Color(0.85f, 0.72f, 1f) :
                open ? new Color(0.62f, 1f, 0.9f) : new Color(0.5f, 0.54f, 0.6f);
            researchNodes[i].GetComponent<Image>().color = done ? ResearchDone : active ? ResearchActive :
                open ? Teal : ResearchLocked;
        }

        var node = TowerRules.ResearchDef(researchSelected) ?? TowerRules.ResearchTree[0];
        int gold = TowerRules.ResearchGold(node);
        string text = "<b>" + node.id + "  " + node.name + "</b>   (tier " + node.Tier + ", " +
            TowerRules.BranchNames[System.Array.IndexOf(TowerRules.Branches, node.branch)] + ")\n" + node.effect +
            "\nCost: " + TowerRules.ResearchCelestium(node) + " Celestium" + (gold > 0 ? " + " + TowerRules.Compact(gold) + " gold" : "") +
            "   •   Time: " + Duration(TowerRules.ResearchSeconds(node)) + " (real time, keeps going while the game is closed)";
        if (rules.Researched(node.id)) text += "\n<color=#ffd45c>Known.</color>";
        else if (state.researching == node.id)
            text += "\n<color=#c9a8ff>Studying: " + Mathf.FloorToInt(rules.ResearchProgress() * 100) + "%, " +
                Duration(rules.ResearchRemaining()) + " left.</color>";
        else
        {
            string why = rules.CanResearch(node.id);
            text += why == null ? "\n<color=#8fe39a>Ready to study.</color>" : "\n<color=#ff9c8a>" + why + "</color>";
        }
        if (rules.Researching && state.researching != node.id)
            text += "\nNow studying " + TowerRules.ResearchDef(state.researching).name + ": " +
                Duration(rules.ResearchRemaining()) + " left.";
        researchDetail.text = text;

        bool canStart = rules.CanResearch(node.id) == null;
        researchStart.interactable = canStart;
        LabelOf(researchStart).text = rules.Researched(node.id) ? "KNOWN" : state.researching == node.id ? "STUDYING" :
            canStart ? "STUDY" : "LOCKED";
        LabelOf(researchStart).color = canStart ? Cream : new Color(0.5f, 0.54f, 0.6f);
        int rush = rules.RushTonicCost();
        researchRush.gameObject.SetActive(rules.Researching);
        researchRush.interactable = rules.Researching && state.tonics >= rush;
        LabelOf(researchRush).text = "RUSH  " + rush + " TONIC" + (rush == 1 ? "" : "S");
    }
}
