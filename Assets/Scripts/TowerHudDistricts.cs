using AdamsHaven.Tower;
using UnityEngine;
using UnityEngine.UI;

// Cities: Skylines-style zoning for the Tower (TT 10.30.0): zone a band of floors as a district, give it a
// specialisation, enact policies, and switch the cutaway between info views (districts, coverage, appeal).
public sealed partial class TowerHud
{
    private Image popupDistricts;
    private Text districtTitle, districtFloor, districtInfo, districtFooter;
    private Button districtZone, districtSpec, districtUp, districtDown, districtTrimTop, districtTrimBottom, districtUnzone;
    private readonly Button[] overlayButtons = new Button[4];
    private readonly string[] overlayIds = { "off", "districts", "coverage", "appeal" };
    private readonly string[] overlayNames = { "VIEW OFF", "DISTRICTS", "COVERAGE", "APPEAL" };
    private readonly Button[] policyButtons = new Button[8];

    private void BuildDistrictsPopup()
    {
        popupDistricts = Box("Districts", safeRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10),
            new Vector2(720, 480), Glass);
        TowerUiSkin.ApplyPanel(popupDistricts, Glass, true);
        var t = popupDistricts.transform;
        districtTitle = TextAt(t, "Districts title", "DISTRICTS", 16, 10, 640, 30, 19, Gold);
        CloseButton(t, 680, 12, CloseAllPopups);
        Divider(t, 12, 46, 696);
        for (int i = 0; i < overlayButtons.Length; i++)
        {
            int index = i;
            overlayButtons[i] = ButtonAt(t, "Overlay " + overlayIds[i], overlayNames[i], 16 + i * 174, 54, 166, 34,
                () => { TowerArtDirector.Overlay = overlayIds[index]; Refresh(); }, Teal, 13);
        }
        ButtonAt(t, "District floor down", "FLOOR −", 16, 98, 110, 40, () => tower.FocusOnFloor(tower.FocusFloor - 1), Teal, 14);
        districtFloor = TextAt(t, "District floor", "", 132, 98, 456, 40, 15, Cream, TextAnchor.MiddleCenter);
        ButtonAt(t, "District floor up", "FLOOR +", 594, 98, 110, 40, () => tower.FocusOnFloor(tower.FocusFloor + 1), Teal, 14);
        districtInfo = TextAt(t, "District info", "", 16, 144, 688, 46, 13, Cream, TextAnchor.UpperLeft);

