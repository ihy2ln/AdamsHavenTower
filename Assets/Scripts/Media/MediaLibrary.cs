using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

// Player-imported media (the MEDIA LIBRARY screen). Files are copied into <persistentDataPath>/Media/files and named
// in media.json, so they survive restarts and app updates. Every slot falls back to the game's own content when empty.
//
// Slot keys:
//   card.<card id>.art | .fx | .cine | .sfx     a move's card art, field effect (image or video), cinematic, sound
//   unit.<unit id>.model | .portrait | .card     a fighter's or monster's field model (image cutout or .glb/.gltf),
//                                                portrait (tiles, plates) and full card art (cut-ins, sheet)
//   sfx.<sound id>                               any game sound (hit, crit, swing, door, Sfx/card_draw ...)
//   music.battle | music.atlas | music.dungeon   looping music
// Model swaps ("use Ghislaine's model for Kaela") are a separate table: unit id -> built-in model id.
// Maps: battle stages. Built-in stages can be taken out (hidden); imported stages are added with a theme tag.
public static class MediaLibrary
{
    public const string Image = "image", Video = "video", Audio = "audio", Model = "model";

    [Serializable] public sealed class Entry { public string key = "", file = "", kind = "", source = ""; public float length; public int width, height; }
    [Serializable] public sealed class Stage { public string id = "", name = "", file = "", theme = "any"; public bool on = true; }
    [Serializable] public sealed class Swap { public string unit = "", model = ""; }
    [Serializable] private sealed class Manifest
    {
        public int version = 1;
        public List<Entry> entries = new List<Entry>();
        public List<Stage> stages = new List<Stage>();
        public List<string> hidden = new List<string>();
        public List<Swap> swaps = new List<Swap>();
    }

    // A stage the game ships with (Resources path under AdamsHaven/), and the dungeon themes it suits.
    public sealed class BuiltInStage { public string Id, Name, Resource, Theme; }

    public static readonly string[] Themes = { "any", "briar", "cave", "crystal", "marsh", "keep", "mine", "ruin", "blight", "heartwood", "boss" };
    public static readonly string[] ImageExt = { ".png", ".jpg", ".jpeg" };
    public static readonly string[] VideoExt = { ".mp4", ".webm", ".mov", ".m4v" };
    public static readonly string[] AudioExt = { ".ogg", ".wav", ".mp3" };
    public static readonly string[] ModelExt = { ".glb", ".gltf" };

    public static event Action Changed;

    private static Manifest data;
    private static readonly Dictionary<string, Texture2D> images = new Dictionary<string, Texture2D>();
    private static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private static readonly HashSet<string> loading = new HashSet<string>();
    private static readonly Dictionary<string, GameObject> models = new Dictionary<string, GameObject>();
    private static Runner runner;

    public static string Root { get { return Path.Combine(Application.persistentDataPath, "Media"); } }
    public static string Files { get { return Path.Combine(Root, "files"); } }
    // A drop folder the browser always lists: on Android it is reachable over USB (Android/data/<app>/files/Media/Inbox).
    public static string Inbox { get { string p = Path.Combine(Root, "Inbox"); try { Directory.CreateDirectory(p); } catch { } return p; } }
    private static string ManifestPath { get { return Path.Combine(Root, "media.json"); } }

