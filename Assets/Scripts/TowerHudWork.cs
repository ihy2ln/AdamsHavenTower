using System.Collections.Generic;
using AdamsHaven.Tower;
using UnityEngine;
using UnityEngine.UI;

// RimWorld's Work tab (TT 10.30.0): every adult against the six jobs. Tap a cell to cycle its priority
// 0-3 (3 comes first, 0 never). A backstory can bar one job; that cell shows a × and stays locked.
public sealed partial class TowerHud
{
    private const int WorkRows = 8;
    private Image popupWork;
    private Text workPageText;
    private readonly Text[] workNames = new Text[WorkRows];
    private readonly Button[,] workCells = new Button[WorkRows, 6];
    private int workPage;

    private void BuildWorkPopup()
    {
        popupWork = Box("Work grid", safeRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10),
            new Vector2(720, 480), Glass);
        TowerUiSkin.ApplyPanel(popupWork, Glass, true);
        var t = popupWork.transform;
        TextAt(t, "Work title", "WORK PRIORITIES", 16, 10, 260, 30, 19, Gold);
        TextAt(t, "Work hint", "tap to cycle  •  3 first, 1 last, – never", 280, 12, 390, 26, 13, Cream);
        CloseButton(t, 680, 12, CloseAllPopups);
        Divider(t, 12, 46, 696);
        for (int j = 0; j < priorityLabelsShort.Length; j++)
            TextAt(t, "Work column " + j, priorityLabelsShort[j], 222 + j * 80, 50, 76, 24, 12, Gold, TextAnchor.MiddleCenter);
        for (int i = 0; i < WorkRows; i++)
        {
            int row = i;
            workNames[i] = TextAt(t, "Work name " + i, "", 16, 78 + i * 44, 200, 40, 13, Cream);
            for (int j = 0; j < priorityNames.Length; j++)
            {
                int column = j;
                workCells[i, j] = ButtonAt(t, "Work cell " + i + "," + j, "", 222 + j * 80, 80 + i * 44, 76, 38,
                    () => CycleWorkCell(row, column), Teal, 17);
            }
        }
        ButtonAt(t, "Work previous", "‹", 16, 436, 56, 34, () => { workPage = Mathf.Max(0, workPage - 1); Refresh(); }, Teal, 22);
        workPageText = TextAt(t, "Work page", "", 80, 436, 560, 34, 14, Cream, TextAnchor.MiddleCenter);
        ButtonAt(t, "Work next", "›", 648, 436, 56, 34, () => { workPage++; Refresh(); }, Teal, 22);
        popupWork.gameObject.SetActive(false);
    }

    private void OpenWorkGrid() { TogglePopup(popupWork); }

    private List<TowerResident> WorkPeople()
    {
        return tower.Rules.State.residents.FindAll(r => r.ageStage == 0);
    }

    private void CycleWorkCell(int row, int column)
    {
        var people = WorkPeople();
        int index = workPage * WorkRows + row;
        if (index >= people.Count) return;
        var person = people[index];
        int current = Priority(person, priorityNames[column]);
        tower.Apply(tower.Rules.SetPriority(person.id, priorityNames[column], (current + 1) % 4));
    }

    private void RefreshWork()
    {
        var people = WorkPeople();
        int pages = Mathf.Max(1, Mathf.CeilToInt(people.Count / (float)WorkRows));
        workPage = Mathf.Clamp(workPage, 0, pages - 1);
        workPageText.text = people.Count + " adults  •  page " + (workPage + 1) + " of " + pages;
        for (int i = 0; i < WorkRows; i++)
        {
            int index = workPage * WorkRows + i;
            bool has = index < people.Count;
            workNames[i].gameObject.SetActive(has);
            for (int j = 0; j < priorityNames.Length; j++) workCells[i, j].gameObject.SetActive(has);
            if (!has) continue;
            var person = people[index];
            string away = person.exploring || person.away ? "  <color=#9aa3ad>(away)</color>" : person.downed ? "  <color=#ff7060>(down)</color>" : "";
            workNames[i].text = person.name + away + "\n<color=#9aa3ad>" +
                (string.IsNullOrEmpty(person.trait) ? "" : person.trait) +
                (string.IsNullOrEmpty(person.trait2) ? "" : ", " + person.trait2) + "</color>";
            string barred = TowerRules.Incapable(person);
            for (int j = 0; j < priorityNames.Length; j++)
            {
                var cell = workCells[i, j];
                int value = Priority(person, priorityNames[j]);
                bool locked = barred == priorityNames[j];
                cell.interactable = !locked;
                LabelOf(cell).text = locked ? "×" : value == 0 ? "–" : value.ToString();
                cell.GetComponent<Image>().color = locked ? Muted : value == 3 ? Gold : value == 2 ? Teal : value == 1 ? Violet : Muted;
            }
        }
    }
}
