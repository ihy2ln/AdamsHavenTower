using System;
using System.Collections.Generic;
using AdamsHaven.Tower;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Runtime-built uGUI keeps the existing Tower scene and its script GUID intact.
// Layout (TOWER_MODE_GDD 12.3-12.5): a top resource bar with the Alerts / Goals / Steward / Speed / Menu
// cluster on the right, and a five-slot dock: BUILD, PEOPLE, HEART (centre), EXPEDITIONS, BATTLE.
// Tap a dock button for its popup; press and hold it for a flyout of related shortcuts.
public sealed partial class TowerHud : MonoBehaviour
{
    private static readonly Color Ink = new Color(0.035f, 0.055f, 0.085f, 0.94f);
    private static readonly Color Panel = new Color(0.08f, 0.12f, 0.16f, 0.95f);
    private static readonly Color Glass = new Color(0.62f, 0.78f, 1f, 1f);
    private static readonly Color Gold = new Color(0.91f, 0.80f, 0.55f);
    private static readonly Color Cream = new Color(0.93f, 0.91f, 0.86f);
    private static readonly Color Teal = new Color(0.22f, 0.55f, 0.64f);
    private static readonly Color Alert = new Color(0.86f, 0.30f, 0.24f);

    private struct FlyItem
    {
        public string label; public Action action;
        public FlyItem(string label, Action action) { this.label = label; this.action = action; }
    }

    private AdamsHavenPrototype tower;
    private Canvas canvas;
    private Font font;
    private RectTransform safeRoot;

    // top bar
    private Text clockText, threatText, resources, materialsText;
    private Image threatFill, materialsPanel;
    private Button timeButton;
    private float materialsTimer;

    // dock, toast, placing chip
    private Button dockBuild, dockPeople, dockHeart, dockExpeditions, dockBattle;
    private Button alertsButton, goalsButton, stewardTop;
    private Image toastPanel, chipPanel;
    private Text toastText, chipText;
    private string shownMessage = "";
    private float toastTimer;

    // popups and flyout
    private Image popupBuild, popupFloors, popupTasks, popupMenu, popupGuild, flyoutPanel, flyoutCatcher;
    private Button guildMap;
    private Button guildSupplies, guildRelics, guildPatrol, guildRecall, guildOpen;
    private Text guildInfo, guildPerson;
    private int guildAutoOpened;
    private readonly Button[] flyoutButtons = new Button[6];
    private Text buildPageText, floorText, incidentText;
    private readonly Button[] categoryButtons = new Button[6];   // the sixth, DUNGEON, shows in Dungeon Mode only
    private readonly string[] categoryIds = { "all", "home", "produce", "store", "service", "dungeon" };
    private readonly string[] categoryNames = { "ALL", "HOMES", "PRODUCE", "STORE", "SERVICES", "DUNGEON" };
    private string buildCategory = "all";
    private Button floorOpenAbove, floorOpenBelow, floorWest, floorEast;
    private Button recruit, autoHaul, stewardButton, autoAssignButton;
    private readonly Text[] goalTexts = new Text[TowerRules.ActiveGoals];
    private readonly Button[] goalClaims = new Button[TowerRules.ActiveGoals];
    // Goals popup tabs: the rolling goals or today's daily board (TowerSigils.cs).
    private const int DailyRows = 1 + TowerRules.DailyPicked;
    private string tasksTab = "goals";
    private Button tabGoals, tabDaily;
    private Text dailyBonusText;
    private readonly Text[] dailyTexts = new Text[DailyRows];
    private readonly Button[] dailyClaims = new Button[DailyRows];

    // resident and room panels
    private Text residentDetail, roomDetail, roomAdvice, rosterPageText, priorityHint, moodText, adviceText;
    private readonly Button[] rosterButtons = new Button[4], buildButtons = new Button[6], priorityButtons = new Button[6];
    private readonly Text[] rosterLabels = new Text[4], buildLabels = new Text[6], priorityLabels = new Text[6];
    private Button collect, rush, upgrade, assign, exploreSupplies, exploreRelics, explorePatrol, recall;
    private Button craftTool, craftWeapon, familyPair, tutorialAction, scheduleButton;
    private Button moveRoom, demolishRoom;
    private int demolishArmedRoom;
    private float demolishArmedUntil;
    private Image tutorialPanel, saveOverlay, defeatOverlay, advicePanel;
    private Text defeatDetail;
    private Text tutorialTitle;
    private readonly Button[] heroButtons = new Button[3];
    private readonly Button[] storytellerButtons = new Button[3];   // founding panel, dormant Heart only
    private Text storytellerHint;
    private int rosterPage, buildPage, familyFirstId;
    private string leftTab = "work";
    private int lastScreenWidth, lastScreenHeight;
    private float restoreSpeed = 1;
    private readonly string[] priorityNames = { "production", "haul", "repair", "fire", "care", "defense" };
    private readonly string[] priorityLabelsShort = { "PROD", "HAUL", "REPAIR", "FIRE", "CARE", "DEFEND" };

    public void Initialize(AdamsHavenPrototype controller)
    {
        tower = controller;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 16);
        var canvasObject = new GameObject("Tower Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = Screen.width / (float)Screen.height < 16f / 9f ? 0f : 1f;
        if (EventSystem.current == null)
        {
            var events = new GameObject("Tower Event System", typeof(EventSystem));
            var module = events.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }
        safeRoot = Rect("Safe UI", canvas.transform, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, Color.clear).rectTransform;
        safeRoot.GetComponent<Image>().raycastTarget = false;
        BuildTop(); BuildLeft(); BuildRight(); BuildToast(); BuildDock(); BuildTutorial();
        BuildBuildPopup(); BuildFloorsPopup(); BuildTasksPopup(); BuildMenuPopup(); BuildGuildPopup();
        BuildHeartPopup(); BuildAlertsPopup(); BuildWorkPopup(); BuildDistrictsPopup();
        BuildAutoPopup(); BuildOutpostsPopup(); BuildSiegeBanner(); BuildLairPopup();
        BuildSaves(); BuildDefeat(); BuildFlyout(); BuildDev();
        BuildTownOverlay();   // last, so it sits above the rest of the canvas
        UpdateSafeArea();
        Refresh();
    }

    // ---------------------------------------------------------------- frame update