    private static Manifest Data
    {
        get
        {
            if (data != null) return data;
            data = new Manifest();
            try
            {
                if (File.Exists(ManifestPath)) data = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath)) ?? new Manifest();
            }
            catch (Exception e) { Debug.LogWarning("Media library unreadable, starting empty: " + e.Message); data = new Manifest(); }
            // Drop entries whose file was deleted outside the game.
            data.entries.RemoveAll(e => !File.Exists(Path.Combine(Files, e.file)));
            data.stages.RemoveAll(s => !File.Exists(Path.Combine(Files, s.file)));
            return data;
        }
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(ManifestPath, JsonUtility.ToJson(Data, true));
        }
        catch (Exception e) { Debug.LogWarning("Media library not saved: " + e.Message); }
        Changed?.Invoke();
    }

    // For tests: point at a fresh state (the files on disk are left alone).
    public static void Reload() { data = null; ForgetLoaded(); Changed?.Invoke(); }

    private static void ForgetLoaded()
    {
        foreach (var t in images.Values) if (t) UnityEngine.Object.Destroy(t);
        images.Clear();
        clips.Clear();
        foreach (var m in models.Values) if (m) UnityEngine.Object.Destroy(m);
        models.Clear();
    }

    // ---- kinds ---------------------------------------------------------------------------------------------

    public static string KindOf(string path)
    {
        string ext = Path.GetExtension(path ?? "").ToLowerInvariant();
        if (Array.IndexOf(ImageExt, ext) >= 0) return Image;
        if (Array.IndexOf(VideoExt, ext) >= 0) return Video;
        if (Array.IndexOf(AudioExt, ext) >= 0) return Audio;
        if (Array.IndexOf(ModelExt, ext) >= 0) return Model;
        return "";
    }

    // Which kinds a slot takes.
    public static string[] Accepts(string key)
    {
        if (key.EndsWith(".art", StringComparison.Ordinal) || key.EndsWith(".portrait", StringComparison.Ordinal)
            || key.EndsWith(".card", StringComparison.Ordinal)) return new[] { Image };
        if (key.EndsWith(".fx", StringComparison.Ordinal)) return new[] { Image, Video };
        if (key.EndsWith(".cine", StringComparison.Ordinal)) return new[] { Video };
        if (key.EndsWith(".sfx", StringComparison.Ordinal) || key.StartsWith("sfx.", StringComparison.Ordinal)
            || key.StartsWith("music.", StringComparison.Ordinal)) return new[] { Audio };
        if (key.EndsWith(".model", StringComparison.Ordinal)) return new[] { Image, Model };
        if (key.StartsWith("stage", StringComparison.Ordinal)) return new[] { Image };
        return new[] { Image, Video, Audio, Model };
    }

    public static string[] Extensions(string[] kinds)
    {
        var list = new List<string>();
        foreach (var k in kinds)
        {
            if (k == Image) list.AddRange(ImageExt);
            if (k == Video) list.AddRange(VideoExt);
            if (k == Audio) list.AddRange(AudioExt);
            if (k == Model) list.AddRange(ModelExt);
        }
        return list.ToArray();
    }

    // ---- slots ---------------------------------------------------------------------------------------------

    public static Entry Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        foreach (var e in Data.entries) if (e.key == key) return e;
        return null;
    }

    public static bool Has(string key) { return Get(key) != null; }
    public static string FilePath(Entry e) { return e == null ? null : Path.Combine(Files, e.file); }
    public static int Count { get { return Data.entries.Count + Data.stages.Count; } }

    // Copies the file into the library and points the slot at it. Returns an error message or null.
    public static string Import(string key, string source)
    {
        if (string.IsNullOrEmpty(source) || !File.Exists(source)) return "That file is not there any more.";
        string kind = KindOf(source);
        if (kind.Length == 0) return "Unsupported file type " + Path.GetExtension(source) + ".";
        if (Array.IndexOf(Accepts(key), kind) < 0) return "This slot takes " + string.Join(" or ", Accepts(key)) + " files, not " + kind + ".";
        string file;
        string error = CopyIn(source, out file);
        if (error != null) return error;
        if (kind == Model)
        {
            string loadError;
            var test = MediaGltf.Load(Path.Combine(Files, file), out loadError);
            if (test == null) { TryDelete(file); return loadError ?? "The model could not be read."; }
            UnityEngine.Object.Destroy(test);
            // A .gltf keeps its .bin and textures beside it: bring the whole folder's referenced files along.
            if (Path.GetExtension(source).ToLowerInvariant() == ".gltf") CopySiblings(source, file);
        }
        var old = Get(key);
        if (old != null) { Forget(old); Data.entries.Remove(old); if (!Used(old.file)) TryDelete(old.file); }
        var entry = new Entry { key = key, file = file, kind = kind, source = Path.GetFileName(source) };
        if (kind == Image)
        {
            var tex = LoadTexture(Path.Combine(Files, file));
            if (tex != null) { entry.width = tex.width; entry.height = tex.height; images[key] = tex; }
            else { TryDelete(file); return "The image could not be read."; }
        }
        Data.entries.Add(entry);
        Save();
        if (kind == Audio) Preload(key);
        return null;
    }

    public static void Clear(string key)
    {
        var e = Get(key);
        if (e == null) return;
        Forget(e);
        Data.entries.Remove(e);
        if (!Used(e.file)) TryDelete(e.file);
        Save();
    }

    // Video length is only known once a player has opened the file; the media screen records it here.
    public static void SetVideoInfo(string key, float seconds, int width, int height)
    {
        var e = Get(key);
        if (e == null || (Mathf.Approximately(e.length, seconds) && e.width == width && e.height == height)) return;
        e.length = seconds; e.width = width; e.height = height;
        Save();
    }

    private static void Forget(Entry e)
    {
        Texture2D t;
        if (images.TryGetValue(e.key, out t)) { if (t) UnityEngine.Object.Destroy(t); images.Remove(e.key); }
        clips.Remove(e.key);
        GameObject m;
        if (models.TryGetValue(e.key, out m)) { if (m) UnityEngine.Object.Destroy(m); models.Remove(e.key); }
    }

    private static bool Used(string file)
    {
        foreach (var e in Data.entries) if (e.file == file) return true;
        foreach (var s in Data.stages) if (s.file == file) return true;
        return false;
    }

    private static string CopyIn(string source, out string file)
    {
        file = null;
        try
        {
            Directory.CreateDirectory(Files);
            string ext = Path.GetExtension(source).ToLowerInvariant();
            string stem = Path.GetFileNameWithoutExtension(source);
            var safe = new System.Text.StringBuilder();
            foreach (char c in stem) safe.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            string name = (safe.Length > 40 ? safe.ToString(0, 40) : safe.ToString()) + "_" + DateTime.UtcNow.Ticks.ToString("x") + ext;
            File.Copy(source, Path.Combine(Files, name), true);
            file = name;
            return null;
        }
        catch (Exception e) { return "Could not copy the file: " + e.Message; }
    }

    private static void CopySiblings(string gltf, string copied)
    {
        try
        {
            var root = MediaJson.Parse(File.ReadAllText(gltf));
            var uris = new List<string>();
            foreach (string list in new[] { "buffers", "images" })
            {
                var items = MediaJson.Arr(root, list);
                if (items == null) continue;
                foreach (var item in items)
                {
                    string uri = MediaJson.Str(item, "uri");
                    if (uri != null && !uri.StartsWith("data:", StringComparison.Ordinal)) uris.Add(Uri.UnescapeDataString(uri));
                }
            }
            string from = Path.GetDirectoryName(gltf);
            foreach (string uri in uris)
            {
                string src = Path.Combine(from, uri), dst = Path.Combine(Files, uri);
                if (!File.Exists(src)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                File.Copy(src, dst, true);
            }
        }
        catch (Exception e) { Debug.LogWarning("glTF side files not copied: " + e.Message); }
    }

    private static void TryDelete(string file)
    {
        try { string p = Path.Combine(Files, file); if (File.Exists(p)) File.Delete(p); } catch { }
    }

    // ---- loading -------------------------------------------------------------------------------------------

    private static Texture2D LoadTexture(string path)
    {
        try
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = Path.GetFileName(path), wrapMode = TextureWrapMode.Clamp };
            if (!tex.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.Destroy(tex); return null; }
            tex.filterMode = FilterMode.Trilinear;
            return tex;
        }
        catch { return null; }
    }

    // The slot's image, or null when it has none.
    public static Texture2D ImageFor(string key)
    {
        var e = Get(key);
        if (e == null || e.kind != Image) return null;
        Texture2D tex;
        if (images.TryGetValue(key, out tex) && tex) return tex;
        tex = LoadTexture(FilePath(e));
        images[key] = tex;
        return tex;
    }

    // file:// URL for a VideoPlayer, or null.
    public static string VideoUrl(string key)
    {
        var e = Get(key);
        return e == null || e.kind != Video ? null : "file://" + FilePath(e).Replace('\\', '/');
    }

    // The slot's sound once loaded; the first call starts loading it and returns null.
    public static AudioClip AudioFor(string key)
    {
        var e = Get(key);
        if (e == null || e.kind != Audio) return null;
        AudioClip clip;
        if (clips.TryGetValue(key, out clip)) return clip;
        Preload(key);
        return null;
    }

    public static void Preload(string key)
    {
        var e = Get(key);
        if (e == null || e.kind != Audio || clips.ContainsKey(key) || loading.Contains(key)) return;
        if (!Application.isPlaying) return;
        if (runner == null)
        {
            var host = new GameObject("Media Library");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideInHierarchy;
            runner = host.AddComponent<Runner>();
        }
        loading.Add(key);
        runner.StartCoroutine(LoadAudio(key, FilePath(e)));
    }

    // Starts loading every imported sound, so the first play of each is not silent.
    public static void PreloadAll()
    {
        foreach (var e in Data.entries) if (e.kind == Audio) Preload(e.key);
    }

    private static IEnumerator LoadAudio(string key, string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        AudioType type = ext == ".wav" ? AudioType.WAV : ext == ".mp3" ? AudioType.MPEG : AudioType.OGGVORBIS;
        using (var request = UnityWebRequestMultimedia.GetAudioClip("file://" + path.Replace('\\', '/'), type))
        {
            yield return request.SendWebRequest();
            AudioClip clip = null;
            if (request.result == UnityWebRequest.Result.Success) clip = DownloadHandlerAudioClip.GetContent(request);
            else Debug.LogWarning("Imported sound not loaded (" + key + "): " + request.error);
            if (clip != null) clip.name = key;
            clips[key] = clip;
            loading.Remove(key);
        }
    }

    // A fresh, active copy of the slot's imported model (the caller owns and destroys it), or null.
    public static GameObject ModelFor(string key, out string error)
    {
        error = null;
        var e = Get(key);
        if (e == null || e.kind != Model) return null;
        GameObject template;
        if (!models.TryGetValue(key, out template) || !template)
        {
            template = MediaGltf.Load(FilePath(e), out error);
            if (template == null) return null;
            UnityEngine.Object.DontDestroyOnLoad(template);
            template.hideFlags = HideFlags.HideInHierarchy;
            models[key] = template;
        }
        var copy = UnityEngine.Object.Instantiate(template);
        copy.hideFlags = HideFlags.None;
        copy.SetActive(true);
        return copy;
    }

    // ---- model swaps ---------------------------------------------------------------------------------------

    // The built-in model a unit borrows ("" when it uses its own).
    public static string SwapFor(string unit)
    {
        foreach (var s in Data.swaps) if (s.unit == unit) return s.model;
        return "";
    }

    public static void SetSwap(string unit, string model)
    {
        Data.swaps.RemoveAll(s => s.unit == unit);
        if (!string.IsNullOrEmpty(model) && model != unit) Data.swaps.Add(new Swap { unit = unit, model = model });
        Save();
    }

    // ---- stages (battle maps) --------------------------------------------------------------------------------

    public static readonly BuiltInStage[] BuiltInStages =
    {
        new BuiltInStage { Id = "silverwood", Name = "Silverwood Glade", Resource = "Battle/silverwood_battle_v1", Theme = "any" },
        new BuiltInStage { Id = "blight", Name = "Blighted Ground", Resource = "Expedition/Dungeon/AnimeV2/blight_vista", Theme = "blight" },
        new BuiltInStage { Id = "crystal", Name = "Crystal Grotto", Resource = "Expedition/Dungeon/AnimeV2/crystal_vista", Theme = "crystal" },
        new BuiltInStage { Id = "heartwood", Name = "Heartwood Hollow", Resource = "Expedition/Dungeon/AnimeV2/heartwood_vista", Theme = "heartwood" },
        new BuiltInStage { Id = "mine", Name = "Abandoned Mine", Resource = "Expedition/Dungeon/AnimeV2/mine_vista", Theme = "mine" },
        new BuiltInStage { Id = "boss_d1", Name = "Depth 1 Lair", Resource = "Expedition/Rooms/boss_d1", Theme = "boss" },
        new BuiltInStage { Id = "boss_d2", Name = "Depth 2 Lair", Resource = "Expedition/Rooms/boss_d2", Theme = "boss" },
        new BuiltInStage { Id = "boss_d3", Name = "Depth 3 Lair", Resource = "Expedition/Rooms/boss_d3", Theme = "boss" },
        new BuiltInStage { Id = "boss_d4", Name = "Depth 4 Lair", Resource = "Expedition/Rooms/boss_d4", Theme = "boss" },
        new BuiltInStage { Id = "boss_d5", Name = "Depth 5 Lair", Resource = "Expedition/Rooms/boss_d5", Theme = "boss" },
    };

    public static List<Stage> CustomStages { get { return Data.stages; } }

    public static bool StageHidden(string builtInId) { return Data.hidden.Contains(builtInId); }

    public static void SetStageHidden(string builtInId, bool hidden)
    {
        Data.hidden.Remove(builtInId);
        if (hidden) Data.hidden.Add(builtInId);
        Save();
    }

    public static string AddStage(string source, string theme)
    {
        if (KindOf(source) != Image) return "A stage is a picture (.png or .jpg), ideally 16:9 and at least 1600 px wide.";
        string file;
        string error = CopyIn(source, out file);
        if (error != null) return error;
        var tex = LoadTexture(Path.Combine(Files, file));
        if (tex == null) { TryDelete(file); return "The image could not be read."; }
        var stage = new Stage { id = "stage_" + DateTime.UtcNow.Ticks.ToString("x"), name = Path.GetFileNameWithoutExtension(source),
            file = file, theme = Array.IndexOf(Themes, theme) >= 0 ? theme : "any" };
        images["stage:" + stage.id] = tex;
        Data.stages.Add(stage);
        Save();
        return null;
    }

    public static void RemoveStage(string id)
    {
        var s = Data.stages.Find(x => x.id == id);
        if (s == null) return;
        Texture2D t;
        if (images.TryGetValue("stage:" + id, out t)) { if (t) UnityEngine.Object.Destroy(t); images.Remove("stage:" + id); }
        Data.stages.Remove(s);
        if (!Used(s.file)) TryDelete(s.file);
        Save();
    }

    public static void UpdateStage(Stage stage) { Save(); }

    public static Texture2D StageImage(Stage s)
    {
        if (s == null) return null;
        Texture2D tex;
        string key = "stage:" + s.id;
        if (images.TryGetValue(key, out tex) && tex) return tex;
        tex = LoadTexture(Path.Combine(Files, s.file));
        images[key] = tex;
        return tex;
    }

    // Picks the stage for a fight. The game's choice is kept unless it was taken out or the player added stages for
    // this theme (or "any"), in which case the seed picks among what is left. Returns a texture or null for the game's.
    public static Texture2D PickStage(string theme, bool boss, string gameChoiceId, int seed)
    {
        var pool = new List<Stage>();
        foreach (var s in Data.stages)
            if (s.on && (s.theme == "any" || s.theme == theme || (boss && s.theme == "boss"))) pool.Add(s);
        bool gameHidden = gameChoiceId != null && StageHidden(gameChoiceId);
        if (pool.Count == 0 && !gameHidden) return null;
        var choices = new List<object>();
        foreach (var s in pool) choices.Add(s);
        if (!gameHidden && gameChoiceId != null) choices.Add(gameChoiceId);
        if (choices.Count == 0)
            foreach (var b in BuiltInStages) if (!StageHidden(b.Id) && b.Theme != "boss") choices.Add(b.Id);
        if (choices.Count == 0) return null;
        var pick = choices[(int)((uint)seed % (uint)choices.Count)];
        if (pick is Stage) return StageImage((Stage)pick);
        foreach (var b in BuiltInStages) if (b.Id == (string)pick) return Resources.Load<Texture2D>("AdamsHaven/" + b.Resource);
        return null;
    }

    private sealed class Runner : MonoBehaviour { }
}
