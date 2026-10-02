using UnityEngine;
using UnityEngine.UI;

namespace AdamsHaven.Tower
{
    // Rain over the expedition map: a tiling texture of slanted streaks, drawn once, scrolled down-left every frame.
    [RequireComponent(typeof(RawImage))]
    public sealed class TowerRainLayer : MonoBehaviour
    {
        private const int Size = 128;
        private static Texture2D streaks;
        private RawImage image;
        private float scroll;

        private static Texture2D Streaks
        {
            get
            {
                if (streaks != null) return streaks;
                streaks = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
                var px = new Color32[Size * Size];
                var rng = new System.Random(4021);
                for (int d = 0; d < 46; d++)
                {
                    int x0 = rng.Next(Size), y0 = rng.Next(Size), len = 7 + rng.Next(10);
                    byte alpha = (byte)(70 + rng.Next(90));
                    // Slanted one pixel left for every four down, wrapping so the tile repeats cleanly.
                    for (int i = 0; i < len; i++)
                    {
                        int x = ((x0 - i / 4) % Size + Size) % Size, y = ((y0 - i) % Size + Size) % Size;
                        px[y * Size + x] = new Color32(210, 225, 240, (byte)(alpha * (1f - i / (float)len * 0.6f)));
                    }
                }
                streaks.SetPixels32(px); streaks.Apply();
                return streaks;
            }
        }

        private void Awake()
        {
            image = GetComponent<RawImage>();
            image.texture = Streaks;
            image.color = new Color(1, 1, 1, 0.55f);
        }

        private void Update()
        {
            var rect = ((RectTransform)transform).rect;
            scroll += Time.unscaledDeltaTime * 1.6f;
            float tilesX = Mathf.Max(1, rect.width / 160f), tilesY = Mathf.Max(1, rect.height / 160f);
            image.uvRect = new Rect(scroll * 0.25f, scroll, tilesX, tilesY);
        }
    }
}
