using UnityEngine;
using UnityEditor;
using System.IO;

public static class BattleModelValidation
{
    static GameObject probe;
    static int frames;
    static int phase;
    static double started;
    [MenuItem("Adams Haven/Validate Battle Models")]
    public static void Validate()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("Run validation in Play Mode."); return; }
        if (probe) Object.Destroy(probe);
        probe = new GameObject("Temporary battle model validation");
        var mode = probe.AddComponent<BattleMode>();
        mode.enabled = false;
        mode.Begin(0, (win, reward) => {});
        Debug.Log(mode.DebugRigReport());
        frames = 0; phase = 0; started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Capture;
        EditorApplication.update += Capture;
    }
    static void Capture()
    {
        if (++frames < 60 || EditorApplication.timeSinceStartup - started < 1.0) return;
        if (!probe) { EditorApplication.update -= Capture; return; }
        var report = new System.Text.StringBuilder();
        foreach(var r in probe.GetComponentsInChildren<Renderer>()) report.AppendLine(r.name + " bounds=" + r.bounds + " scale=" + r.transform.lossyScale);
        File.WriteAllText(Path.Combine(Application.dataPath, "../BattleModelRuntimeReport.txt"), report.ToString());
        foreach (var camera in probe.GetComponentsInChildren<Camera>())
        {
            var rt = camera.targetTexture;
            if (!rt) continue;
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var image = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); image.Apply();
            RenderTexture.active = previous;
            string id = camera.transform.parent.name.Replace("Battle rig: ", "");
            File.WriteAllBytes(Path.Combine(Application.dataPath, "../Battle3D-" + id + (phase == 1 ? "-attack" : phase == 2 ? "-draw" : "") + ".png"), image.EncodeToPNG());
            Object.Destroy(image);
        }
        if (phase < 2) { if (phase == 0) probe.GetComponent<BattleMode>().DebugRigAttack(); else probe.GetComponent<BattleMode>().DebugRigDraw(); phase++; frames = 0; started = EditorApplication.timeSinceStartup; return; }
        EditorApplication.update -= Capture;
        Object.Destroy(probe);
        Debug.Log("Battle model validation images saved.");
    }
}
