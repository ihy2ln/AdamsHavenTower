using System.Collections.Generic;
using System.IO;
using System.Text;
using AdamsHaven.Tower;
using UnityEditor;
using UnityEngine;

// Brings the Codex layer art (MAP_LAYER_ART_PROMPTS.md, sliced by Tools/slice_overworld_sheets.py) into
// Resources/AdamsHaven/Expedition/Overworld, sorted into the folders TowerMapArt reads, with import settings
// that suit each layer. Anything not delivered keeps its procedural placeholder.
public static class TowerOverworldArtImporter
{
    public const string DefaultSource = @"S:\AI\Game\Game Assets\expedition\overworld_v1\sliced";
    private const string Dest = "Assets/Resources/" + TowerMapArt.Root;

    public enum Kind { Tile, Stamp, Prop, Fx }

    [MenuItem("Adams Haven/Import Overworld Art")]
    public static void ImportMenu()
    {
        string src = Directory.Exists(DefaultSource) ? DefaultSource :
            EditorUtility.OpenFolderPanel("Sliced overworld art (the 'sliced' folder)", "", "");
        if (string.IsNullOrEmpty(src)) return;
        string report = Import(src);
        Debug.Log(report);
        EditorUtility.DisplayDialog("Overworld art", report.Length > 1500 ? report.Substring(0, 1500) + "\n..." : report, "OK");
    }

    // Where each sliced file goes. Returns null for files that are not part of the pack.
    public static string Target(string name, out Kind kind)
    {
        kind = Kind.Stamp;
        if (name.StartsWith("ground_") || name.StartsWith("water_")) { kind = Kind.Tile; return "ground/" + name; }
        if (name == "cloud_tile") { kind = Kind.Tile; return "fx/cloud_tile"; }
        if (name.StartsWith("poi_")) { kind = Kind.Prop; return "props/" + name; }
        if (name.StartsWith("atlas_")) { kind = Kind.Prop; return "atlas/" + name; }
        if (name.StartsWith("bridge_")) { kind = Kind.Prop; return "crossings/" + name; }
        int split = name.IndexOf("_sheet__");
        if (split < 0) return null;
        string sheet = name.Substring(0, split);
        int n;
        if (!int.TryParse(name.Substring(split + 8), out n)) return null;
        switch (sheet)
        {
            case "canopy_oak": case "canopy_pine": case "canopy_birch": case "canopy_silverwood": case "canopy_willow":
                return "stamps/" + sheet + "/" + sheet + "_" + n;
            case "canopy_deadwood": return "stamps/canopy_dead/canopy_dead_" + n;
            // Undergrowth sheet: 5 is the reed clump; the rest are bushes, ferns, briars, mushrooms, log and stump.
            case "undergrowth": return n == 5 ? "stamps/reed/reed_" + n : "stamps/bush/bush_" + n;
            case "saplings": return "stamps/sapling/sapling_" + n;
            case "rocks": return "stamps/rock/rock_" + n;
            case "ford_stones": kind = Kind.Prop; return "crossings/ford_stones_" + n;
            case "fog_wisps": kind = Kind.Fx; return "fx/wisp_" + n;
            case "tells":
                kind = Kind.Fx;
                string[] tells = { "tell_smoke", "tell_campfire", "tell_lair", "tell_glint" };
                return n >= 1 && n <= 4 ? "fx/" + tells[n - 1] : null;
        }
        return null;
    }

    public static string Import(string src)
    {
        var report = new StringBuilder();
        var imported = new List<string>();
        int skipped = 0;
        foreach (var file in Directory.GetFiles(src, "*.png"))
        {
            string name = Path.GetFileNameWithoutExtension(file);
            Kind kind;
            string target = Target(name, out kind);
            if (target == null) { skipped++; report.AppendLine("skipped (not in the pack): " + name); continue; }
            string path = Dest + target + ".png";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.Copy(file, path, true);
            imported.Add(path);
        }
        // Settings are applied by OverworldTextureRules on import, so a later reimport keeps them.
        AssetDatabase.Refresh();
        foreach (var path in imported) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TowerMapArt.ClearCache();
        report.Insert(0, "Imported " + imported.Count + " files into " + Dest + (skipped > 0 ? " (" + skipped + " skipped)" : "") + ".\n" + Missing() + "\n");
        return report.ToString();
    }

    public static void Configure(TextureImporter t, string path)
    {
        string rel = path.Substring(Dest.Length);
        t.textureType = TextureImporterType.Default;
        t.alphaIsTransparency = !rel.StartsWith("ground/");
        t.mipmapEnabled = true;
        t.textureCompression = TextureImporterCompression.CompressedHQ;
        t.sRGBTexture = true;
        if (rel.StartsWith("ground/") || rel == "fx/cloud_tile.png")
        {
            t.wrapMode = TextureWrapMode.Repeat;
            t.maxTextureSize = 1024;
            t.isReadable = false;
        }
        else if (rel.StartsWith("stamps/"))
        {
            // Packed into one atlas per map at runtime, which needs readable, uncompressed sources.
            t.wrapMode = TextureWrapMode.Clamp;
            t.maxTextureSize = 512;
            t.isReadable = true;
            t.textureCompression = TextureImporterCompression.Uncompressed;
        }
        else
        {
            t.wrapMode = TextureWrapMode.Clamp;
            t.maxTextureSize = 1024;
            t.isReadable = false;
        }
    }

    // What the map still draws with placeholders.
    public static string Missing()
    {
        var missing = new List<string>();
        foreach (var g in TowerMapArt.Grounds)
            if (!File.Exists(Dest + "ground/" + g + ".png")) missing.Add(g);
        foreach (var family in new[] { "canopy_oak", "canopy_pine", "canopy_birch", "canopy_silverwood", "canopy_willow", "canopy_dead", "bush", "rock", "reed" })
            if (!Directory.Exists(Dest + "stamps/" + family) || Directory.GetFiles(Dest + "stamps/" + family, "*.png").Length == 0) missing.Add("stamps/" + family);
        foreach (var kind in new[] { "camp", "lair", "merchant", "shrine", "treasure", "mystery", "combat", "elite" })
            if (!File.Exists(Dest + "props/poi_" + kind + ".png")) missing.Add("poi_" + kind);
        if (!File.Exists(Dest + "fx/cloud_tile.png")) missing.Add("cloud_tile");
        return missing.Count == 0 ? "Every layer now uses real art." : "Still placeholder: " + string.Join(", ", missing);
    }
}

// Import settings for everything under Resources/AdamsHaven/Expedition/Overworld (see Configure).
public sealed class OverworldTextureRules : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        string path = assetPath.Replace("\\", "/");
        if (!path.StartsWith("Assets/Resources/" + TowerMapArt.Root)) return;
        TowerOverworldArtImporter.Configure((TextureImporter)assetImporter, path);
    }
}
