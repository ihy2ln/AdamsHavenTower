using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Shrink-to-fit for the runtime HUD (TT 10.4.5, mobile compact pass). Panels are laid out with absolute positions;
// Fit stacks the visible rows again on every refresh: hidden rows leave no gap, body text takes only the lines it
// needs, and the panel ends just under its last visible row. Only direct children anchored top-left take part (skin
// frames and stretched art keep their anchors). Each child remembers its authored place, so a row that comes back
// returns to its slot.
public static class TowerUiFit
{
    private struct Home
    {
        public Vector2 position;
        public float height;
    }

    private static readonly Dictionary<RectTransform, Home> homes = new Dictionary<RectTransform, Home>();

    private struct Item
    {
        public RectTransform rect;
        public float top, bottom;
    }

    // Texts at least this tall in the layout are body text: they shrink or grow to their content.
    private const float BodyTextHeight = 40f;

    // Lays the panel out again and sets its height. maxGap caps the space between two rows, so a hidden row leaves at
    // most that much; minHeight keeps a floor for panels with a fixed-size background.
    public static float Fit(RectTransform panel, float pad = 10f, float maxGap = 6f, float minHeight = 0f)
    {
        if (panel == null) return 0;
        var items = new List<Item>();
        for (int i = 0; i < panel.childCount; i++)
        {
            var rect = panel.GetChild(i) as RectTransform;
            if (rect == null || rect.anchorMin != new Vector2(0, 1) || rect.anchorMax != new Vector2(0, 1)) continue;
            Home home;
            if (!homes.TryGetValue(rect, out home))
            {
                if (homes.Count > 4000) homes.Clear();   // a scene reload left the old HUD's entries behind
                home = new Home { position = rect.anchoredPosition, height = rect.sizeDelta.y };
                homes[rect] = home;
            }
            rect.anchoredPosition = home.position;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, home.height);
            if (!rect.gameObject.activeSelf) continue;
            var text = rect.GetComponent<Text>();
            if (text != null && !text.enabled) continue;
            if (text != null && home.height >= BodyTextHeight)
            {
                float wanted = string.IsNullOrEmpty(text.text) ? 0 : Mathf.Ceil(LayoutUtility.GetPreferredHeight(rect)) + 2;
                // Keep the authored top: grow or shrink downwards from it.
                float topEdge = rect.anchoredPosition.y + (1 - rect.pivot.y) * home.height;
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, wanted);
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, topEdge - (1 - rect.pivot.y) * wanted);
                if (wanted <= 0) continue;
            }
            float top = -(rect.anchoredPosition.y + (1 - rect.pivot.y) * rect.sizeDelta.y);
            items.Add(new Item { rect = rect, top = top, bottom = top + rect.sizeDelta.y });
        }
        items.Sort((a, b) => a.top.CompareTo(b.top));
        float cursor = 0, rowTop = 0, rowBottom = 0, shift = 0, lastBottom = 0;
        bool first = true;
        int start = 0;
        for (int i = 0; i <= items.Count; i++)
        {
            // A row is a run of items that overlap vertically.
            bool newRow = i == items.Count || !first && items[i].top >= rowBottom - 0.5f;
            if (newRow && !first)
            {
                float gap = rowTop - cursor;
                float placed = start == 0 ? rowTop : cursor + Mathf.Clamp(gap, 0, maxGap);
                shift = rowTop - placed;
                for (int k = start; k < i; k++)
                    items[k].rect.anchoredPosition += new Vector2(0, shift);
                lastBottom = rowBottom - shift;
                cursor = lastBottom;
                start = i;
                first = true;
            }
            if (i == items.Count) break;
            if (first) { rowTop = items[i].top; rowBottom = items[i].bottom; first = false; }
            else rowBottom = Mathf.Max(rowBottom, items[i].bottom);
        }
        float height = Mathf.Max(minHeight, lastBottom + pad);
        panel.sizeDelta = new Vector2(panel.sizeDelta.x, height);
        return height;
    }
}