        districtZone = ButtonAt(t, "Zone district", "ZONE A DISTRICT ON THIS FLOOR", 16, 196, 688, 44,
            () => tower.Apply(tower.Rules.CreateDistrict(tower.FocusFloor)), Teal, 15);
        districtSpec = ButtonAt(t, "District spec", "SPECIALISATION", 16, 196, 340, 40, () => DistrictAction(d => tower.Rules.CycleDistrictSpec(d.id)), Violet, 13);
        districtUnzone = ButtonAt(t, "Unzone", "UNZONE", 364, 196, 340, 40, () => DistrictAction(d => tower.Rules.DissolveDistrict(d.id)), Alert, 13);
        districtUp = ButtonAt(t, "District extend up", "+ FLOOR ABOVE", 16, 242, 166, 36,
            () => DistrictAction(d => tower.Rules.ResizeDistrict(d.id, d.floorMin, d.floorMax + 1)), Teal, 12);
        districtDown = ButtonAt(t, "District extend down", "+ FLOOR BELOW", 190, 242, 166, 36,
            () => DistrictAction(d => tower.Rules.ResizeDistrict(d.id, d.floorMin - 1, d.floorMax)), Teal, 12);
        districtTrimTop = ButtonAt(t, "District trim top", "− TOP FLOOR", 364, 242, 166, 36,
            () => DistrictAction(d => tower.Rules.ResizeDistrict(d.id, d.floorMin, d.floorMax - 1)), Teal, 12);
        districtTrimBottom = ButtonAt(t, "District trim bottom", "− BOTTOM FLOOR", 538, 242, 166, 36,
            () => DistrictAction(d => tower.Rules.ResizeDistrict(d.id, d.floorMin + 1, d.floorMax)), Teal, 12);
        TextAt(t, "Policies title", "POLICIES", 16, 284, 300, 24, 14, Gold);
        for (int i = 0; i < policyButtons.Length; i++)
        {
            int index = i;
            policyButtons[i] = ButtonAt(t, "Policy " + TowerRules.Policies[i].id, "", 16 + (i % 4) * 174, 310 + (i / 4) * 54,
                166, 48, () => DistrictAction(d => tower.Rules.TogglePolicy(d.id, TowerRules.Policies[index].id)), Teal, 12);
        }
        districtFooter = TextAt(t, "District footer", "", 16, 420, 688, 50, 13, Cream, TextAnchor.UpperLeft);
        popupDistricts.gameObject.SetActive(false);
    }

    private void OpenDistricts()
    {
        if (TowerArtDirector.Overlay == "off") TowerArtDirector.Overlay = "districts";
        TogglePopup(popupDistricts);
    }

    private void DistrictAction(System.Func<TowerDistrict, string> action)
    {
        var district = tower.Rules.DistrictAt(tower.FocusFloor);
        if (district != null) tower.Apply(action(district));
    }

    private static string CoverageText(int mask)
    {
        string text = ((mask & TowerRules.MedicalService) != 0 ? "medical  " : "") +
            ((mask & TowerRules.SafetyService) != 0 ? "safety  " : "") +
            ((mask & TowerRules.FoodService) != 0 ? "food  " : "") +
            ((mask & TowerRules.LeisureService) != 0 ? "leisure" : "");
        return text.Length == 0 ? "no services reach this floor" : text.Trim();
    }

    private void RefreshDistricts(TowerState state)
    {
        var rules = tower.Rules;
        int focus = tower.FocusFloor;
        int cap = TowerRules.MaxDistricts(state.heartRank);
        districtTitle.text = "DISTRICTS   " + state.districts.Count + " / " + cap +
            (cap == 0 ? "   (the Heart allows the first at rank E)" : "");
        for (int i = 0; i < overlayButtons.Length; i++)
            overlayButtons[i].GetComponent<Image>().color = TowerArtDirector.Overlay == overlayIds[i] ? Gold : Teal;
        var district = rules.DistrictAt(focus);
        bool open = rules.Floor(focus) != null;
        districtFloor.text = "FLOOR " + (focus >= 0 ? "+" : "") + focus + (open ? "" : "  (not open)") +
            (district == null ? "   •   unzoned" : "   •   " + district.name.ToUpperInvariant() + "  " +
                (district.floorMin == district.floorMax ? "floor " + district.floorMin :
                    "floors " + district.floorMin + " to " + district.floorMax));
        districtInfo.text = "Appeal " + Mathf.RoundToInt(rules.Appeal(focus)) + "   •   Coverage: " +
            CoverageText(rules.CoverageMask(focus)) +
            "\nMug, Gate guards, Kitchens and Markets serve nearby floors; well-kept, high-rank and decorative rooms raise appeal.";
        bool zoned = district != null;
        districtZone.gameObject.SetActive(!zoned);
        districtZone.interactable = open && state.districts.Count < cap;
        foreach (var button in new[] { districtSpec, districtUnzone, districtUp, districtDown, districtTrimTop, districtTrimBottom })
            button.gameObject.SetActive(zoned);
        for (int i = 0; i < policyButtons.Length; i++)
        {
            var def = TowerRules.Policies[i];
            bool on = zoned && district.policies.Contains(def.id);
            policyButtons[i].interactable = zoned;
            policyButtons[i].GetComponent<Image>().color = on ? (state.policiesLapsed ? Alert : Gold) : Teal;
            LabelOf(policyButtons[i]).text = def.name.ToUpperInvariant() + "\n" + (def.upkeep > 0 ? def.upkeep + "g / day" : "free");
        }
        int upkeep = rules.PolicyUpkeepPerDay();
        if (!zoned)
        {
            districtFooter.text = "Zone floors into districts to specialise them and enact policies.  Upkeep " + upkeep + "g / day." +
                (state.policiesLapsed ? "  <color=#ff7060>Policies have lapsed: the treasury is empty.</color>" : "");
            return;
        }
        var spec = TowerRules.SpecDef(district.spec);
        LabelOf(districtSpec).text = spec == null ? "SPECIALISATION: NONE  (tap)" :
            spec.name.ToUpperInvariant() + "  " + Mathf.RoundToInt(rules.DistrictPurity(district) * 100) + "% pure  (tap)";
        districtUp.interactable = rules.Floor(district.floorMax + 1) != null;
        districtDown.interactable = rules.Floor(district.floorMin - 1) != null;
        districtTrimTop.interactable = districtTrimBottom.interactable = district.floorMax > district.floorMin;
        string policies = "";
        foreach (string id in district.policies) policies += (policies.Length > 0 ? "  •  " : "") + TowerRules.PolicyDef(id).effect;
        districtFooter.text = (spec == null ? "No specialisation." : spec.effect) + "  Total upkeep " + upkeep + "g / day." +
            (state.policiesLapsed ? "  <color=#ff7060>Lapsed: the treasury is empty.</color>" : "") +
            (policies.Length > 0 ? "\n" + policies : "");
    }
}