    private void Update()
    {
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight) UpdateSafeArea();
        if (tower == null) return;
        TickTown();
        TickNewGameConfirm();
        TickModeSwitch();
        if (toastTimer > 0)
        {
            toastTimer -= Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(toastTimer / 0.6f);
            toastPanel.color = new Color(0.03f, 0.05f, 0.08f, 0.86f * alpha);
            toastText.color = new Color(Cream.r, Cream.g, Cream.b, alpha);
            if (toastTimer <= 0) toastPanel.gameObject.SetActive(false);
        }
        if (materialsPanel.gameObject.activeSelf)
        {
            materialsTimer -= Time.unscaledDeltaTime;
            if (materialsTimer <= 0) materialsPanel.gameObject.SetActive(false);
        }
        PulseHeart();
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (flyoutPanel.gameObject.activeSelf) CloseFlyout();
            else if (AnyPopupOpen()) CloseAllPopups();
            else if (tower.Placing || tower.Moving) { tower.CancelPlacing(); Refresh(); }
        }
    }

    private void UpdateSafeArea()
    {
        lastScreenWidth = Screen.width; lastScreenHeight = Screen.height;
        canvas.GetComponent<CanvasScaler>().matchWidthOrHeight =
            Screen.width / (float)Screen.height < 16f / 9f ? 0f : 1f;
        Rect area = Screen.safeArea;
        safeRoot.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
        safeRoot.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
    }

    // ---------------------------------------------------------------- widgets

    private static Image Rect(string name, Transform parent, Vector2 min, Vector2 max,
        Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
        var image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    // A fixed-size box positioned by its anchor point and pivot.
    private static Image Box(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position,
        Vector2 size, Color color)
    {
        var image = Rect(name, parent, anchor, anchor, Vector2.zero, Vector2.zero, color);
        var rect = image.rectTransform;
        rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size;
        return image;
    }

    private Text TextAt(Transform parent, string name, string value, float x, float y,
        float width, float height, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        var label = go.GetComponent<Text>();
        label.font = font; label.fontSize = size; label.color = color;
        label.alignment = align; label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.text = value; label.raycastTarget = false;
        return label;
    }

    private Button ButtonAt(Transform parent, string name, string value, float x, float y,
        float width, float height, Action action, Color color, int fontSize = 16)
    {
        var image = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(x, -y - height), new Vector2(x + width, -y), color);
        var button = image.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.16f, 1.12f, 1.02f);
        colors.pressedColor = new Color(0.78f, 0.76f, 0.72f);
        colors.disabledColor = new Color(0.52f, 0.54f, 0.56f, 0.70f);
        colors.fadeDuration = 0.06f;
        button.colors = colors;
        TowerUiSkin.Apply(image, color);
        TowerUiSkin.StyleLabel(TextAt(image.transform, name + " label", value, 4, 0, width - 8, height,
            fontSize, Cream, TextAnchor.MiddleCenter));
        if (action != null) button.onClick.AddListener(() => action());
        return button;
    }

    // A dock/top button: tap runs `tap`, press-and-hold opens a flyout built from `items`.
    private Button HoldButtonAt(Transform parent, string name, string value, float x, float y, float width,
        float height, Action tap, Func<FlyItem[]> items, bool flyUp, Color color, int fontSize = 16)
    {
        var button = ButtonAt(parent, name, value, x, y, width, height, null, color, fontSize);
        var hold = button.gameObject.AddComponent<TowerHoldButton>();
        var bar = Rect("Hold progress", button.transform, Vector2.zero, Vector2.zero, Vector2.zero,
            Vector2.zero, new Color(1f, 0.86f, 0.5f, 0.95f));
        bar.raycastTarget = false;
        var barRect = bar.rectTransform;
        barRect.pivot = Vector2.zero; barRect.anchoredPosition = new Vector2(10, 5);
        barRect.sizeDelta = new Vector2(0, 4);
        hold.progress = barRect; hold.fullWidth = width - 20;
        for (int i = 0; i < 3; i++)
        {
            var dot = Box("Hold hint " + i, button.transform, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-9 - i * 6, -7), new Vector2(3.5f, 3.5f), new Color(1f, 0.88f, 0.55f, 0.85f));
            dot.raycastTarget = false;
        }
        var rect = button.GetComponent<RectTransform>();
        hold.onHold = () => ShowFlyout(rect, flyUp, items());
        button.onClick.AddListener(() => { if (hold.Consume()) return; tap(); });
        return button;
    }

    private Button CloseButton(Transform parent, float x, float y, Action action)
    {
        return ButtonAt(parent, "Close", "×", x, y, 30, 30, action, Teal, 19);
    }

    private static Text LabelOf(Button button)
    { return button.GetComponentInChildren<Text>(); }

    // A 1px hairline under a section title.
    private void Divider(Transform parent, float x, float y, float width)
    {
        var line = Rect("Divider", parent, new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(x + 4, -y - 8), new Vector2(x + width - 4, -y - 7), new Color(1, 1, 1, 0.12f));
        line.raycastTarget = false;
    }

    // ---------------------------------------------------------------- top bar

    private void BuildTop() { BuildTopChrome(); }

    private FlyItem[] TimeItems()
    {
        return new[] {
            new FlyItem("PAUSE", () => SetTime(0)),
            new FlyItem("1×  NORMAL", () => SetTime(1)),
            new FlyItem("2×  FAST", () => SetTime(2)),
            new FlyItem("4×  QUICK", () => SetTime(4)),
            new FlyItem("8×  TIME LAPSE", () => SetTime(8))
        };
    }

    private void SetTime(float speed)
    {
        if (speed > 0) restoreSpeed = speed;
        tower.SetSpeed(speed);
        Refresh();
    }

    private FlyItem[] MenuItems()
    {
        return new[] {
            new FlyItem("SAVE NOW", () => tower.SaveNow()),
            new FlyItem("CHECKPOINTS", () => saveOverlay.gameObject.SetActive(true))
        };
    }

    // ---------------------------------------------------------------- dock

    private void BuildToast()
    {
        toastPanel = Box("Toast", safeRoot, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 106),
            new Vector2(600, 30), new Color(0.03f, 0.05f, 0.08f, 0.86f));
        Pill(toastPanel, 0.86f).raycastTarget = false;
        toastText = TextAt(toastPanel.transform, "Toast text", "", 8, 0, 584, 30, 13, Cream, TextAnchor.MiddleCenter);
        toastPanel.gameObject.SetActive(false);

        chipPanel = Box("Placing chip", safeRoot, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 142),
            new Vector2(560, 38), new Color(0.10f, 0.07f, 0.03f, 0.92f));
        TowerUiSkin.ApplyPanel(chipPanel, new Color(1f, 0.85f, 0.55f), true);
        chipText = TextAt(chipPanel.transform, "Placing text", "", 14, 0, 400, 38, 14, Cream);
        ButtonAt(chipPanel.transform, "Cancel placing", "CANCEL", 438, 4, 110, 30,
            () => { tower.CancelPlacing(); Refresh(); }, Alert, 13);
        chipPanel.gameObject.SetActive(false);
    }

    private void BuildDock() { BuildDockChrome(); }

    private FlyItem[] BuildItems()
    {
        return new[] {
            new FlyItem("HOMES", () => OpenBuild("home")),
            new FlyItem("PRODUCTION", () => OpenBuild("produce")),
            new FlyItem("STORAGE", () => OpenBuild("store")),
            new FlyItem("SERVICES", () => OpenBuild("service")),
            new FlyItem("FLOORS AND DIGGING", () => TogglePopup(popupFloors)),
            new FlyItem("DISTRICTS AND ZONING", OpenDistricts)
        };
    }

    private FlyItem[] PeopleItems()
    {
        return new[] {
            new FlyItem("WORK GRID (ALL)", OpenWorkGrid),
            new FlyItem("WORK PRIORITIES", () => OpenPeople("work")),
            new FlyItem("MOODS", () => OpenPeople("mood")),
            new FlyItem("GUILD EXPEDITIONS", OpenGuildBoard),
            new FlyItem("FAMILY", () => OpenPeople("family"))
        };
    }

    private FlyItem[] TaskItems()
    {
        return new[] {
            new FlyItem("CLAIM ALL GOALS AND DAILIES", ClaimAll),
            new FlyItem("RECRUIT AT GATE", () => tower.Apply(tower.Rules.RecruitVisitor())),
            new FlyItem("UNLOCK AUTO-HAUL", () => tower.Apply(tower.Rules.UnlockHauling())),
            new FlyItem("AUTO-ASSIGN IDLE", () => tower.Apply(tower.Rules.AutoAssignIdle())),
            new FlyItem(tower.Rules.State.steward ? "STEWARD: ON" : "STEWARD: OFF",
                () => tower.Apply(tower.Rules.SetSteward(!tower.Rules.State.steward)))
        };
    }

    private int HighestFloor()
    {
        int top = 0;
        foreach (var f in tower.Rules.State.floors) top = Mathf.Max(top, f.number);
        return top;
    }

    private void ClaimAll()
    {
        for (int i = tower.Rules.State.goals.Count - 1; i >= 0; i--)
            if (tower.Rules.GoalComplete(tower.Rules.State.goals[i])) tower.Apply(tower.Rules.ClaimGoal(i));
        for (int i = 0; i < tower.Rules.State.daily.Count; i++)
            if (!tower.Rules.State.daily[i].claimed && tower.Rules.DailyComplete(tower.Rules.State.daily[i]))
                tower.Apply(tower.Rules.ClaimDaily(i));
    }

    private void TogglePeople()
    {
        var director = tower.GetComponent<TowerArtDirector>();
        if (director != null) director.ToggleResidents();
        CloseAllPopups();
    }

    private void OpenPeople(string tab)
    {
        leftTab = tab;
        var director = tower.GetComponent<TowerArtDirector>();
        if (director != null) director.SetResidentsOpen(true);
        Refresh();
    }

    // ---------------------------------------------------------------- flyout and popups

    private void BuildFlyout()
    {
        flyoutCatcher = Rect("Flyout catcher", safeRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Color(0, 0, 0, 0.01f));
        var catcherButton = flyoutCatcher.gameObject.AddComponent<Button>();
        catcherButton.transition = Selectable.Transition.None;
        catcherButton.onClick.AddListener(CloseFlyout);
        flyoutPanel = Box("Flyout", safeRoot, Vector2.zero, new Vector2(0.5f, 0), Vector2.zero,
            new Vector2(200, 100), Glass);
        TowerUiSkin.ApplyPanel(flyoutPanel, Glass, true);
        for (int i = 0; i < flyoutButtons.Length; i++)
            flyoutButtons[i] = ButtonAt(flyoutPanel.transform, "Flyout item " + i, "", 8, 8 + i * 46, 184, 40,
                null, Teal, 15);
        flyoutCatcher.gameObject.SetActive(false);
        flyoutPanel.gameObject.SetActive(false);
    }

    private void ShowFlyout(RectTransform anchor, bool up, FlyItem[] items)
    {
        CloseAllPopups();
        int count = Mathf.Min(items.Length, flyoutButtons.Length);
        flyoutCatcher.transform.SetAsLastSibling();
        flyoutPanel.transform.SetAsLastSibling();
        var rect = flyoutPanel.rectTransform;
        rect.sizeDelta = new Vector2(200, 16 + count * 46 - 6);
        var corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        Vector3 point = up ? (corners[1] + corners[2]) * 0.5f : (corners[0] + corners[3]) * 0.5f;
        Vector3 local = safeRoot.InverseTransformPoint(point);
        float x = Mathf.Clamp(local.x - safeRoot.rect.xMin, 108, safeRoot.rect.width - 108);
        float y = local.y - safeRoot.rect.yMin + (up ? 8 : -8);
        rect.pivot = new Vector2(0.5f, up ? 0 : 1);
        rect.anchoredPosition = new Vector2(x, y);
        for (int i = 0; i < flyoutButtons.Length; i++)
        {
            var button = flyoutButtons[i];
            button.gameObject.SetActive(i < count);
            if (i >= count) continue;
            // The item nearest the pressed button comes first, whichever way the flyout opens.
            int slot = up ? count - 1 - i : i;
            var buttonRect = button.GetComponent<RectTransform>();
            buttonRect.anchoredPosition = new Vector2(8, -8 - slot * 46 - 40);
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0, 1);
            buttonRect.pivot = new Vector2(0, 0);
            buttonRect.sizeDelta = new Vector2(184, 40);
            LabelOf(button).text = items[i].label;
            var action = items[i].action;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { CloseFlyout(); action(); });
        }
        flyoutCatcher.gameObject.SetActive(true);
        flyoutPanel.gameObject.SetActive(true);
    }

    private void CloseFlyout()
    {
        if (flyoutPanel == null) return;
        flyoutCatcher.gameObject.SetActive(false);
        flyoutPanel.gameObject.SetActive(false);
    }

    private Image MakePopup(string name, float width, float height, bool top)
    {
        Image popup = top ?
            Box(name, safeRoot, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-10, -58), new Vector2(width, height), Glass) :
            Box(name, safeRoot, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 94), new Vector2(width, height), Glass);
        TowerUiSkin.ApplyPanel(popup, Glass, true);
        popup.gameObject.SetActive(false);
        return popup;
    }

    private bool AnyPopupOpen()
    {
        return popupBuild.gameObject.activeSelf || popupFloors.gameObject.activeSelf ||
            popupTasks.gameObject.activeSelf || popupMenu.gameObject.activeSelf ||
            popupHeart.gameObject.activeSelf || popupAlerts.gameObject.activeSelf ||
            popupGuild.gameObject.activeSelf || popupWork.gameObject.activeSelf ||
            popupDistricts.gameObject.activeSelf || popupAuto.gameObject.activeSelf ||
            popupOutposts.gameObject.activeSelf || popupLair != null && popupLair.gameObject.activeSelf;
    }

    private void HidePopups()
    {
        popupBuild.gameObject.SetActive(false); popupFloors.gameObject.SetActive(false);
        popupTasks.gameObject.SetActive(false); popupMenu.gameObject.SetActive(false);
        popupHeart.gameObject.SetActive(false); popupAlerts.gameObject.SetActive(false);
        popupGuild.gameObject.SetActive(false); popupWork.gameObject.SetActive(false);
        popupDistricts.gameObject.SetActive(false); popupAuto.gameObject.SetActive(false);
        popupOutposts.gameObject.SetActive(false);
        if (popupLair != null) popupLair.gameObject.SetActive(false);
    }

    private void CloseAllPopups()
    {
        HidePopups();
        Refresh();
    }

    private void TogglePopup(Image popup)
    {
        bool open = !popup.gameObject.activeSelf;
        HidePopups();
        popup.gameObject.SetActive(open);
        if (open) popup.transform.SetAsLastSibling();
        Refresh();
    }

    private void OpenBuild(string category)
    {
        buildCategory = category; buildPage = 0;
        popupBuild.gameObject.SetActive(false);
        TogglePopup(popupBuild);
    }

    private void BuildBuildPopup()
    {
        popupBuild = MakePopup("Build popup", 690, 262, false);
        for (int i = 0; i < categoryButtons.Length; i++)
        {
            int index = i;
            float pitch = TowerModes.IsLair ? 96 : 116;
            categoryButtons[i] = ButtonAt(popupBuild.transform, "Category " + categoryIds[i], categoryNames[i],
                14 + i * pitch, 12, pitch - 6, 32, () => { buildCategory = categoryIds[index]; buildPage = 0; Refresh(); },
                Teal, 13);
        }
        categoryButtons[5].gameObject.SetActive(TowerModes.IsLair);
        CloseButton(popupBuild.transform, 648, 12, CloseAllPopups);
        for (int i = 0; i < buildButtons.Length; i++)
        {
            int index = i;
            buildButtons[i] = ButtonAt(popupBuild.transform, "Build choice " + i, "", 14 + (i % 3) * 224,
                56 + (i / 3) * 66, 216, 58, () => ChooseBuild(index), Teal, 13);
            buildLabels[i] = LabelOf(buildButtons[i]);
        }
        ButtonAt(popupBuild.transform, "Previous rooms", "‹", 14, 216, 56, 34,
            () => { buildPage = Mathf.Max(0, buildPage - 1); Refresh(); }, Teal, 22);
        ButtonAt(popupBuild.transform, "Floors page", "FLOORS / DIG", 76, 216, 150, 34,
            () => TogglePopup(popupFloors), Teal, 13);
        buildPageText = TextAt(popupBuild.transform, "Build page", "", 232, 216, 382, 34, 14, Cream,
            TextAnchor.MiddleCenter);
        ButtonAt(popupBuild.transform, "Next rooms", "›", 620, 216, 56, 34,
            () => { buildPage++; Refresh(); }, Teal, 22);
    }

    private void BuildFloorsPopup()
    {
        popupFloors = MakePopup("Floors popup", 470, 296, false);
        TextAt(popupFloors.transform, "Floors title", "FLOORS AND FOUNDATIONS", 16, 10, 380, 28, 17, Gold);
        CloseButton(popupFloors.transform, 428, 10, CloseAllPopups);
        ButtonAt(popupFloors.transform, "Floor down", "FLOOR −", 14, 46, 130, 42,
            () => tower.FocusOnFloor(tower.FocusFloor - 1), Teal, 15);
        floorText = TextAt(popupFloors.transform, "Floor", "", 150, 46, 170, 42, 18, Cream, TextAnchor.MiddleCenter);
        ButtonAt(popupFloors.transform, "Floor up", "FLOOR +", 326, 46, 130, 42,
            () => tower.FocusOnFloor(tower.FocusFloor + 1), Teal, 15);
        floorOpenAbove = ButtonAt(popupFloors.transform, "Open above", "OPEN ABOVE", 14, 98, 218, 46,
            () => tower.Apply(tower.Rules.OpenFloor(tower.FocusFloor + 1)), Teal, 14);
        floorOpenBelow = ButtonAt(popupFloors.transform, "Open below", "OPEN BELOW", 238, 98, 218, 46,
            () => tower.Apply(tower.Rules.LairFounded ? tower.Rules.LairAddLivingFloor() :
                tower.Rules.OpenFloor(tower.FocusFloor - 1)), Teal, 14);
        floorWest = ButtonAt(popupFloors.transform, "Expand west", "< WEST", 14, 152, 218, 46,
            () => ExpandFocused(-1), Teal, 14);
        floorEast = ButtonAt(popupFloors.transform, "Expand east", "EAST >", 238, 152, 218, 46,
            () => ExpandFocused(1), Teal, 14);
        floorEast.gameObject.SetActive(false);   // the Heart and Gate are the right edge: no east wing
        ButtonAt(popupFloors.transform, "Jump ground", "GROUND", 14, 206, 218, 36,
            () => tower.FocusOnFloor(0), Teal, 13);
        ButtonAt(popupFloors.transform, "Jump top", "TOP FLOOR", 238, 206, 218, 36,
            () => tower.FocusOnFloor(HighestFloor()), Teal, 13);
        ButtonAt(popupFloors.transform, "Districts", "DISTRICTS AND ZONING", 14, 250, 442, 36, OpenDistricts, Violet, 13);
    }

    private void BuildTasksPopup()
    {
        popupTasks = MakePopup("Tasks popup", 580, 428, false);
        tabGoals = ButtonAt(popupTasks.transform, "Tab goals", "GOALS", 14, 6, 126, 30, () => tasksTab = "goals", Gold, 14);
        tabDaily = ButtonAt(popupTasks.transform, "Tab daily", "DAILY", 146, 6, 126, 30, () => tasksTab = "daily", Teal, 14);
        dailyBonusText = TextAt(popupTasks.transform, "Daily bonus", "", 284, 10, 244, 26, 13, Cream);
        CloseButton(popupTasks.transform, 538, 10, CloseAllPopups);
        Divider(popupTasks.transform, 12, 40, 556);
        for (int i = 0; i < goalTexts.Length; i++)
        {
            int index = i;
            goalTexts[i] = TextAt(popupTasks.transform, "Goal " + i, "", 16, 56 + i * 56, 420, 48, 14, Cream);
            goalClaims[i] = ButtonAt(popupTasks.transform, "Claim goal " + i, "CLAIM", 452, 58 + i * 56,
                112, 40, () => tower.Apply(tower.Rules.ClaimGoal(index)), Teal, 14);
        }
        for (int i = 0; i < dailyTexts.Length; i++)
        {
            int index = i;
            dailyTexts[i] = TextAt(popupTasks.transform, "Daily " + i, "", 16, 50 + i * 42, 420, 38, 14, Cream);
            dailyClaims[i] = ButtonAt(popupTasks.transform, "Claim daily " + i, "CLAIM", 452, 52 + i * 42,
                112, 34, () => tower.Apply(tower.Rules.ClaimDaily(index)), Teal, 14);
        }
        TextAt(popupTasks.transform, "Events title", "TOWER EVENTS", 16, 226, 300, 26, 16, Gold);
        incidentText = TextAt(popupTasks.transform, "Incident list", "", 16, 252, 548, 58, 14, Cream);
        recruit = ButtonAt(popupTasks.transform, "Recruit visitor", "RECRUIT AT GATE", 14, 318, 274, 46,
            () => tower.Apply(tower.Rules.RecruitVisitor()), Teal, 15);
        autoHaul = ButtonAt(popupTasks.transform, "Unlock hauling", "AUTO-HAUL", 294, 318, 274, 46,
            () => tower.Apply(tower.Rules.UnlockHauling()), Teal, 15);
        stewardButton = ButtonAt(popupTasks.transform, "Steward", "STEWARD", 14, 370, 274, 46,
            () => tower.Apply(tower.Rules.SetSteward(!tower.Rules.State.steward)), Teal, 15);
        autoAssignButton = ButtonAt(popupTasks.transform, "Auto assign", "AUTO-ASSIGN IDLE", 294, 370, 274, 46,
            () => tower.Apply(tower.Rules.AutoAssignIdle()), Teal, 15);
    }

    // ---------------------------------------------------------------- guild expedition board

    private void OpenGuildBoard()
    {
        string error = tower.Rules.GuildRequired();
        if (error != null) { tower.Apply(error); return; }
        HidePopups();
        popupGuild.gameObject.SetActive(true);
        popupGuild.transform.SetAsLastSibling();
        Refresh();
    }

    private void BuildGuildPopup()
    {
        popupGuild = MakePopup("Guild popup", 580, 296, false);
        var t = popupGuild.transform;
        TextAt(t, "Guild title", "SILVERBROOK ADVENTURE GUILD", 16, 10, 480, 28, 18, Gold);
        CloseButton(t, 538, 10, CloseAllPopups);
        Divider(t, 12, 40, 556);
        guildInfo = TextAt(t, "Guild info", "", 16, 46, 548, 26, 14, Cream);
        guildMap = ButtonAt(t, "Expedition map", "OPEN THE EXPEDITION MAP", 14, 76, 360, 74,
            () => { CloseAllPopups(); tower.OpenExpedition(); }, Teal, 17);
        ButtonAt(t, "Auto expedition", "AUTO EXPEDITION", 382, 76, 184, 35, OpenAuto, Teal, 13);
        ButtonAt(t, "Outposts", "OUTPOSTS", 382, 115, 184, 35, OpenOutposts, Violet, 13);
        TextAt(t, "Guild resident title", "SEND A RESIDENT", 16, 160, 300, 26, 16, Gold);
        guildPerson = TextAt(t, "Guild resident", "", 16, 186, 548, 24, 14, Cream);
        guildSupplies = ButtonAt(t, "Guild supplies", "SUPPLIES", 14, 220, 132, 46, () => Explore("supplies"), Teal, 15);
        guildRelics = ButtonAt(t, "Guild relics", "RELICS", 154, 220, 132, 46, () => Explore("relics"), Teal, 15);
        guildPatrol = ButtonAt(t, "Guild patrol", "PATROL", 294, 220, 132, 46, () => Explore("patrol"), Teal, 15);
        guildRecall = ButtonAt(t, "Guild recall", "RECALL", 434, 220, 132, 46,
            () => { if (tower.SelectedPerson != null) tower.Apply(tower.Rules.Recall(tower.SelectedPerson.id)); },
            Alert, 15);
    }

    private void RefreshGuild()
    {
        var rules = tower.Rules;
        var run = rules.Run;
        guildInfo.text = run != null ? "An expedition is under way in " + TowerRules.Region(run.region).name + "." :
            "Regions conquered " + rules.State.regionsConquered.Count + " / " + TowerRules.Regions.Length +
            "  •  plan a party and venture into Silverbrook Forest.";
        LabelOf(guildMap).text = run != null ? "RESUME EXPEDITION" : "OPEN THE EXPEDITION MAP";
        var person = tower.SelectedPerson;
        guildPerson.text = person == null ? "Select a resident in PEOPLE first." :
            person.name + (TowerRules.IsPosted(person) ? " is " + TowerRules.PostingLabel(person) + "." :
                person.exploring ? " is away on an expedition." : person.ageStage != 0 ? " is too young." :
                person.downed ? " is down." : " is ready.");
        bool ready = person != null && person.ageStage == 0 && !person.exploring && !person.downed;
        guildSupplies.interactable = guildRelics.interactable = guildPatrol.interactable = ready;
        guildRecall.interactable = person != null && person.exploring;
    }

    private void BuildMenuPopup() { BuildMainMenu(); }

    private void ExpandFocused(int side)
    {
        int floor = tower.FocusFloor;
        var opened = tower.Rules.Floor(floor);
        if (opened != null && floor < 0) tower.Rules.Excavate(floor, tower.Rules.NextExpansionX(floor, side));
        tower.Apply(tower.Rules.ExpandFloor(floor, side));
    }

    // ---------------------------------------------------------------- resident and room panels

    private void BuildLeft()
    {
        var left = Rect("Resident panel", safeRoot, new Vector2(0, 0), new Vector2(0, 1),
            new Vector2(0, 8), new Vector2(294, -104), Panel);
        TowerUiSkin.ApplyPanel(left, Glass, true);
        TextAt(left.transform, "Resident heading", "RESIDENTS", 14, 10, 150, 32, 22, Gold);
        Divider(left.transform, 9, 39, 279);
        ButtonAt(left.transform, "Previous residents", "‹", 179, 10, 45, 36,
            () => { rosterPage = Mathf.Max(0, rosterPage - 1); Refresh(); }, Teal, 23);
        ButtonAt(left.transform, "Next residents", "›", 232, 10, 45, 36,
            () => { rosterPage++; Refresh(); }, Teal, 23);
        rosterPageText = TextAt(left.transform, "Roster page", "", 12, 53, 275, 20, 13, Cream);
        for (int i = 0; i < rosterButtons.Length; i++)
        {
            int row = i;
            rosterButtons[i] = ButtonAt(left.transform, "Resident row " + i, "", 12,
                75 + i * 44, 270, 39, () => ClickResidentRow(row), Teal, 14);
            rosterLabels[i] = LabelOf(rosterButtons[i]);
            var drag = rosterButtons[i].gameObject.AddComponent<TowerResidentDrag>();
            drag.OnDrop = position => DragResidentRow(row, position);
        }
        residentDetail = TextAt(left.transform, "Resident details", "", 14, 254, 267, 102, 14, Cream);
        ButtonAt(left.transform, "Work tab", "WORK", 12, 360, 66, 38,
            () => { leftTab = "work"; Refresh(); }, Teal, 13);
        ButtonAt(left.transform, "Explore tab", "GO", 82, 360, 66, 38,
            () => { leftTab = "explore"; Refresh(); }, Teal, 13);
        ButtonAt(left.transform, "Family tab", "FAMILY", 152, 360, 66, 38,
            () => { leftTab = "family"; Refresh(); }, Teal, 12);
        ButtonAt(left.transform, "Mood tab", "MOOD", 222, 360, 66, 38,
            () => { leftTab = "mood"; Refresh(); }, Teal, 13);
        moodText = TextAt(left.transform, "Mood thoughts", "", 12, 402, 270, 74, 12, Cream);
        scheduleButton = ButtonAt(left.transform, "Schedule", "SCHEDULE", 11, 478, 270, 26,
            CycleSchedule, Teal, 13);
        var work = Rect("Work controls", left.transform, new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(0, -506), new Vector2(294, -403), Color.clear);
        work.GetComponent<Image>().raycastTarget = false;
        for (int i = 0; i < priorityButtons.Length; i++)
        {
            int index = i;
            priorityButtons[i] = ButtonAt(work.transform, "Priority " + priorityNames[i], "",
                10 + (i % 3) * 94, 2 + (i / 3) * 47, 88, 41,
                () => CyclePriority(index), Teal, 13);
            priorityLabels[i] = LabelOf(priorityButtons[i]);
        }
        priorityHint = TextAt(left.transform, "Context hint", "", 12, 402, 270, 49, 14, Cream);
        priorityHint.gameObject.SetActive(false);
        exploreSupplies = ButtonAt(left.transform, "Supplies expedition", "SUPPLIES", 11, 406,
            86, 42, () => Explore("supplies"), Teal, 13);
        exploreRelics = ButtonAt(left.transform, "Relics expedition", "RELICS", 103, 406,
            86, 42, () => Explore("relics"), Teal, 13);
        explorePatrol = ButtonAt(left.transform, "Battle expedition", "BATTLE", 195, 406,
            86, 42, OpenGuildBoard, Teal, 13);
        recall = ButtonAt(left.transform, "Recall", "RECALL", 11, 454, 86, 43,
            () => { if (tower.SelectedPerson != null) tower.Apply(tower.Rules.Recall(tower.SelectedPerson.id)); },
            Alert, 14);
        craftTool = ButtonAt(left.transform, "Craft tool", "+ TOOL", 103, 454, 86, 43,
            () => Craft("tool"), Teal, 13);
        craftWeapon = ButtonAt(left.transform, "Craft weapon", "+ WEAPON", 195, 454, 86, 43,
            () => Craft("weapon"), Teal, 12);
        familyPair = ButtonAt(left.transform, "Pair family", "FORM FAMILY", 11, 455, 270, 45,
            StartFamily, Teal, 17);
    }

    private void BuildRight()
    {
        var right = Rect("Room panel", safeRoot, new Vector2(1, 0), new Vector2(1, 1),
            new Vector2(-300, 8), new Vector2(0, -104), Panel);
        TowerUiSkin.ApplyPanel(right, Glass, true);
        TextAt(right.transform, "Room heading", "SELECTED ROOM", 15, 11, 250, 32, 22, Gold);
        CloseButton(right.transform, 262, 12, () =>
        {
            var director = tower.GetComponent<TowerArtDirector>();
            if (director != null) director.SetRoomOpen(false);
        });
        Divider(right.transform, 10, 38, 280);
        roomDetail = TextAt(right.transform, "Room details", "", 16, 52, 267, 87, 16, Cream);
        collect = ButtonAt(right.transform, "Collect", "COLLECT", 15, 145, 128, 45,
            () => RoomAction(r => tower.Rules.Collect(r.uid)), Teal, 16);
        rush = ButtonAt(right.transform, "Rush", "RUSH", 155, 145, 130, 45,
            () => RoomAction(r => tower.Rules.Rush(r.uid)), Alert, 16);
        upgrade = ButtonAt(right.transform, "Upgrade", "UPGRADE", 15, 198, 128, 45,
            () => RoomAction(r => tower.Rules.UpgradeRoom(r.uid)), Teal, 16);
        assign = ButtonAt(right.transform, "Assign", "ASSIGN", 155, 198, 130, 45,
            () => { if (tower.SelectedRoom != null) tower.AssignSelectedToRoom(tower.SelectedRoom.uid); },
            Teal, 16);
        roomAdvice = TextAt(right.transform, "Assignment advice", "", 15, 252, 270, 110, 14, Cream);
        guildOpen = ButtonAt(right.transform, "Guild expeditions", "EXPEDITIONS", 15, 366, 270, 46,
            OpenGuildBoard, Teal, 16);
        guildOpen.gameObject.SetActive(false);
        moveRoom = ButtonAt(right.transform, "Move room", "MOVE", 15, 418, 128, 40,
            () => { if (tower.SelectedRoom != null) tower.BeginMove(tower.SelectedRoom.uid); }, Teal, 15);
        demolishRoom = ButtonAt(right.transform, "Demolish room", "DEMOLISH", 155, 418, 130, 40,
            ConfirmDemolish, Alert, 14);
    }

    // DEMOLISH asks for a second tap within three seconds, like NEW GAME.
    private void ConfirmDemolish()
    {
        var room = tower.SelectedRoom;
        if (room == null) return;
        if (demolishArmedRoom != room.uid || Time.unscaledTime > demolishArmedUntil)
        {
            demolishArmedRoom = room.uid;
            demolishArmedUntil = Time.unscaledTime + 3f;
            LabelOf(demolishRoom).text = "TAP TO CONFIRM";
            return;
        }
        demolishArmedRoom = 0;
        tower.Apply(tower.Rules.Demolish(room.uid));
    }

    // ---------------------------------------------------------------- tutorial, overlays

    private void BuildTutorial()
    {
        tutorialPanel = Rect("Tutorial", safeRoot, new Vector2(0.26f, 1), new Vector2(0.74f, 1),
            new Vector2(0, -178), new Vector2(0, -86), Ink);
        TowerUiSkin.ApplyPanel(tutorialPanel, Glass, true);
        tutorialTitle = TextAt(tutorialPanel.transform, "Tutorial title", "", 9, 2, 592, 38,
            20, Gold, TextAnchor.MiddleCenter);
        tutorialAction = ButtonAt(tutorialPanel.transform, "Tutorial action", "", 139, 43,
            325, 45, () =>
            {
                string phase = tower.Rules.State.introPhase;
                tower.Apply(phase == "dormant" ? tower.Rules.AwakenHeart() : tower.Rules.PlaceIntroGate());
            }, Teal, 17);
        string[] ids = { "kaela", "ghislaine", "elara" };
        for (int i = 0; i < 3; i++)
        {
            string id = ids[i];
            heroButtons[i] = ButtonAt(tutorialPanel.transform, "Starter " + id, id.ToUpperInvariant(),
                22 + i * 194, 44, 181, 44, () => tower.Apply(tower.Rules.ChooseStarter(id)), Teal, 15);
        }
        BuildLairFounding(tutorialPanel.transform);
        // RimWorld's storyteller pick, offered with the dormant Heart (optional: AWAKEN HEART stays the only step).
        for (int i = 0; i < storytellerButtons.Length; i++)
        {
            string id = TowerRules.Storytellers[i].id;
            storytellerButtons[i] = ButtonAt(tutorialPanel.transform, "Storyteller " + id, id.ToUpperInvariant(),
                22 + i * 194, 94, 181, 36, () => tower.Apply(tower.Rules.SetStoryteller(id)), Teal, 14);
        }
        storytellerHint = TextAt(tutorialPanel.transform, "Storyteller hint", "", 40, 132, 530, 22, 12, Cream,
            TextAnchor.MiddleCenter);
        advicePanel = Box("Advice pill", safeRoot, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0, -86), new Vector2(600, 28), new Color(0.10f, 0.06f, 0.03f, 0.88f));
        Pill(advicePanel, 0.88f).raycastTarget = false;
        adviceText = TextAt(advicePanel.transform, "Advice", "", 10, 0, 580, 28, 14,
            new Color(1f, 0.82f, 0.5f), TextAnchor.MiddleCenter);
    }

    private void BuildSaves()
    {
        saveOverlay = Rect("Checkpoint overlay", safeRoot, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, new Color(0.01f, 0.02f, 0.04f, 0.92f));
        var card = Rect("Checkpoint card", saveOverlay.transform,
            new Vector2(0.22f, 0.14f), new Vector2(0.78f, 0.86f), Vector2.zero, Vector2.zero, Panel);
        TowerUiSkin.ApplyPanel(card, Glass, true);
        TextAt(card.transform, "Checkpoint title", TowerModes.IsLair ? "DUNGEON SAVES" : "TOWER CHECKPOINTS", 28, 19, 340, 44,
            27, Gold);
        // Slot 0 is the NEW GAME save; this reopens it without wiping it.
        ButtonAt(card.transform, "Continue new game", "MY NEW GAME", 380, 18, 175, 43, () =>
        {
            if (!System.IO.File.Exists(TowerSaveFiles.PathFor(0))) { tower.Apply("No new game yet: use MENU > NEW GAME."); return; }
            tower.LoadCheckpoint(0); saveOverlay.gameObject.SetActive(false); Refresh();
        }, Gold, 14);
        ButtonAt(card.transform, "Close checkpoints", "CLOSE", 570, 18, 130, 43,
            () => { saveOverlay.gameObject.SetActive(false); Refresh(); }, Alert, 16);
        for (int i = 0; i < 10; i++)
        {
            int slot = i + 1;
            ButtonAt(card.transform, "Slot " + slot, TowerMilestones.SlotCaption(slot), 27 + i % 2 * 340, 79 + i / 2 * 77, 323, 65,
                () => { tower.LoadCheckpoint(slot); saveOverlay.gameObject.SetActive(false); Refresh(); }, Teal, 15);
        }
        saveOverlay.gameObject.SetActive(false);
    }

    private void BuildDefeat()
    {
        defeatOverlay = Rect("Heart defeat", safeRoot, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, new Color(0.025f, 0.025f, 0.045f, 0.94f));
        var card = Rect("Defeat card", defeatOverlay.transform,
            new Vector2(0.22f, 0.26f), new Vector2(0.78f, 0.74f), Vector2.zero, Vector2.zero, Panel);
        TowerUiSkin.ApplyPanel(card, Glass, true);
        TextAt(card.transform, "Defeat title", "THE CELESTIUM HEART HAS FALLEN", 20, 23,
            600, 54, 28, Gold, TextAnchor.MiddleCenter);
        defeatDetail = TextAt(card.transform, "Defeat detail", "This run has ended. Begin a new run: your heroes and Sigils carry over, the tower starts again.",
            32, 85, 575, 130, 16, Cream, TextAnchor.MiddleCenter);
        ButtonAt(card.transform, "Choose checkpoint", "LOAD CHECKPOINT", 32, 232, 260, 58,
            () => { saveOverlay.gameObject.SetActive(true); defeatOverlay.gameObject.SetActive(false); }, Teal, 17);
        ButtonAt(card.transform, "Restart current slot", "NEW RUN (LEGACY BONUS)", 313, 232, 280, 58,
            () => tower.RestartCurrentSlot(), Alert, 17);
        defeatOverlay.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- actions

    private void CycleSchedule()
    {
        var person = tower.SelectedPerson;
        if (person == null) return;
        string next = person.schedule == "day" ? "night" : person.schedule == "night" ? "flexible" : "day";
        tower.Apply(tower.Rules.SetSchedule(person.id, next));
    }

    private void ClickResidentRow(int row)
    {
        int index = rosterPage * 4 + row;
        if (index >= tower.Rules.State.residents.Count) return;
        int id = tower.Rules.State.residents[index].id;
        if (familyFirstId > 0 && familyFirstId != id)
        {
            tower.Apply(tower.Rules.PairFamily(familyFirstId, id));
            familyFirstId = 0;
        }
        tower.SelectPerson(id);
    }

    private void DragResidentRow(int row, Vector2 position)
    {
        int index = rosterPage * 4 + row;
        if (index < tower.Rules.State.residents.Count)
            tower.DragAssign(tower.Rules.State.residents[index].id, position);
    }

    private void ChooseBuild(int index)
    {
        var available = BuildChoices();
        int number = buildPage * 6 + index;
        if (number >= available.Count) return;
        string id = available[number];
        if (!tower.Rules.State.blueprints.Contains(id))
        {
            // Locked buildings open the Construction node that unlocks them.
            var node = TowerRules.UnlockNode(id);
            if (node != null) { OpenResearch(node.id); return; }
            string result = tower.Rules.ResearchBlueprint(id);
            tower.Apply(result);
            if (result != null) return;
            Refresh();
            return;
        }
        popupBuild.gameObject.SetActive(false);
        tower.SelectBuildType(id);
    }

    private static string CategoryOf(TowerRoomDef def)
    {
        switch (def.kind)
        {
            case "living": return "home";
            case "produce": return "produce";
            case "storage": return "store";
            case "lair": return "dungeon";
            default: return "service";
        }
    }

    private List<string> BuildChoices()
    {
        var choices = new List<string>();
        foreach (var def in tower.Rules.BuildableDefs())
            if (def.kind != "heart" && def.kind != "gate" && tower.Rules.State.blueprints.Contains(def.id) &&
                (buildCategory == "all" || CategoryOf(def) == buildCategory)) choices.Add(def.id);
        if (tower.Rules.State.introPhase != "complete") return choices;
        foreach (var def in tower.Rules.BuildableDefs())
            if (def.kind != "heart" && def.kind != "gate" && !tower.Rules.State.blueprints.Contains(def.id) &&
                (buildCategory == "all" || CategoryOf(def) == buildCategory)) choices.Add(def.id);
        return choices;
    }

    private void CyclePriority(int index)
    {
        var person = tower.SelectedPerson;
        if (person == null) return;
        int current = Priority(person, priorityNames[index]);
        tower.Apply(tower.Rules.SetPriority(person.id, priorityNames[index], (current + 1) % 4));
    }

    private static int Priority(TowerResident resident, string key)
    {
        switch (key)
        {
            case "production": return resident.priorityProduction;
            case "haul": return resident.priorityHaul;
            case "repair": return resident.priorityRepair;
            case "fire": return resident.priorityFire;
            case "care": return resident.priorityCare;
            case "defense": return resident.priorityDefense;
            default: return 0;
        }
    }

    private void Explore(string choice)
    {
        var person = tower.SelectedPerson;
        string error = tower.Rules.GuildRequired();
        if (error != null) { tower.Apply(error); return; }
        if (person != null) tower.Apply(tower.Rules.SendExploring(person.id, choice));
    }

    private void Craft(string kind)
    {
        var person = tower.SelectedPerson;
        if (person != null) tower.Apply(tower.Rules.CraftEquipment(person.id, kind));
    }

    private void StartFamily()
    {
        var person = tower.SelectedPerson;
        if (person == null) return;
        familyFirstId = person.id;
        leftTab = "family";
        Refresh();
    }

    private void RoomAction(Func<TowerRoom, string> action)
    {
        var room = tower.SelectedRoom;
        if (room != null) tower.Apply(action(room));
    }

    // ---------------------------------------------------------------- refresh

    public void Refresh()
    {
        if (tower == null || tower.Rules == null) return;
        RefreshTown();
        var state = tower.Rules.State;
        defeatOverlay.gameObject.SetActive(state.defeated && !saveOverlay.gameObject.activeSelf);
        if (state.defeated && defeatDetail != null)
            defeatDetail.text = "This run has ended. Your heroes, Sigils and summon pity carry over; the tower starts again.\n" +
                TowerRules.LegacyPreview(state);
        RefreshTop(state);
        RefreshSiegeBanner(state);
        RefreshToast();
        RefreshResidents(); RefreshRoom(); RefreshTutorial(); RefreshAdvice(state);
        RefreshDock(state);
        if (popupBuild.gameObject.activeSelf) RefreshBuild();
        if (popupFloors.gameObject.activeSelf) RefreshFloors(state);
        if (popupTasks.gameObject.activeSelf) RefreshTasks(state);
        if (popupGuild.gameObject.activeSelf) RefreshGuild();
        if (popupHeart.gameObject.activeSelf) RefreshHeart(state);
        if (popupAlerts.gameObject.activeSelf) RefreshAlerts(state);
        if (popupMenu.gameObject.activeSelf) RefreshMainMenu(state);
        if (popupWork.gameObject.activeSelf) RefreshWork();
        if (popupDistricts.gameObject.activeSelf) RefreshDistricts(state);
        if (popupAuto.gameObject.activeSelf) RefreshAuto(state);
        if (popupOutposts.gameObject.activeSelf) RefreshOutposts(state);
        if (popupLair != null && popupLair.gameObject.activeSelf) RefreshLair(state);
        RefreshDev();
        RefreshChip();
    }

    private void RefreshToast()
    {
        string message = tower.CurrentMessage ?? "";
        if (message == shownMessage) return;
        shownMessage = message;
        if (message.Length == 0) return;
        toastText.text = message;
        toastTimer = 4.5f;
        toastPanel.gameObject.SetActive(true);
    }

    private void RefreshChip()
    {
        bool show = (tower.Placing || tower.Moving) && !AnyPopupOpen() && tower.Rules.State.introPhase != "dormant" &&
            tower.Rules.State.introPhase != "gate" && tower.Rules.State.introPhase != "choose";
        chipPanel.gameObject.SetActive(show);
        if (!show) return;
        var moving = tower.MovingRoom;
        if (moving != null)
        {
            chipText.text = "MOVING  " + TowerCatalog.Get(moving.type).displayName.ToUpperInvariant() + "  " +
                tower.Rules.MoveCost(moving) + "g   -  tap its new place";
            return;
        }
        var def = TowerCatalog.Get(tower.BuildType);
        chipText.text = "PLACING  " + (def == null ? "" : def.displayName.ToUpperInvariant() + "  " +
            tower.Rules.BuildCost(def.id) + "g " + tower.Rules.BuildWoodCost(def.id) + "w " +
            tower.Rules.BuildStoneCost(def.id) + "s") + "   -  tap a + lot";
    }

    private void RefreshTop(TowerState state)
    {
        float hour = tower.Rules.Hour();
        clockText.text = "DAY " + state.day + "  ·  " + ((int)hour).ToString("00") + ":" +
            ((int)((hour % 1f) * 60f)).ToString("00");
        float threat = Mathf.Clamp01(state.threat / 100f);
        threatFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.03f, threat), 1);
        threatFill.color = threat < 0.5f ? Color.Lerp(new Color(0.25f, 0.65f, 0.4f), Gold, threat * 2) :
            Color.Lerp(Gold, Alert, (threat - 0.5f) * 2);
        threatText.text = state.introPhase == "complete" ?
            tower.Rules.ThreatLabel().ToUpperInvariant() + "  ·  HEART " + Mathf.CeilToInt(state.heartHp) +
                (state.siegeWarning > 0 ? "  ·  <color=#ff7060>SIEGE " + TowerRules.Clock(state.siegeWarning) + "</color>" : "") :
            "HEART " + Mathf.CeilToInt(state.heartHp);
        if (tower.Rules.LairFounded && state.introPhase == "complete") threatText.text = LairTopShort(state);
        RefreshTopChrome(state);
    }

    // Net stock change per game minute, coloured so a falling store is obvious at a glance.
    private string Trend(string resource)
    {
        if (tower.Rules.State.introPhase != "complete") return "";
        float net = tower.Rules.NetPerMinute(resource);
        if (Mathf.Abs(net) < 0.05f) return "";
        string color = net < 0 ? "#ff8a70" : "#8fe39a";
        return " <color=" + color + ">" + (net > 0 ? "+" : "") + net.ToString("0.0") + "/m</color>";
    }

    private void RefreshAdvice(TowerState state)
    {
        bool show = state.introPhase == "complete" && state.tutorialStep >= 7;
        string advice = show ? tower.Rules.NeedsAdvice() : "";
        advicePanel.gameObject.SetActive(advice.Length > 0);
        adviceText.text = advice;
    }

    // Dock buttons carry badges so nothing needs its own always-on widget.
    private void RefreshDock(TowerState state)
    {
        int claimable = 0;
        if (state.introPhase == "complete")
            foreach (var goal in state.goals) if (tower.Rules.GoalComplete(goal)) claimable++;
        claimable += tower.Rules.DailyClaimable();
        bool danger = state.incidents.Count > 0;
        SetBadge(goalsButton, claimable);
        goalsButton.GetComponent<Image>().color = claimable > 0 ? Gold : Teal;
        SetBadge(alertsButton, state.incidents.Count);
        alertsButton.GetComponent<Image>().color = danger ? Alert : Teal;
        stewardTop.GetComponent<Image>().color = state.steward ? Gold : Muted;
        LabelOf(stewardTop).text = state.steward ? "Steward" : "Steward off";
        dockBuild.GetComponent<Image>().color = tower.Placing || tower.Moving || popupBuild.gameObject.activeSelf ||
            popupFloors.gameObject.activeSelf || popupDistricts.gameObject.activeSelf ? Gold : Teal;
        var director = tower.GetComponent<TowerArtDirector>();
        dockPeople.GetComponent<Image>().color = director != null && director.ResidentsOpen ? Gold : Teal;
        dockExpeditions.GetComponent<Image>().color = popupGuild.gameObject.activeSelf || popupAuto.gameObject.activeSelf ||
            popupOutposts.gameObject.activeSelf ? Gold : Teal;
        // Gold switches the Heart emblem to its READY art (or a gold rim on the flat skin).
        dockHeart.GetComponent<Image>().color = HeartReady() ? Gold : Violet;
        bool started = state.introPhase != "dormant" && state.introPhase != "gate";
        bool complete = state.introPhase == "complete";
        dockBuild.interactable = goalsButton.interactable = started;
        dockPeople.interactable = dockHeart.interactable = dockExpeditions.interactable = dockBattle.interactable =
            stewardTop.interactable = complete;
    }

    private void RefreshFloors(TowerState state)
    {
        int focus = tower.FocusFloor;
        floorText.text = "FLOOR " + (focus >= 0 ? "+" : "") + focus;
        var here = tower.Rules.Floor(focus);
        LabelOf(floorOpenAbove).text = "OPEN ABOVE  " + tower.Rules.FloorOpenCost(focus + 1) + "C";
        LabelOf(floorOpenBelow).text = "OPEN BELOW  " + tower.Rules.FloorOpenCost(focus - 1) + "C";
        var rules = tower.Rules;
        var above = rules.FloorWork(focus + 1);
        var below = rules.FloorWork(focus - 1);
        floorOpenAbove.interactable = rules.Floor(focus + 1) == null && above == null && focus < TowerRules.FloorMax &&
            rules.FloorCapReason(focus + 1) == null;
        floorOpenBelow.interactable = rules.Floor(focus - 1) == null && below == null && focus > TowerRules.FloorMin &&
            rules.FloorCapReason(focus - 1) == null;
        // GDD 8.4: past the Heart's reach the button names the rank that opens the floor.
        if (rules.Floor(focus + 1) == null && rules.FloorCapReason(focus + 1) != null && focus < TowerRules.FloorMax)
            LabelOf(floorOpenAbove).text = "NEEDS HEART " + TowerTiers.Tier(TowerRules.HeartRankForFloor(focus + 1));
        if (rules.Floor(focus - 1) == null && rules.FloorCapReason(focus - 1) != null && focus > TowerRules.FloorMin)
            LabelOf(floorOpenBelow).text = "NEEDS HEART " + TowerTiers.Tier(TowerRules.HeartRankForFloor(focus - 1));
        if (above != null) LabelOf(floorOpenAbove).text = "BUILDING  " + TowerRules.Clock(above.remaining);
        if (below != null) LabelOf(floorOpenBelow).text = "BUILDING  " + TowerRules.Clock(below.remaining);
        if (rules.LairFounded)
        {
            floorText.text = rules.FloorLabel(focus);
            LabelOf(floorOpenAbove).text = "DIG DUNGEON FLOOR  " + rules.LairDigCost() + "C";
            LabelOf(floorOpenBelow).text = "ADD LIVING FLOOR  " + rules.LairLivingCost() + "C";
            floorOpenAbove.interactable = floorOpenBelow.interactable = true;
        }
        var westWork = rules.WingWork(focus, -1);
        var eastWork = rules.WingWork(focus, 1);
        floorWest.interactable = here != null && westWork == null;
        floorEast.interactable = here != null && eastWork == null;
        if (here == null) return;
        LabelOf(floorWest).text = westWork != null ? "BUILDING  " + TowerRules.Clock(westWork.remaining) :
            "< WEST  " + rules.ExpandCost(focus, -1) + "C   (" +
            rules.WingCellsOf(focus, -1) + "/" + TowerRules.WingCells + ")";
        int eastCells = rules.WingCellsOf(focus, 1) - (focus == 0 ? 1 : 0);
        LabelOf(floorEast).text = eastWork != null ? "BUILDING  " + TowerRules.Clock(eastWork.remaining) :
            "EAST >  " + rules.ExpandCost(focus, 1) + "C   (" + eastCells + "/" + TowerRules.WingCells + ")";
    }

    private void RefreshTasks(TowerState state)
    {
        int claimable = 0, dailyReady = tower.Rules.DailyClaimable();
        bool daily = tasksTab == "daily";
        tabGoals.GetComponent<Image>().color = daily ? Teal : Gold;
        tabDaily.GetComponent<Image>().color = daily ? Gold : Teal;
        LabelOf(tabDaily).text = dailyReady > 0 ? "DAILY  (" + dailyReady + ")" : "DAILY";
        dailyBonusText.gameObject.SetActive(daily);
        dailyBonusText.text = state.dailyBonusClaimed ? "BOARD CLEAR  +" + TowerRules.DailyBonusSigils + " paid" :
            "CLEAR ALL: +" + TowerRules.DailyBonusSigils + " SIGILS";
        for (int i = 0; i < dailyTexts.Length; i++)
        {
            bool has = daily && state.introPhase == "complete" && i < state.daily.Count;
            dailyTexts[i].gameObject.SetActive(has);
            dailyClaims[i].gameObject.SetActive(has);
            if (!has) continue;
            var task = state.daily[i];
            var def = TowerRules.DailyDef(task.id);
            bool done = tower.Rules.DailyComplete(task);
            dailyTexts[i].text = def.title + "\n" + (task.claimed ? "Claimed" : tower.Rules.DailyProgress(task) + " / " +
                Mathf.Max(1, def.target)) + "     Reward: " + TowerRules.DailyTaskSigils + " Sigils";
            dailyClaims[i].interactable = done && !task.claimed;
            dailyClaims[i].GetComponent<Image>().color = done && !task.claimed ? Gold : Teal;
            LabelOf(dailyClaims[i]).text = task.claimed ? "DONE" : "CLAIM";
        }
        for (int i = 0; i < goalTexts.Length; i++)
        {
            bool has = !daily && state.introPhase == "complete" && i < state.goals.Count;
            goalTexts[i].gameObject.SetActive(has);
            goalClaims[i].gameObject.SetActive(has);
            if (!has) continue;
            var goal = state.goals[i];
            var def = TowerRules.GoalDef(goal.id);
            bool done = tower.Rules.GoalComplete(goal);
            if (done) claimable++;
            goalTexts[i].text = def.title + "\n" + tower.Rules.GoalProgress(goal) + " / " + def.target +
                "     Reward: " + def.Reward;
            goalClaims[i].interactable = done;
            goalClaims[i].GetComponent<Image>().color = done ? Gold : Teal;
        }
        if (state.introPhase != "complete") goalTexts[0].gameObject.SetActive(false);
        string incidents = "";
        foreach (var incident in state.incidents)
        {
            var room = tower.Rules.Room(incident.roomUid);
            incidents += TowerRules.IncidentName(incident.kind).ToUpperInvariant() + "  /  " +
                (room == null ? "unknown room" : TowerCatalog.Get(room.type).displayName) +
                (incident.stolen > 0 ? "  /  " + incident.stolen + "g stolen" : "") + "\n";
        }
        string lost = "";
        if (state.memorial.Count > 0)
        {
            var last = state.memorial[state.memorial.Count - 1];
            lost = "\nRemembered: " + last.name + " (day " + last.day + ", " + last.cause + ")" +
                (state.memorial.Count > 1 ? " and " + (state.memorial.Count - 1) + " more" : "");
        }
        incidentText.text = (incidents.Length == 0 ? "The Tower is safe." : incidents.TrimEnd('\n')) +
            "\nThreat " + tower.Rules.ThreatLabel() + "  •  next event " + Mathf.CeilToInt(state.eventCooldown) +
            "s  •  Gate guards " + tower.Rules.GuardCount() + "/" + GateSlots() +
            (tower.Rules.DarkRoomCount > 0 ? "  •  " + tower.Rules.DarkRoomCount + " rooms dark" : "") + lost;
        recruit.interactable = state.pendingVisitors > 0;
        LabelOf(recruit).text = "RECRUIT AT GATE (" + state.pendingVisitors + ")";
        autoHaul.interactable = !state.haulingUnlocked;
        LabelOf(autoHaul).text = state.haulingUnlocked ? "AUTO-HAUL ON" : "UNLOCK AUTO-HAUL";
        LabelOf(stewardButton).text = state.steward ? "STEWARD: ON" : "STEWARD: OFF";
        stewardButton.GetComponent<Image>().color = state.steward ? Gold : Teal;
    }

    private void RefreshResidents()
    {
        var people = tower.Rules.State.residents;
        rosterPage = Mathf.Clamp(rosterPage, 0, Mathf.Max(0, (people.Count - 1) / 4));
        rosterPageText.text = "" + people.Count + " residents  /  page " + (rosterPage + 1) +
            " of " + Mathf.Max(1, Mathf.CeilToInt(people.Count / 4f));
        for (int i = 0; i < 4; i++)
        {
            int index = rosterPage * 4 + i;
            rosterButtons[i].gameObject.SetActive(index < people.Count);
            if (index >= people.Count) continue;
            var person = people[index];
            rosterLabels[i].text = person.name + "  Lv" + person.level + "   " +
                (person.ageStage == 1 ? "CHILD" : TowerRules.IsCritical(person) ? "CRITICAL" :
                    person.downed ? "DOWN" : person.currentTask.ToUpperInvariant());
            rosterButtons[i].GetComponent<Image>().color = tower.SelectedPerson == person ? Gold :
                person.downed ? Alert : Teal;
        }
        var selected = tower.SelectedPerson;
        if (selected == null)
        {
            residentDetail.text = "Choose a starter at the Heart.";
        }
        else
        {
            residentDetail.text = selected.name + TowerRoster.Tag(selected) + "  Lv " + selected.level +
                (selected.ageStage == 0 ? " (" + selected.xp + "/" + TowerRules.XpToNext(selected) + " xp)" : "") +
                "   HP " + Mathf.CeilToInt(selected.hp) + "/" + Mathf.CeilToInt(TowerRules.MaxHp(selected)) +
                "   Mood " + Mathf.CeilToInt(selected.happiness) + " " + tower.Rules.MoodLabel(selected) +
                "\nFood " + Mathf.CeilToInt(selected.hunger) + "  Water " +
                Mathf.CeilToInt(selected.thirst) + "  Rest " + Mathf.CeilToInt(selected.rest) +
                (selected.ageStage == 0 && selected.origin != "body" ? "  Joy " + Mathf.CeilToInt(selected.joy) : "") +
                "\nM" + selected.might + " S" + selected.sight + " G" + selected.grit +
                " C" + selected.charm + " W" + selected.wit + " A" + selected.grace +
                " L" + selected.luck + "   T" + selected.tool + " B" + selected.weapon +
                "\n" + (selected.injury > 0 ? "Injury " + Mathf.CeilToInt(selected.injury) + "  " : "") +
                (selected.illness > 0 ? "Ill " + Mathf.CeilToInt(selected.illness) + "  " : "") +
                tower.Rules.TaskExplanation(selected);
        }
        bool work = leftTab == "work", explore = leftTab == "explore", family = leftTab == "family";
        bool mood = leftTab == "mood";
        foreach (var button in priorityButtons) button.gameObject.SetActive(work);
        moodText.gameObject.SetActive(mood);
        scheduleButton.gameObject.SetActive(mood);
        if (mood) RefreshMood(selected);
        priorityHint.gameObject.SetActive(family);
        priorityHint.text = familyFirstId > 0 ? "Select another adult resident above to form a family.\nA spare bed and good morale are required." :
            "Families need two housed adults and a spare bed. Children live here and mature before work.";
        familyPair.gameObject.SetActive(family);
        exploreSupplies.gameObject.SetActive(explore);
        exploreRelics.gameObject.SetActive(explore);
        explorePatrol.gameObject.SetActive(explore);
        recall.gameObject.SetActive(explore);
        craftTool.gameObject.SetActive(explore);
        craftWeapon.gameObject.SetActive(explore);
        LabelOf(explorePatrol).text = "GUILD";
        explorePatrol.interactable = tower.Rules.State.introPhase == "complete";
        if (selected != null)
        {
            for (int i = 0; i < priorityButtons.Length; i++)
            {
                priorityLabels[i].text = priorityLabelsShort[i] + " " +
                    Priority(selected, priorityNames[i]);
                priorityButtons[i].interactable = selected.ageStage == 0;
            }
            exploreSupplies.interactable = exploreRelics.interactable =
                selected.ageStage == 0 && !selected.exploring && !selected.downed;
            recall.interactable = selected.exploring;
            craftTool.interactable = craftWeapon.interactable = selected.ageStage == 0;
            familyPair.interactable = selected.ageStage == 0 && selected.familyPartnerId == 0;
        }
        else
        {
            foreach (var button in priorityButtons) button.interactable = false;
            exploreSupplies.interactable = exploreRelics.interactable = recall.interactable = false;
            craftTool.interactable = craftWeapon.interactable = familyPair.interactable = false;
        }
    }

    private void RefreshMood(TowerResident selected)
    {
        if (selected == null) { moodText.text = "Select a resident."; scheduleButton.interactable = false; return; }
        var thoughts = tower.Rules.Thoughts(selected);
        thoughts.Sort((a, b) => Mathf.Abs(b.value).CompareTo(Mathf.Abs(a.value)));
        string text = TowerRules.TraitLine(selected) + "   /   " +
            (selected.origin == "body" ? "never sleeps" : selected.schedule + " schedule, free " +
                TowerRules.FreeHours(selected.schedule));
        if (selected.inspirationSeconds > 0)
            text += "\n<color=#ffd45c>" + TowerRules.InspirationLabel(selected.inspiration) + " " +
                Mathf.CeilToInt(selected.inspirationSeconds) + "s</color>";
        else if (TowerRules.CanLeave(selected) && selected.leaveSeconds >= TowerRules.LeaveWarnSeconds)
            text += "\n<color=#ff7060>Thinking of leaving the Tower</color>";
        int shown = 0;
        foreach (var thought in thoughts)
        {
            if (shown++ >= 3) break;
            text += "\n" + (thought.value >= 0 ? "+" : "") + Mathf.RoundToInt(thought.value) + "  " + thought.label;
        }
        var relations = tower.Rules.Relations(selected);
        if (relations.Count > 0)
        {
            text += "\n";
            for (int i = 0; i < relations.Count && i < 2; i++)
                text += (i > 0 ? "   " : "") + TowerRules.OpinionLabel(relations[i].Value) + ": " +
                    relations[i].Key.name + " " + Mathf.RoundToInt(relations[i].Value);
        }
        if (thoughts.Count == 0) text += "\nThis resident has no moods to manage.";
        moodText.text = text;
        scheduleButton.interactable = selected.ageStage == 0 && selected.origin != "body";
        LabelOf(scheduleButton).text = "SCHEDULE: " + selected.schedule.ToUpperInvariant() + "  (tap to change)";
    }

    private void RefreshRoom()
    {
        var room = tower.SelectedRoom;
        if (room == null)
        {
            roomDetail.text = "Tap a room in the cutaway.";
            roomAdvice.text = "Drag a resident from the roster onto a room, or select both and press ASSIGN.";
            collect.interactable = rush.interactable = upgrade.interactable = assign.interactable = false;
            moveRoom.interactable = demolishRoom.interactable = false;
            guildOpen.gameObject.SetActive(false);
            guildAutoOpened = 0;
            return;
        }
        var def = TowerCatalog.Get(room.type);
        bool isGuild = room.type == "guild_hall";
        guildOpen.gameObject.SetActive(isGuild);
        if (!isGuild) guildAutoOpened = 0;
        else if (guildAutoOpened != room.uid)
        {
            // Selecting the Guild Hall opens its expedition board.
            guildAutoOpened = room.uid;
            OpenGuildBoard();
        }
        string activity = room.ready ? "READY TO COLLECT" : def.kind == "living" ?
            "Housing " + tower.Rules.State.residents.FindAll(r => r.homeRoom == room.uid).Count +
                "/" + tower.Rules.Capacity(room) : def.kind == "train" ?
            "Training " + Mathf.RoundToInt(room.progress * 100) + "%" :
            def.kind == "gate" ? "Guard post" :
            string.IsNullOrEmpty(def.produces) ? "Tower facility" : !tower.Rules.IsPowered(room) ?
                "DARK: no firewood reaches this room" : "Production " +
                Mathf.RoundToInt(room.progress * 100) + "%  •  " +
                tower.Rules.ProductionRate(room).ToString("0.0") + " rate";
        roomDetail.text = def.displayName + "   /   Floor " + room.floor +
            "\n" + tower.Rules.LevelLabel(room) + "   Condition " + Mathf.CeilToInt(room.condition) + "%" +
            "   Workers " + tower.Rules.WorkerCount(room.uid) +
            "\n" + activity + (TowerRules.VenueDef(room.type) != null ? "   •   " + tower.Rules.VenueLine(room) : "") +
            (tower.Rules.AdjacencyNote(room).Length > 0 ?
                "\n" + tower.Rules.AdjacencyNote(room) : "");
        collect.interactable = room.ready;
        rush.interactable = !room.ready && !string.IsNullOrEmpty(def.produces) &&
            tower.Rules.RushChance(room.uid) > 0;
        LabelOf(rush).text = string.IsNullOrEmpty(def.produces) ? "RUSH" :
            "RUSH " + Mathf.RoundToInt(tower.Rules.RushChance(room.uid) * 100) + "%";
        upgrade.interactable = room.level < tower.Rules.MaxLevel(room) && room.type != "heart" && room.type != "gate";
        LabelOf(upgrade).text = room.type == "heart" || room.type == "gate" ? "UPGRADE" :
            room.level >= tower.Rules.MaxLevel(room) ? "RANK SSR" :
            room.level >= tower.Rules.RankCap() ? "HEART " + TowerTiers.Tier(room.level + 1) + "+" :
            "TO " + TowerTiers.Tier(room.level + 1) + " " + tower.Rules.UpgradeGoldCost(room) + "g";
        bool fixedRoom = room.type == "heart" || room.type == "gate";
        moveRoom.interactable = !fixedRoom && !tower.Moving;
        LabelOf(moveRoom).text = fixedRoom ? "MOVE" : tower.Moving ? "MOVING..." : "MOVE " + tower.Rules.MoveCost(room) + "g";
        demolishRoom.interactable = !fixedRoom;
        if (demolishArmedRoom != room.uid || Time.unscaledTime > demolishArmedUntil)
            LabelOf(demolishRoom).text = fixedRoom ? "DEMOLISH" : "DEMOLISH +" + tower.Rules.DemolishRefund(room) + "g";
        assign.interactable = tower.SelectedPerson != null && tower.SelectedPerson.ageStage == 0 &&
            !tower.SelectedPerson.downed && !tower.SelectedPerson.exploring &&
            room.type != "heart";
        var chosen = tower.SelectedPerson;
        if (def.kind == "living")
        {
            int housed = tower.Rules.State.residents.FindAll(r => r.homeRoom == room.uid).Count;
            roomAdvice.text = "Beds " + housed + "/" + tower.Rules.Capacity(room) +
                "  •  shared housing supports family growth.";
            return;
        }
        if (def.kind == "lair") { roomAdvice.text = tower.Rules.LairRoomAdvice(room); return; }
        if (def.kind == "heart")
        { roomAdvice.text = "The Heart connects every floor. If raiders reach it, it bleeds."; return; }
        if (def.kind == "gate")
        {
            roomAdvice.text = "GUARD POST  •  guards " + tower.Rules.GuardCount() + "/" + tower.Rules.Capacity(room) +
                "\nSilverwood raiders arrive here first. Post strong residents (Might, weapons)." +
                "\nUnfought raiders loot gold and push deeper every " + (int)TowerRules.RaidMarchSeconds + "s.";
            return;
        }
        TowerResident best = null;
        float score = 0;
        foreach (var resident in tower.Rules.State.residents)
            if (!resident.downed && !resident.exploring && resident.ageStage == 0 &&
                tower.Rules.MatchScore(resident, room) > score)
            { best = resident; score = tower.Rules.MatchScore(resident, room); }
        float gain = tower.Rules.AssignmentImpact(chosen, room);
        float rate = tower.Rules.ProductionRate(room);
        float cycle = def.kind == "train" ? 240f : 90f;
        roomAdvice.text = "Best match: " + (best == null ? "none" : best.name + " (" + score.ToString("0.0") + ")") +
            "\nSelected: " + (chosen == null ? "none" : chosen.name + " (" +
                tower.Rules.MatchScore(chosen, room).ToString("0.0") + ")") +
            "\nExpected: +" + gain.ToString("0.0") + " rate" +
            (gain > 0 ? "  •  ~" + Mathf.CeilToInt(cycle / (rate + gain)) + "s cycle" : "") +
            "\nRush failure: fire/pests, -7% condition";
    }

    private void RefreshBuild()
    {
        var available = BuildChoices();
        buildPage = Mathf.Clamp(buildPage, 0, Mathf.Max(0, (available.Count - 1) / 6));
        buildPageText.text = "ROOMS  " + (buildPage + 1) + " / " +
            Mathf.Max(1, Mathf.CeilToInt(available.Count / 6f)) + "     (" + available.Count + " " +
            (buildCategory == "all" ? "total" : buildCategory) + ")";
        for (int i = 0; i < categoryButtons.Length; i++)
            categoryButtons[i].GetComponent<Image>().color = buildCategory == categoryIds[i] ? Gold : Teal;
        for (int i = 0; i < buildButtons.Length; i++)
        {
            int index = buildPage * 6 + i;
            buildButtons[i].gameObject.SetActive(index < available.Count);
            if (index >= available.Count) continue;
            string id = available[index];
            var def = TowerCatalog.Get(id);
            bool known = tower.Rules.State.blueprints.Contains(id);
            buildLabels[i].text = known ? def.displayName.ToUpperInvariant() + "  " +
                tower.Rules.BuildCost(id) + "g  " + tower.Rules.BuildWoodCost(id) + "w " +
                tower.Rules.BuildStoneCost(id) + "s" :
                (TowerRules.UnlockNode(id) != null ? "RESEARCH " + TowerRules.UnlockNode(id).id + "  " +
                    def.displayName.ToUpperInvariant() :
                    "STUDY " + def.displayName.ToUpperInvariant() + "  " + tower.Rules.BlueprintGoldCost(id) + "g " +
                    tower.Rules.BlueprintCelestiumCost(id) + "C");
            buildButtons[i].GetComponent<Image>().color = tower.Placing && tower.BuildType == id && known ? Gold : Teal;
        }
    }

    private void RefreshTutorial()
    {
        string phase = tower.Rules.State.introPhase;
        int step = tower.Rules.State.tutorialStep;
        tutorialPanel.gameObject.SetActive(phase != "complete" || step < 7);
        bool dormant = phase == "dormant";
        foreach (var button in storytellerButtons) button.gameObject.SetActive(dormant);
        storytellerHint.gameObject.SetActive(dormant);
        tutorialPanel.rectTransform.offsetMin = new Vector2(tutorialPanel.rectTransform.offsetMin.x, dormant ? -258 : -178);
        if (dormant)
        {
            var teller = tower.Rules.Storyteller;
            for (int i = 0; i < storytellerButtons.Length; i++)
                storytellerButtons[i].GetComponent<Image>().color = TowerRules.Storytellers[i].id == teller.id ? Gold : Teal;
            storytellerHint.text = "Storyteller: " + teller.name + ". " + teller.blurb;
        }
        if (phase == "complete")
        {
            tutorialAction.gameObject.SetActive(false);
            foreach (var button in heroButtons) button.gameObject.SetActive(false);
            tutorialTitle.fontSize = 17;
            tutorialTitle.rectTransform.sizeDelta = new Vector2(592, 82);
            string[] lessons = {
                "1 / BUILD  •  Open FLOORS and expand the foundation twice (WEST or EAST). Then open BUILD, choose Kitchen and tap the empty + lot.",
                "2 / MATCH  •  In PEOPLE pick a resident, tap the Kitchen, compare the expected rate, then press ASSIGN.",
                "3 / COLLECT  •  Let the Kitchen finish, then tap its food bubble (or COLLECT).",
                "4 / RUSH  •  Review the success chance and failure risk, then rush the Kitchen.",
                "5 / PRIORITIES  •  In PEOPLE > WORK, tap a priority button (PROD, HAUL, REPAIR...) to change it.",
                "6 / INCIDENT  •  Watch the alert and responders travel to the affected room.",
                "7 / RECOVER  •  Keep fire, care, and defense priorities active until danger ends."
            };
            if (step < lessons.Length) tutorialTitle.text = lessons[step];
            return;
        }
        if (RefreshLairFounding(phase)) return;
        tutorialTitle.fontSize = 20;
        tutorialTitle.rectTransform.sizeDelta = new Vector2(592, 38);
        tutorialTitle.text = phase == "dormant" ? "Awaken the Celestium Heart" :
            phase == "gate" ? "Place the Celestium Gate" :
            phase == "shack" ? "Build the Shack beside the Heart: tap the + lot" : "Choose your first resident";
        tutorialAction.gameObject.SetActive(phase == "dormant" || phase == "gate");
        LabelOf(tutorialAction).text = phase == "dormant" ? "AWAKEN HEART" : "PLACE GATE";
        foreach (var button in heroButtons) button.gameObject.SetActive(phase == "choose");
    }
}

public sealed class TowerResidentDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Action<Vector2> OnDrop;
    public void OnBeginDrag(PointerEventData eventData) { }
    public void OnDrag(PointerEventData eventData) { }
    public void OnEndDrag(PointerEventData eventData)
    { if (OnDrop != null) OnDrop(eventData.position); }
}
