using System.Collections.Generic;
using UnityEngine;

// Immediate-mode drawing kit for the battle screen: procedural glow/vignette textures, rounded
// panels, outlined text and bars, and alpha-trimmed sprites. Everything draws in the 1600x900
// virtual space BattleMode sets up through GUI.matrix.
public static class BattleGui
{
    public struct Cutout
    {
        public Texture2D Tex;
        public Rect Uv;          // trimmed content, in texture UV space
        public float Aspect;     // width / height of the trimmed content
        public bool Valid { get { return Tex != null; } }
    }

    public static Texture2D Glow, RingTex, Streak, GradTop, Vignette;
    public static Texture2D SlashSheet, BurstSheet, Rune, Rays, CardFrame, GemTex, Sparkle;
    // Painted 8-frame sheets from Tools/produce_battle_motion.py (MiniMax H3); null when not generated yet.
    public static Texture2D GenSlash, GenImpact, GenFrost, GenFire, GenLightning, GenLight, GenWarp;
    public static Font Display;
    public static Cutout CommandSkin, CommandDiscSkin;
    private static bool built;
    private static readonly Dictionary<string, GUIStyle> styles = new Dictionary<string, GUIStyle>();
    private static readonly Dictionary<Texture2D, Cutout> trims = new Dictionary<Texture2D, Cutout>();

