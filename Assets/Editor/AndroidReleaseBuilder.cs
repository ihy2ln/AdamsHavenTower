using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// One-shot APK build that survives the domain reloads a build-target switch causes.
// Start() -> switch to Android -> build -> switch back to Windows. Progress lives in SessionState
// and the outcome is written to Builds/apk_build_log.txt.
[InitializeOnLoad]
public static class AndroidReleaseBuilder
{
    private const string StageKey = "AdamsHaven.ApkBuild.Stage";
    private const string NameKey = "AdamsHaven.ApkBuild.ProductName";
    private const string Version = "0.9.1";
    private const int VersionCode = 12;
    private const string ApkName = "AdamsHavenTowerBattle-" + Version + ".apk";

    private static string BuildsFolder { get { return Path.Combine(Directory.GetCurrentDirectory(), "Builds"); } }
    private static string LogPath { get { return Path.Combine(BuildsFolder, "apk_build_log.txt"); } }

    static AndroidReleaseBuilder() { EditorApplication.delayCall += Continue; }

    public static void Start()
    {
        Directory.CreateDirectory(BuildsFolder);
        File.WriteAllText(LogPath, "started " + DateTime.Now + "\n");
        SessionState.SetString(NameKey, PlayerSettings.productName);
        SessionState.SetString(StageKey, "switch");
        Continue();
    }

    // Batch entry: Unity -batchmode -buildTarget Android -executeMethod AndroidReleaseBuilder.BuildBatch -apkOut <file>
    public static void BuildBatch()
    {
        var args = Environment.GetCommandLineArgs();
        int at = Array.IndexOf(args, "-apkOut");
        if (at >= 0 && at + 1 < args.Length) outputOverride = args[at + 1];
        Directory.CreateDirectory(BuildsFolder);
        File.WriteAllText(LogPath, "batch started " + DateTime.Now + "\n");
        string name = PlayerSettings.productName;
        try { Build(); }
        finally { PlayerSettings.productName = name; AssetDatabase.SaveAssets(); Note("batch finished " + DateTime.Now); }
    }

    private static string outputOverride;

    // Build on the current target without the switch back to Windows (TT 10.5.0b): when the editor already targets
    // Android, the switch-back costs a full reimport for nothing. Runs from a delayCall so the caller returns first.
    public static void BuildHere()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) { Start(); return; }
        Directory.CreateDirectory(BuildsFolder);
        File.WriteAllText(LogPath, "started (Android, no switch) " + DateTime.Now + Environment.NewLine);
        string name = PlayerSettings.productName;
        try { Build(); }
        catch (Exception ex) { Note("EXCEPTION " + ex); }
        finally { PlayerSettings.productName = name; AssetDatabase.SaveAssets(); Note("finished " + DateTime.Now); }
    }

    private static void Note(string text) { File.AppendAllText(LogPath, text + "\n"); }

    private static void Continue()
    {
        string stage = SessionState.GetString(StageKey, "");
        if (stage.Length == 0 || EditorApplication.isPlaying) return;
        // After the target switch the editor may still be compiling or importing: try again next editor tick instead of
        // giving up, which left builds stuck at the "build" stage.
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += Continue; return; }
        try
        {
            if (stage == "switch")
            {
                SessionState.SetString(StageKey, "build");
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                {
                    Note("switching to Android");
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                    return; // the reload that follows calls Continue again
                }
                stage = "build";
            }
            if (stage == "build")
            {
                SessionState.SetString(StageKey, "restore");
                Build();
                stage = "restore";
            }
            if (stage == "restore")
            {
                SessionState.SetString(StageKey, "");
                PlayerSettings.productName = SessionState.GetString(NameKey, PlayerSettings.productName);
                AssetDatabase.SaveAssets();
                Note("restored product name; switching back to Windows");
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
                Note("finished " + DateTime.Now);
            }
        }
        catch (Exception ex)
        {
            Note("EXCEPTION " + ex);
            SessionState.SetString(StageKey, "");
        }
    }

    private static void Build()
    {
        var android = NamedBuildTarget.Android;
        PlayerSettings.productName = "Adams Haven: Tower Battle";
        PlayerSettings.SetApplicationIdentifier(android, "com.adamshaven.tower");
        PlayerSettings.bundleVersion = Version;
        PlayerSettings.Android.bundleVersionCode = VersionCode;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.development = false;
        // One scene: AdamsHavenTower runs the Tower, town, expeditions and battles (and the debug menu).
        var scenes = new System.Collections.Generic.List<string> { "Assets/Scenes/AdamsHavenTower.unity" };
        var settingsScenes = new EditorBuildSettingsScene[scenes.Count];
        for (int i = 0; i < scenes.Count; i++) settingsScenes[i] = new EditorBuildSettingsScene(scenes[i], true);
        EditorBuildSettings.scenes = settingsScenes;
        string output = outputOverride ?? Path.Combine(BuildsFolder, ApkName);
        Note("building " + string.Join(",", scenes) + " -> " + output);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = output,
            target = BuildTarget.Android,
            options = BuildOptions.None
        });
        var summary = report.summary;
        Note("result=" + summary.result + " errors=" + summary.totalErrors + " warnings=" + summary.totalWarnings +
            " size=" + summary.totalSize + " seconds=" + summary.totalTime.TotalSeconds.ToString("0"));
        foreach (var step in report.steps)
            foreach (var message in step.messages)
                if (message.type == LogType.Error || message.type == LogType.Exception)
                    Note("ERROR: " + message.content);
    }
}
