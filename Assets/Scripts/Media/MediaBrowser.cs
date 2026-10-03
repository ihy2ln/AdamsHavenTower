using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using static BattleGui;

// In-game file picker for the media library (IMGUI, virtual canvas). Lists folders and the files a slot accepts.
// Quick places: the library's Inbox, the usual user folders on Windows, and shared storage on Android (asks for the
// media permissions the first time). Desktop players can also type or paste a full path.
public sealed class MediaBrowser
{
    public string Title = "IMPORT";
    public string[] Extensions = new string[0];
    public Action<string> Picked;
    public Action Cancelled;

    private string folder;
    private readonly List<string> dirs = new List<string>();
    private readonly List<string> files = new List<string>();
    private string error;
    private float scroll;
    private string typed = "";
    private bool dragging;
    private float dragStartY, dragStartScroll;

    private static readonly Color Gold = new Color(.97f, .82f, .48f), Ice = new Color(.60f, .86f, 1f), Red = new Color(1f, .55f, .50f);
    private const string LastFolderKey = "AdamsHaven.Media.LastFolder";

    public void Open(string title, string[] extensions, Action<string> picked, Action cancelled = null)
    {
        Title = title; Extensions = extensions ?? new string[0]; Picked = picked; Cancelled = cancelled;
        RequestAndroidPermissions();
        string last = PlayerPrefs.GetString(LastFolderKey, "");
        Go(Directory.Exists(last) ? last : MediaLibrary.Inbox);
    }