    public static void Build()
    {
        if (built) return;
        built = true;
        Glow = Make(64, 64, (u, v) => { float d = Mathf.Sqrt(u * u + v * v); float a = Mathf.Clamp01(1f - d); return new Color(1, 1, 1, a * a * (0.6f + 0.4f * a)); });
        RingTex = Make(128, 128, (u, v) =>
        {
            float d = Mathf.Sqrt(u * u + v * v); float x = (d - 0.78f) / 0.10f;
            return new Color(1, 1, 1, Mathf.Exp(-x * x) * (d < 1f ? 1f : 0f));
        });
        Streak = Make(128, 16, (u, v) =>
        {
            float ax = 1f - Mathf.Abs(u); float ay = 1f - Mathf.Abs(v);
            return new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(ax), 1.4f) * Mathf.Pow(Mathf.Clamp01(ay), 2.4f));
        });
        GradTop = Make(2, 64, (u, v) => new Color(1, 1, 1, Mathf.Clamp01(v * 0.5f + 0.5f)));
        Vignette = Make(128, 72, (u, v) =>
        {
            float d = Mathf.Sqrt(u * u * 0.85f + v * v * 1.0f);
            float t = Mathf.Clamp01((d - 0.55f) / 0.75f);
            return new Color(0, 0, 0, t * t * (3f - 2f * t) * 0.72f);
        });
        SlashSheet = Resources.Load<Texture2D>("AdamsHaven/Fx/fx_slash_sheet");
        BurstSheet = Resources.Load<Texture2D>("AdamsHaven/Fx/fx_burst_sheet");
        Rune = Resources.Load<Texture2D>("AdamsHaven/Fx/fx_rune");
        Rays = Resources.Load<Texture2D>("AdamsHaven/Fx/fx_rays");
        CardFrame = Resources.Load<Texture2D>("AdamsHaven/Fx/ui_card_frame");
        GemTex = Resources.Load<Texture2D>("AdamsHaven/Fx/ui_gem");
        Sparkle = Resources.Load<Texture2D>("AdamsHaven/Fx/fx_sparkle");
        GenSlash = Resources.Load<Texture2D>("AdamsHaven/Fx/Gen/fx_slash_sheet");
        GenImpact = Resources.Load<Texture2D>("AdamsHaven/Fx/Gen/fx_impact_sheet");
        GenFrost = Resources.Load<Texture2D>("AdamsHaven/Fx/Gen/fx_frost_sheet");
        GenFire = Resources.Load<Texture2D>("AdamsHaven/Fx/Gen/fx_fire_sheet");
        GenLightning = Resources.Load<Texture2D>("AdamsHaven/Fx/Gen/fx_lightning_sheet");
        GenLight = Resources.Load<Texture2D>("AdamsHaven/Fx/Gen/fx_light_sheet");
        GenWarp = Resources.Load<Texture2D>("AdamsHaven/Fx/Gen/fx_warp_sheet");
        CommandSkin = Trim(Resources.Load<Texture2D>("AdamsHaven/Fx/ui_command_plate"));
        CommandDiscSkin = Trim(Resources.Load<Texture2D>("AdamsHaven/Fx/ui_command_disc"));
        Display = null;
        try
        {
            RuntimePlatform p = Application.platform;
            bool desktop = p == RuntimePlatform.WindowsEditor || p == RuntimePlatform.WindowsPlayer
                || p == RuntimePlatform.OSXEditor || p == RuntimePlatform.OSXPlayer;
            if (desktop) Display = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Segoe UI Semibold", "Arial" }, 32);
        }
        catch { Display = null; }
    }

    // Nine-slice the generated panel so its bevel remains crisp on both small and wide controls.
    public static void CommandSurface(Rect r, Color accent, bool enabled, bool hover, bool active, float fill = -1f)
    {
        DrawGlow(new Rect(r.x - 5f, r.y + 3f, r.width + 10f, r.height + 9f), new Color(0, 0, 0, .7f));
        if (enabled && (hover || active))
            DrawGlow(new Rect(r.x - 10f, r.y - 7f, r.width + 20f, r.height + 14f), Alpha(accent, hover ? .32f : .18f));
        Color saved = GUI.color;
        GUI.color = enabled ? (hover ? new Color(1.15f, 1.15f, 1.15f) : Color.white) : new Color(.48f, .52f, .58f);
        if (CommandSkin.Valid)
        {
            Rect uv = CommandSkin.Uv;
            float bx = Mathf.Min(r.height * .44f, r.width * .22f), by = Mathf.Min(r.height * .24f, 12f);
            float[] xs = { r.x, r.x + bx, r.xMax - bx, r.xMax };
            float[] ys = { r.y, r.y + by, r.yMax - by, r.yMax };
            float[] us = { uv.x, uv.x + uv.width * .10f, uv.xMax - uv.width * .10f, uv.xMax };
            float[] vs = { uv.yMax, uv.yMax - uv.height * .23f, uv.y + uv.height * .23f, uv.y };
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                    GUI.DrawTextureWithTexCoords(new Rect(xs[col], ys[row], xs[col + 1] - xs[col], ys[row + 1] - ys[row]),
                        CommandSkin.Tex, new Rect(us[col], vs[row + 1], us[col + 1] - us[col], vs[row] - vs[row + 1]), true);
        }
        else Round(r, new Color(.07f, .11f, .16f), 4f);
        GUI.color = saved;
        if (enabled && active) Fill(new Rect(r.x + 7f, r.y + 6f, r.width - 14f, r.height - 12f), Alpha(accent, .16f));
        float amount = fill >= 0 ? Mathf.Clamp01(fill) : active || hover ? 1f : .24f;
        Fill(new Rect(r.x + 8f, r.yMax - 4f, (r.width - 16f) * amount, 2f), Alpha(accent, enabled ? .95f : .25f));
    }

    // fn receives coordinates in -1..1 across the texture, y up.
    private static Texture2D Make(int w, int h, System.Func<float, float, Color> fn)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear; tex.hideFlags = HideFlags.HideAndDontSave;
        Color32[] px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = fn((x + 0.5f) / w * 2f - 1f, (y + 0.5f) / h * 2f - 1f);
        tex.SetPixels32(px); tex.Apply(false, true);
        return tex;
    }

    // ---- sprites ---------------------------------------------------------------------------

    public static Cutout Trim(Texture2D tex)
    {
        Cutout sprite;
        if (tex == null) return new Cutout();
        if (trims.TryGetValue(tex, out sprite)) return sprite;
        sprite = new Cutout { Tex = tex, Uv = new Rect(0, 0, 1, 1), Aspect = (float)tex.width / Mathf.Max(1, tex.height) };
        RenderTexture previous = RenderTexture.active;
        RenderTexture rt = null;
        try
        {
            const int n = 256;
            rt = RenderTexture.GetTemporary(n, n, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            Texture2D read = new Texture2D(n, n, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, n, n), 0, 0); read.Apply();
            Color32[] px = read.GetPixels32();
            Object.Destroy(read);
            int x0 = n, x1 = -1, y0 = n, y1 = -1;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    if (px[y * n + x].a > 28) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
            if (x1 > x0 && y1 > y0)
            {
                Rect uv = new Rect(x0 / (float)n, y0 / (float)n, (x1 - x0 + 1) / (float)n, (y1 - y0 + 1) / (float)n);
                sprite.Uv = uv;
                sprite.Aspect = uv.width * tex.width / Mathf.Max(1f, uv.height * tex.height);
            }
        }
        catch { }
        finally
        {
            RenderTexture.active = previous;
            if (rt != null) RenderTexture.ReleaseTemporary(rt);
        }
        trims[tex] = sprite;
        return sprite;
    }

    public static void DrawSprite(Rect rect, Cutout sprite)
    {
        if (sprite.Valid) GUI.DrawTextureWithTexCoords(rect, sprite.Tex, sprite.Uv, true);
    }

    // A square head-and-shoulders crop of a standing character.
    public static void DrawPortrait(Rect rect, Cutout sprite, float coverage = 0.46f)
    {
        if (!sprite.Valid) return;
        float texH = sprite.Uv.height * sprite.Tex.height;
        float side = texH * coverage;
        float w = side / sprite.Tex.width, h = side / sprite.Tex.height;
        float cx = sprite.Uv.center.x;
        Rect uv = new Rect(cx - w * 0.5f, sprite.Uv.yMax - h, w, h);
        GUI.DrawTextureWithTexCoords(rect, sprite.Tex, uv, true);
    }

    // ---- shapes ----------------------------------------------------------------------------

    public static void Fill(Rect r, Color c)
    {
        Color before = GUI.color; GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = before;
    }

    public static void Round(Rect r, Color c, float radius)
    {
        radius = Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f);
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, Vector4.zero, new Vector4(radius, radius, radius, radius));
    }

    public static void Outline(Rect r, Color c, float width, float radius)
    {
        radius = Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f);
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c,
            new Vector4(width, width, width, width), new Vector4(radius, radius, radius, radius));
    }

    public static void Disc(Vector2 center, float radius, Color c)
    {
        Round(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), c, radius);
    }

    public static void Ring(Vector2 center, float radius, float width, Color c)
    {
        Outline(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), c, width, radius);
    }

    public static void DrawGlow(Vector2 center, float radius, Color c)
    {
        if (c.a <= 0.004f) return;
        Color before = GUI.color; GUI.color = c;
        GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), Glow);
        GUI.color = before;
    }

    public static void DrawGlow(Rect r, Color c)
    {
        if (c.a <= 0.004f) return;
        Color before = GUI.color; GUI.color = c;
        GUI.DrawTexture(r, Glow);
        GUI.color = before;
    }

    // One frame of a horizontal sprite sheet, centred on a point and rotated.
    public static void DrawSheet(Texture2D sheet, int frames, int frame, Vector2 center, float size, float angle, Color c)
    {
        if (sheet == null || c.a <= 0.004f) return;
        frame = Mathf.Clamp(frame, 0, frames - 1);
        Matrix4x4 old = GUI.matrix;
        RotateAround(angle, center);
        Color before = GUI.color; GUI.color = c;
        GUI.DrawTextureWithTexCoords(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), sheet,
            new Rect(frame / (float)frames, 0f, 1f / frames, 1f), true);
        GUI.color = before;
        GUI.matrix = old;
    }

    // The painted element burst for a hit, or null when that element has none.
    public static Texture2D ElementSheet(BattleElement element)
    {
        switch (element)
        {
            case BattleElement.Water: return GenFrost;
            case BattleElement.Fire: return GenFire;
            case BattleElement.Lightning: return GenLightning;
            case BattleElement.Light: return GenLight;
            default: return null;
        }
    }

    // One frame of a horizontal sprite sheet stretched over a rectangle (full-screen overlays).
    public static void DrawSheetRect(Texture2D sheet, int frames, int frame, Rect r, Color c)
    {
        if (sheet == null || c.a <= 0.004f) return;
        frame = Mathf.Clamp(frame, 0, frames - 1);
        Color before = GUI.color; GUI.color = c;
        GUI.DrawTextureWithTexCoords(r, sheet, new Rect(frame / (float)frames, 0f, 1f / frames, 1f), true);
        GUI.color = before;
    }

    // A circle lying on the floor: spun in its own plane, then squashed to the ground perspective.
    public static void DrawGround(Texture2D tex, Vector2 center, float radius, float spin, float squash, Color c)
    {
        if (tex == null || c.a <= 0.004f) return;
        Matrix4x4 old = GUI.matrix;
        GUI.matrix = old * Matrix4x4.TRS(center, Quaternion.identity, new Vector3(1f, squash, 1f))
            * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, spin), Vector3.one);
        Color before = GUI.color; GUI.color = c;
        GUI.DrawTexture(new Rect(-radius, -radius, radius * 2f, radius * 2f), tex);
        GUI.color = before;
        GUI.matrix = old;
    }

    public static void DrawSpin(Texture2D tex, Vector2 center, float size, float angle, Color c)
    {
        if (tex == null || c.a <= 0.004f) return;
        Matrix4x4 old = GUI.matrix;
        RotateAround(angle, center);
        Color before = GUI.color; GUI.color = c;
        GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), tex);
        GUI.color = before;
        GUI.matrix = old;
    }

    public static void Shockwave(Vector2 center, float radius, Color c)
    {
        if (c.a <= 0.004f) return;
        Color before = GUI.color; GUI.color = c;
        GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), RingTex);
        GUI.color = before;
    }

    // A streak of light of a given length, centred on a point and rotated to an angle in degrees.
    public static void DrawStreak(Vector2 center, float length, float thickness, float angle, Color c)
    {
        if (c.a <= 0.004f) return;
        Matrix4x4 old = GUI.matrix;
        RotateAround(angle, center);
        Color before = GUI.color; GUI.color = c;
        GUI.DrawTexture(new Rect(center.x - length * 0.5f, center.y - thickness * 0.5f, length, thickness), Streak);
        GUI.color = before;
        GUI.matrix = old;
    }

    public static void GradientDown(Rect r, Color c)
    {
        Color before = GUI.color; GUI.color = c;
        GUI.DrawTexture(r, GradTop);
        GUI.color = before;
    }

    public static void GradientUp(Rect r, Color c)
    {
        Color before = GUI.color; GUI.color = c;
        GUI.DrawTextureWithTexCoords(r, GradTop, new Rect(0, 1, 1, -1));
        GUI.color = before;
    }

    // ---- text ------------------------------------------------------------------------------

    private static GUIStyle Style(int size, bool bold, TextAnchor anchor, bool wrap)
    {
        string key = size + (bold ? "b" : "n") + (int)anchor + (wrap ? "w" : "s");
        GUIStyle style;
        if (styles.TryGetValue(key, out style)) return style;
        style = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = anchor, wordWrap = wrap,
            fontStyle = bold ? FontStyle.Bold : FontStyle.Normal, clipping = TextClipping.Overflow, richText = false };
        style.padding = new RectOffset(0, 0, 0, 0);
        style.margin = new RectOffset(0, 0, 0, 0);
        if (Display != null) style.font = Display;
        styles[key] = style;
        return style;
    }

    public static void Text(Rect r, string value, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft,
        bool bold = false, bool wrap = false, float outline = 0f)
    {
        if (string.IsNullOrEmpty(value)) return;
        GUIStyle style = Style(size, bold, anchor, wrap);
        Color before = GUI.color;
        float alpha = color.a * before.a;
        if (outline > 0f)
        {
            GUI.color = new Color(0.02f, 0.03f, 0.05f, alpha * 0.92f);
            style.normal.textColor = Color.white;
            float o = outline;
            GUI.Label(new Rect(r.x - o, r.y, r.width, r.height), value, style);
            GUI.Label(new Rect(r.x + o, r.y, r.width, r.height), value, style);
            GUI.Label(new Rect(r.x, r.y - o, r.width, r.height), value, style);
            GUI.Label(new Rect(r.x, r.y + o, r.width, r.height), value, style);
            GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), value, style);
            GUI.Label(new Rect(r.x - o, r.y - o, r.width, r.height), value, style);
            GUI.Label(new Rect(r.x + o, r.y - o, r.width, r.height), value, style);
            GUI.Label(new Rect(r.x - o, r.y + o, r.width, r.height), value, style);
        }
        GUI.color = new Color(1, 1, 1, alpha);
        style.normal.textColor = new Color(color.r, color.g, color.b, 1f);
        GUI.Label(r, value, style);
        GUI.color = before;
    }

    // ---- bars ------------------------------------------------------------------------------

    public static void Bar(Rect r, float fill, float ghost, Color color, Color ghostColor)
    {
        float radius = r.height * 0.5f;
        Round(new Rect(r.x - 1.5f, r.y - 1.5f, r.width + 3f, r.height + 3f), new Color(0.01f, 0.015f, 0.03f, 0.92f), radius + 1.5f);
        Round(r, new Color(0.07f, 0.09f, 0.13f, 0.95f), radius);
        fill = Mathf.Clamp01(fill); ghost = Mathf.Clamp01(ghost);
        if (ghost > fill) Round(new Rect(r.x, r.y, Mathf.Max(r.height, r.width * ghost), r.height), ghostColor, radius);
        if (fill > 0.001f)
        {
            Rect f = new Rect(r.x, r.y, Mathf.Max(r.height * 0.9f, r.width * fill), r.height);
            Round(f, color, radius);
            Round(new Rect(f.x + 2f, f.y + 1f, Mathf.Max(0f, f.width - 4f), Mathf.Max(1f, f.height * 0.36f)), new Color(1, 1, 1, 0.24f), radius * 0.5f);
        }
    }

    // GUIUtility.RotateAroundPivot treats its pivot as a screen point, which breaks under the scaled
    // battle matrix. These compose the rotation/scale in the current (virtual) space instead.
    public static void RotateAround(float angle, Vector2 pivot)
    {
        GUI.matrix = GUI.matrix * Matrix4x4.TRS(pivot, Quaternion.Euler(0f, 0f, angle), Vector3.one) * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
    }

    public static void ScaleAround(float scale, Vector2 pivot)
    {
        GUI.matrix = GUI.matrix * Matrix4x4.TRS(pivot, Quaternion.identity, new Vector3(scale, scale, 1f)) * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);
    }

    public static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
    public static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }
    public static Color Alpha(Color c, float a) { return new Color(c.r, c.g, c.b, a); }
}
