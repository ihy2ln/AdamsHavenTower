using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AdamsHaven.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Renders the actual expedition UI in an isolated, unsaved scene; never reads or writes player saves.
[InitializeOnLoad]
public static class TowerDungeonPreview
{
    private static int frames;
    static TowerDungeonPreview()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("SilverbrookPreview", false))
            { frames = 0; EditorApplication.update += RenderWhenReady; }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                string previous = SessionState.GetString("SilverbrookPreviousScene", "");
                SessionState.EraseString("SilverbrookPreviousScene");
                if (!Application.isBatchMode && !string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
            }
        };
    }

    [MenuItem("Adams Haven/Expedition/Render Silverbrook Board Previews")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode before rendering board previews."); return; }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SessionState.SetString("SilverbrookPreviousScene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("SilverbrookPreview", true);
        EditorApplication.EnterPlaymode();
    }

    private static void RenderWhenReady()
    {
        if (++frames < 20) return;
        EditorApplication.update -= RenderWhenReady;
        SessionState.SetBool("SilverbrookPreview", false);
        try
        {
            foreach (string theme in new[] { "cave", "ruin", "keep", "marsh", "briar", "crystal" }) Render(theme, false, 1280, 720);
            Render("cave", true, 1280, 720);
            Render("ruin", true, 1920, 1080);
            Render("briar", true, 1920, 1080, true);
            Render("briar", true, 960, 540);
            Debug.Log("Silverbrook board previews rendered successfully.");
            Finish(0);
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(1); }
    }

    private static void Finish(int code)
    {
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else EditorApplication.ExitPlaymode();
    }

    private static void Render(string theme, bool fog, int width, int height, bool close = false)
    {
        var state = TowerMilestones.Create(10);
        var rules = new TowerRules(state);
        state.hasRun = true;
        state.run = new TowerRun { layout = "briar_tangle", region = "silverbrook_edge", seed = 4321,
            party = new List<string> { "kaela", "elara", "ghislaine" }, hp = new List<int> { 100, 100, 100 } };
        var node = rules.RunLayout.nodes.Find(n => n.kind == "lair");
        node.theme = theme;
        state.run.at = node.id;
        if (rules.EnterPoi() != null) throw new Exception("Preview cannot enter POI");
        var run = rules.Run; var dungeon = rules.Dungeon;
        if (!fog) run.fog = new string('1', 400);
        else
        {
            var distances = dungeon.Distances(run.px, run.py);
            var discovery = run.fog.ToCharArray();
            for (int i = 0; i < discovery.Length; i++) if (distances[i] >= 0 && distances[i] <= 12) discovery[i] = '1';
            foreach (var room in dungeon.rooms)
                if (discovery[TowerDungeon.Index(room.CenterX, room.CenterY)] == '1')
                    for (int y = room.y - 1; y <= room.y + room.h; y++) for (int x = room.x - 1; x <= room.x + room.w; x++)
                        if (TowerDungeon.Inside(x, y)) discovery[TowerDungeon.Index(x, y)] = '1';
            run.fog = new string(discovery);
        }
        var controllerObject = new GameObject("Isolated preview controller");
        controllerObject.SetActive(false); // prevents Awake from loading real slots
        var controller = controllerObject.AddComponent<AdamsHavenPrototype>();
        typeof(AdamsHavenPrototype).GetField("rules", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(controller, rules);
        var uiObject = new GameObject("Dungeon preview UI");
        var ui = uiObject.AddComponent<TowerExpeditionUi>(); ui.enabled = false;
        ui.Begin(controller);
        var canvas = (Canvas)Get(ui, "canvas");
        var cameraObject = new GameObject("Preview camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.025f, 0.04f, 0.065f);
        var target = new RenderTexture(width, height, 24);
        camera.targetTexture = target;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        Canvas.ForceUpdateCanvases();
        var oldContent = (RectTransform)Get(ui, "content");
        UnityEngine.Object.DestroyImmediate(oldContent.gameObject);
        typeof(TowerExpeditionUi).GetField("content", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ui, null);
        typeof(TowerExpeditionUi).GetMethod("Rebuild", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(ui, null);
        Canvas.ForceUpdateCanvases();
        var board = (RectTransform)Get(ui, "board"); var viewport = (RectTransform)Get(ui, "viewport");
        float zoom = Mathf.Min((viewport.rect.width - 20) / board.rect.width, (viewport.rect.height - 20) / board.rect.height);
        typeof(TowerExpeditionUi).GetField("zoom", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, zoom);
        board.localScale = Vector3.one * zoom; board.anchoredPosition = Vector2.zero;
        var figures = (List<RectTransform>)Get(ui, "partyFigures");
        if (figures.Count != run.party.Count) throw new Exception("Full party missing");
        var panels = new List<GameObject>();
        foreach (Transform child in (RectTransform)Get(ui, "content")) if (child.name == "Run panel") panels.Add(child.gameObject);
        for (int i = 0; i + 1 < panels.Count; i++) UnityEngine.Object.DestroyImmediate(panels[i]);
        Canvas.ForceUpdateCanvases();
        var screenPoint = RectTransformUtility.WorldToScreenPoint(camera, viewport.TransformPoint(viewport.rect.center));
        if (!(bool)Call(ui, "OverViewport", screenPoint)) throw new Exception("Board pointer input blocked by its background");
        var illustration = (TowerDungeonIllustration)Get(ui, "dungeonIllustration");
        if (illustration == null) throw new Exception("Illustrated board was not selected");
        var mask = (Texture2D)Get(illustration, "discovery");
        for (int y = 0; y < TowerDungeon.Size; y++) for (int x = 0; x < TowerDungeon.Size; x++)
            if (!rules.Revealed(x, y) && mask.GetPixel(x * 8 + 4, mask.height - 1 - (y * 8 + 4)).r != 0)
                throw new Exception("Fog mask leaks an undiscovered cell");
        CheckAnimatedTravel(ui, rules);
        board.anchoredPosition = Vector2.zero;
        if (close)
        {
            board.localScale = Vector3.one * 1.35f;
            board.anchoredPosition = -(new Vector2((run.px + 0.5f) * 48, -(run.py + 0.5f) * 48) + new Vector2(-480, 480)) * 1.35f;
        }
        camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = target;
        var picture = new Texture2D(width, height, TextureFormat.RGB24, false);
        picture.ReadPixels(new Rect(0, 0, width, height), 0, 0); picture.Apply(); RenderTexture.active = previous;
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../build/dungeon-review/anime-v2"));
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, theme + (close ? "-close-" : fog ? "-fog-" : "-explored-") + width + ".png"), picture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(picture); UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(canvas.gameObject); UnityEngine.Object.DestroyImmediate(uiObject);
        UnityEngine.Object.DestroyImmediate(controllerObject); UnityEngine.Object.DestroyImmediate(cameraObject);
    }

    private static object Get(object target, string field)
    { return target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target); }

    private static object Call(object target, string method, params object[] args)
    { return target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args); }

    private static void CheckAnimatedTravel(TowerExpeditionUi ui, TowerRules rules)
    {
        int saves = 0;
        typeof(TowerExpeditionUi).GetField("commitDungeonStep", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(ui, (Action)(() => saves++));
        var run = rules.Run;
        int x = run.px, y = run.py;
        Call(ui, "Step", x + 1, y);
        if (run.px != x || run.py != y || saves != 0) throw new Exception("Room selection teleported the party");
        typeof(TowerExpeditionUi).GetField("travelClock", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, 0.3f);
        Call(ui, "UpdateTravel");
        if (run.px != x || run.py != y) throw new Exception("Cell committed before animation completed");
        typeof(TowerExpeditionUi).GetField("travelClock", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, 1f);
        Call(ui, "UpdateTravel");
        if (run.px != x + 1 || saves != 1) throw new Exception("Animated cell did not commit and save exactly once");
        // Reset this isolated fixture for a comparable static board render.
        run.px = x; run.py = y;
        Call(ui, "CancelTravel"); Call(ui, "RefreshDungeon");
    }
}