    public static List<KeyValuePair<string, string>> Places()
    {
        var list = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("INBOX", MediaLibrary.Inbox) };
        if (Application.platform == RuntimePlatform.Android)
        {
            foreach (var name in new[] { "Download", "Pictures", "Movies", "Music", "DCIM", "Documents" })
            {
                string p = "/storage/emulated/0/" + name;
                if (Directory.Exists(p)) list.Add(new KeyValuePair<string, string>(name.ToUpperInvariant(), p));
            }
            if (Directory.Exists("/storage/emulated/0")) list.Add(new KeyValuePair<string, string>("PHONE", "/storage/emulated/0"));
            return list;
        }
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var name in new[] { "Downloads", "Desktop", "Pictures", "Videos", "Music", "Documents" })
        {
            string p = Path.Combine(home, name);
            if (Directory.Exists(p)) list.Add(new KeyValuePair<string, string>(name.ToUpperInvariant(), p));
        }
        try { foreach (var d in DriveInfo.GetDrives()) if (d.IsReady && d.DriveType == DriveType.Fixed) list.Add(new KeyValuePair<string, string>(d.Name.TrimEnd('\\', '/'), d.RootDirectory.FullName)); }
        catch { }
        return list;
    }

    private static void RequestAndroidPermissions()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        foreach (var p in new[] { "android.permission.READ_MEDIA_IMAGES", "android.permission.READ_MEDIA_VIDEO",
            "android.permission.READ_MEDIA_AUDIO", "android.permission.READ_EXTERNAL_STORAGE" })
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(p)) UnityEngine.Android.Permission.RequestUserPermission(p);
#endif
    }

    private void Go(string path)
    {
        folder = path; scroll = 0; error = null;
        dirs.Clear(); files.Clear();
        try
        {
            foreach (var d in Directory.GetDirectories(path))
            {
                string name = Path.GetFileName(d);
                if (name.StartsWith(".", StringComparison.Ordinal) || name.StartsWith("$", StringComparison.Ordinal)) continue;
                dirs.Add(d);
            }
            foreach (var f in Directory.GetFiles(path))
                if (Extensions.Length == 0 || Array.IndexOf(Extensions, Path.GetExtension(f).ToLowerInvariant()) >= 0) files.Add(f);
            dirs.Sort(StringComparer.OrdinalIgnoreCase);
            files.Sort(StringComparer.OrdinalIgnoreCase);
            PlayerPrefs.SetString(LastFolderKey, path);
        }
        catch (Exception e) { error = "Cannot open this folder (" + e.GetType().Name + "). On a phone, allow media access or copy files into the INBOX folder."; }
    }

    // Drawn inside the media screen; consumes clicks in its own area.
    public void Draw(Rect box, Vector2 mouse)
    {
        Event e = Event.current;
        Fill(new Rect(-2000, -2000, 6000, 6000), new Color(0, 0, 0, .55f));
        Round(box, new Color(.02f, .04f, .07f, .99f), 16f);
        Outline(box, Alpha(Gold, .85f), 2f, 16f);
        Text(new Rect(box.x + 24f, box.y + 12f, box.width - 200f, 36f), Title, 24, Gold, TextAnchor.MiddleLeft, true);
        Text(new Rect(box.x + 24f, box.y + 46f, box.width - 48f, 22f), "Accepts " + (Extensions.Length == 0 ? "any file" : string.Join("  ", Extensions)), 13, Ice, TextAnchor.MiddleLeft);
        if (Button(new Rect(box.xMax - 150f, box.y + 14f, 126f, 38f), "CANCEL", true, Red, mouse)) { Cancelled?.Invoke(); return; }

        // Places.
        float px = box.x + 24f, py = box.y + 78f;
        foreach (var place in Places())
        {
            float w = Mathf.Max(70f, place.Key.Length * 10f + 30f);
            if (px + w > box.xMax - 24f) { px = box.x + 24f; py += 40f; }
            if (Button(new Rect(px, py, w, 34f), place.Key, true, Ice, mouse, string.Equals(folder, place.Value, StringComparison.OrdinalIgnoreCase))) Go(place.Value);
            px += w + 8f;
        }
        py += 44f;
        // Path and UP.
        Rect pathRect = new Rect(box.x + 24f, py, box.width - 170f, 30f);
        Round(pathRect, new Color(.05f, .08f, .12f), 8f);
        Text(new Rect(pathRect.x + 10f, pathRect.y, pathRect.width - 20f, pathRect.height), folder ?? "", 13, Color.white, TextAnchor.MiddleLeft);
        var parent = folder != null ? Directory.GetParent(folder) : null;
        if (Button(new Rect(box.xMax - 136f, py - 2f, 112f, 34f), "UP", parent != null, Ice, mouse)) Go(parent.FullName);
        py += 40f;

        // Listing.
        Rect list = new Rect(box.x + 24f, py, box.width - 48f, box.yMax - py - (Application.isMobilePlatform ? 20f : 64f));
        Round(list, new Color(.03f, .05f, .09f), 10f);
        if (error != null) Text(new Rect(list.x + 16f, list.y + 10f, list.width - 32f, 60f), error, 14, Red, TextAnchor.UpperLeft, false, true);
        const float row = 40f;
        int total = dirs.Count + files.Count;
        float maxScroll = Mathf.Max(0f, total * row - list.height + 8f);
        if (e.type == EventType.ScrollWheel && list.Contains(mouse)) { scroll = Mathf.Clamp(scroll + e.delta.y * 30f, 0, maxScroll); e.Use(); }
        if (e.type == EventType.MouseDown && list.Contains(mouse)) { dragging = true; dragStartY = mouse.y; dragStartScroll = scroll; }
        if (e.type == EventType.MouseDrag && dragging) { scroll = Mathf.Clamp(dragStartScroll - (mouse.y - dragStartY), 0, maxScroll); }
        bool tap = e.type == EventType.MouseUp && dragging && Mathf.Abs(mouse.y - dragStartY) < 8f && list.Contains(mouse);
        if (e.type == EventType.MouseUp) dragging = false;
        GUI.BeginGroup(list);
        for (int i = 0; i < total; i++)
        {
            float y = 4f + i * row - scroll;
            if (y < -row || y > list.height) continue;
            bool isDir = i < dirs.Count;
            string path = isDir ? dirs[i] : files[i - dirs.Count];
            Rect r = new Rect(8f, y, list.width - 16f, row - 4f);
            Vector2 local = mouse - list.position;
            bool hover = r.Contains(local);
            Round(r, hover ? new Color(.10f, .16f, .24f) : new Color(.05f, .08f, .12f), 8f);
            Text(new Rect(r.x + 14f, r.y, 90f, r.height), isDir ? "FOLDER" : MediaLibrary.KindOf(path).ToUpperInvariant(), 11, isDir ? Gold : Ice, TextAnchor.MiddleLeft, true);
            Text(new Rect(r.x + 104f, r.y, r.width - 220f, r.height), Path.GetFileName(path), 15, Color.white, TextAnchor.MiddleLeft);
            if (!isDir)
            {
                long size = 0;
                try { size = new FileInfo(path).Length; } catch { }
                Text(new Rect(r.xMax - 120f, r.y, 106f, r.height), Size(size), 12, new Color(.7f, .76f, .85f), TextAnchor.MiddleRight);
            }
            if (tap && hover)
            {
                tap = false;
                if (isDir) { GUI.EndGroup(); Go(path); e.Use(); return; }
                GUI.EndGroup(); e.Use(); Picked?.Invoke(path); return;
            }
        }
        GUI.EndGroup();
        if (total == 0 && error == null)
            Text(new Rect(list.x + 16f, list.y + 10f, list.width - 32f, 50f), "No matching files here. Folders and " + string.Join(" ", Extensions) + " files are listed.", 14, new Color(.7f, .76f, .85f), TextAnchor.UpperLeft, false, true);

        if (!Application.isMobilePlatform)
        {
            // Paste or type a full path (desktop).
            Rect field = new Rect(box.x + 24f, box.yMax - 52f, box.width - 200f, 34f);
            GUI.SetNextControlName("media-path");
            typed = GUI.TextField(field, typed);
            if (Button(new Rect(box.xMax - 164f, box.yMax - 54f, 140f, 38f), "USE PATH", typed.Trim().Length > 0, Gold, mouse))
            {
                string p = typed.Trim().Trim('"');
                if (Directory.Exists(p)) Go(p);
                else if (File.Exists(p)) Picked?.Invoke(p);
                else error = "No file or folder at that path.";
            }
        }
    }

    private static string Size(long bytes)
    {
        if (bytes >= 1 << 20) return (bytes / 1048576f).ToString("0.0") + " MB";
        if (bytes >= 1 << 10) return (bytes / 1024f).ToString("0") + " KB";
        return bytes + " B";
    }

    // A command button (press on mouse down, like the battle screen).
    public static bool Button(Rect r, string label, bool enabled, Color accent, Vector2 mouse, bool active = false, int size = 14)
    {
        bool hover = enabled && r.Contains(mouse);
        CommandSurface(r, accent, enabled, hover, active);
        Text(r, label, size, enabled ? Color.white : new Color(.5f, .54f, .6f), TextAnchor.MiddleCenter, true);
        Event e = Event.current;
        if (enabled && e.type == EventType.MouseDown && e.button == 0 && r.Contains(mouse)) { e.Use(); return true; }
        return false;
    }
}
