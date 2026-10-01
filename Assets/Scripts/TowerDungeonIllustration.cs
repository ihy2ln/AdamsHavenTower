using AdamsHaven.Tower;
using UnityEngine;
using UnityEngine.UI;

// Illustrated room dioramas share the navigation grid; only the presentation has depth.
public sealed class TowerDungeonIllustration : MonoBehaviour
{
    public const string ArtRoot = "AdamsHaven/Expedition/Dungeon/AnimeV2/";
    private Texture2D discovery;
    private Material mistMaterial;

    public static bool Available(string theme)
    { return Resources.Load<Texture2D>(ArtRoot + theme + "_room") != null
        && Resources.Load<Texture2D>(ArtRoot + "corridor") != null
        && Resources.Load<Texture2D>(ArtRoot + "backdrop") != null
        && Resources.Load<Texture2D>(ArtRoot + "mist") != null
        && Shader.Find("UI/SilverbrookDungeonMist") != null; }

    public void Build(TowerDungeon dungeon, float cellSize)
    {
        var parent = GetComponent<RectTransform>();
        float extent = TowerDungeon.Size * cellSize;
        AddImage("Silverwood depth backdrop", parent, Resources.Load<Texture2D>(ArtRoot + "backdrop"),
            new Vector2(extent / 2, -extent / 2), new Vector2(extent + 120, extent + 120), Color.white);
        var passage = Resources.Load<Texture2D>(ArtRoot + "corridor");
        for (int y = 0; y < TowerDungeon.Size; y++) for (int x = 0; x < TowerDungeon.Size; x++)
        {
            int type = dungeon.CellAt(x, y);
            if (type != TowerDungeon.Corridor && type != TowerDungeon.Door) continue;
            var image = AddImage("Carved Celestium walkway", parent, passage,
                new Vector2((x + 0.5f) * cellSize, -(y + 0.5f) * cellSize), Vector2.one * cellSize * 1.08f, Color.white);
            bool horizontal = dungeon.Walkable(x - 1, y) || dungeon.Walkable(x + 1, y);
            bool vertical = dungeon.Walkable(x, y - 1) || dungeon.Walkable(x, y + 1);
            if (horizontal && !vertical) image.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
            if (horizontal && vertical)
            {
                var crossing = AddImage("Walkway junction", parent, passage, image.rectTransform.anchoredPosition,
                    image.rectTransform.sizeDelta, Color.white);
                crossing.rectTransform.localRotation = Quaternion.Euler(0, 0, 90);
            }
        }
        var roomTexture = Resources.Load<Texture2D>(ArtRoot + dungeon.theme + "_room");
        foreach (var room in dungeon.rooms)
        {
            Vector2 center = new Vector2((room.CenterX + 0.5f) * cellSize, -(room.CenterY + 0.5f) * cellSize);
            Vector2 size = new Vector2((room.w + 1.2f) * cellSize, (room.h + 1.2f) * cellSize);
            AddImage("Room cast shadow", parent, roomTexture, center + new Vector2(6, -10), size * 1.025f, new Color(0, 0, 0, 0.55f));
            AddImage("Anime " + dungeon.theme + " chamber", parent, roomTexture, center, size, Color.white);
        }
    }

    public void BuildMist(float cellSize)
    {
        discovery = new Texture2D(TowerDungeon.Size * 8, TowerDungeon.Size * 8, TextureFormat.RGBA32, false, true)
        { name = "Dungeon discovery mask", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var hidden = new Color32[discovery.width * discovery.height];
        for (int i = 0; i < hidden.Length; i++) hidden[i] = new Color32(0, 0, 0, 255);
        discovery.SetPixels32(hidden); discovery.Apply();
        Shader shader = Shader.Find("UI/SilverbrookDungeonMist");
        if (shader == null) { Debug.LogError("Silverbrook dungeon mist shader is missing."); return; }
        mistMaterial = new Material(shader) { name = "Silverbrook discovery mist" };
        mistMaterial.SetTexture("_Discovery", discovery);
        float size = cellSize * TowerDungeon.Size;
        var image = AddImage("Animated Silverbrook discovery mist", GetComponent<RectTransform>(),
            Resources.Load<Texture2D>(ArtRoot + "mist"), new Vector2(size / 2, -size / 2), Vector2.one * size, Color.white);
        image.material = mistMaterial;
    }

    public void Refresh(TowerRules rules)
    {
        if (discovery == null) return;
        var pixels = new Color32[discovery.width * discovery.height];
        // Feather inward; undiscovered cell interiors remain fully opaque.
        for (int py = 0; py < discovery.height; py++) for (int px = 0; px < discovery.width; px++)
        {
            float x = (px + 0.5f) / 8, y = (py + 0.5f) / 8;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            float state = 0;
            if (rules.Revealed(cx, cy))
            {
                float distance = 1;
                for (int oy = -1; oy <= 1; oy++) for (int ox = -1; ox <= 1; ox++)
                {
                    int nx = cx + ox, ny = cy + oy;
                    if (!TowerDungeon.Inside(nx, ny) || rules.Revealed(nx, ny)) continue;
                    float dx = Mathf.Max(nx - x, 0, x - nx - 1);
                    float dy = Mathf.Max(ny - y, 0, y - ny - 1);
                    distance = Mathf.Min(distance, Mathf.Sqrt(dx * dx + dy * dy));
                }
                state = (rules.InSight(cx, cy) ? 1 : 0.5f) * Mathf.SmoothStep(0, 1, distance / 0.6f);
            }
            byte value = (byte)Mathf.RoundToInt(state * 255);
            pixels[(discovery.height - 1 - py) * discovery.width + px] = new Color32(value, value, value, 255);
        }
        discovery.SetPixels32(pixels); discovery.Apply();
    }

    private static RawImage AddImage(string name, RectTransform parent, Texture2D texture, Vector2 center, Vector2 size, Color tint)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        image.transform.SetParent(parent, false); image.texture = texture; image.color = tint; image.raycastTarget = false;
        var rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = center; rect.sizeDelta = size;
        return image;
    }

    private void OnDestroy()
    {
        if (discovery != null) Destroy(discovery);
        if (mistMaterial != null) Destroy(mistMaterial);
    }
}
