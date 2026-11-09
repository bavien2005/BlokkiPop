using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Native Unity screenshots and gameplay checks, isolated QA project ONLY.</summary>
public static class PopClassicPresentationValidation
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly Vector2Int[] Sizes = { new(720, 1280), new(720, 1440), new(720, 1600), new(1080, 2400), new(768, 1024) };
    private static string Output => Path.GetFullPath("../gameover-hud-20261018");
    private static readonly List<string> results = new(), errors = new();
    private static readonly Dictionary<string, string> authored = new();
    private static int stage, sizeIndex, checks, objectsBefore;
    private static double next, deadline;
    private static bool editProbe = true;
    private static float authoredFont;
    private static Color authoredColor;
    private static string pausePose;
    private static PopRunSnapshot saved;
    private static Mouse mouse;
    private static Rect safe;
    public static void RunRelease() { editProbe = false; Run(); }
    public static void Run()
    {
        if (Application.companyName != "BlokkiPopRegression" || !Application.dataPath.Replace('\\', '/').Contains("/.utmp/domain-reload-validation-20261009/")) throw new Exception("QA project/preferences only.");
        Directory.CreateDirectory(Output); PopClassicUIAuthoring.Install();
        GameObject root = PrefabUtility.LoadPrefabContents(PopSceneUIAuthoring.PrefabPath);
        try
        {
            var hud = root.GetComponentInChildren<PopClassicHUDView>(true);
            if (editProbe) { hud.header.anchoredPosition += new Vector2(4, -6); hud.stats.anchoredPosition += new Vector2(-4, 6); hud.score.fontSize = 106; hud.score.color = new Color(.93f, 1, 1); }
            authoredFont = hud.score.fontSize; authoredColor = hud.score.color;
            var pause = root.GetComponent<PopGameplayUI>().View(PopUIPage.Pause).GetComponent<PopPausePresentation>();
            if (editProbe) pause.card.anchoredPosition += new Vector2(12, -16);
            pausePose = Pose(pause.card);
            foreach (RectTransform rect in hud.GetComponentsInChildren<RectTransform>(true)) if (IsAuthored(rect, hud)) authored[AnimationUtility.CalculateTransformPath(rect, hud.transform)] = Pose(rect);
            PrefabUtility.SaveAsPrefabAsset(root, PopSceneUIAuthoring.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        PopClassicUIAuthoring.Install(); // Repeat install must not reset the probe edits.
        var config = AssetDatabase.LoadAssetAtPath<PopGameplayConfiguration>(PopGameplayConfiguration.AssetPath);
        config.runs.classicRewardedRevive = false; EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
        PlayerPrefs.SetInt("HighScore", 3913); PlayerPrefs.Save();
        Application.runInBackground = true; ConfigureSize(Sizes[0]);
        EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        Application.logMessageReceived += OnLog; EditorApplication.update += Tick; deadline = EditorApplication.timeSinceStartup + 230;
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Home.unity"); EditorApplication.EnterPlaymode(); Arm(0, .8);
    }
    private static void Tick()
    {
        try
        {
            if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout stage " + stage);
            if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling) return;
            if (stage == 99)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Check(errors.Count == 0, "No runtime errors: " + string.Join(";", errors));
                results.Add("CLASSIC PRESENTATION PASS: " + checks + " checks; five screen ratios + notch, pause/settings/resume, new HUD retained behind Game Over, retired score/settings inactive, result canvas above HUD, actual Restart/Home clicks, revive layering, no layout changes.");
                File.WriteAllLines(Output + "/results.txt", results); Debug.Log(results[^1]); Finish(0); return;
            }
            var session = PopGameplaySession.Instance; var ui = PopGameplayUI.Instance; var manager = GameFlowManager._instance;
            if (!EditorApplication.isPlaying || session == null || ui == null) return;
            var hud = ui.ClassicHUD;
            switch (stage)
            {
                case 0:
                    if (!ui.View(PopUIPage.Home).gameObject.activeInHierarchy) return;
                    session.SelectTheme(session.Configuration.themes[0].id); session.PlayClassic(false); Arm(1, .5); break;
                case 1:
                    if (!session.IsActive || session.Mode != PopGameMode.Classic || SceneManager.GetActiveScene().buildIndex != 1) return;
                    for (int i = 0; i < 7; ++i) PlayMove(session);
                    objectsBefore = hud.GetComponentsInChildren<RectTransform>(true).Length;
                    Object.FindFirstObjectByType<BoardEffects>().PlayScoreReward(12, new Vector3(4, 4, 0), Color.cyan);
                    Arm(2, 1.4); break;
                case 2:
                    if (Screen.width != Sizes[sizeIndex].x || Screen.height != Sizes[sizeIndex].y) return;
                    Canvas.ForceUpdateCanvases(); Object.FindFirstObjectByType<ResponsiveGameLayout>().Refresh(); safe = Screen.safeArea; CheckLayout(ui, safe);
                    Check((Transform)typeof(BoardEffects).GetField("cachedScoreTarget", Flags).GetValue(Object.FindFirstObjectByType<BoardEffects>()) == hud.score.transform, "Floating score rewards target the visible score, not the hidden legacy coin");
                    ScreenCapture.CaptureScreenshot(Output + "/classic-" + Screen.width + "x" + Screen.height + ".png"); Arm(3, .25); break;
                case 3:
                    if (++sizeIndex < Sizes.Length) { ConfigureSize(Sizes[sizeIndex]); Arm(2, .5); }
                    else { ConfigureSize(new Vector2Int(720, 1600)); Arm(4, .5); } break;
                case 4:
                    if (Screen.width != 720 || Screen.height != 1600) return;
                    safe = new Rect(0, 40, 720, 1476); var rect = (RectTransform)hud.transform; Vector2 pos = rect.anchoredPosition, size = rect.sizeDelta;
                    hud.fitDeviceSafeArea = false; typeof(PopClassicHUDView).GetMethod("FitSafeArea", Flags).Invoke(hud, new object[] { safe, 720, 1600 });
                    Check(rect.anchoredPosition == pos && rect.sizeDelta == size, "Notch fitting preserves authored offsets"); Arm(5, .3); break;
                case 5:
                    Canvas.ForceUpdateCanvases(); Object.FindFirstObjectByType<ResponsiveGameLayout>().Refresh(); CheckLayout(ui, safe);
                    ScreenCapture.CaptureScreenshot(Output + "/classic-safe-area.png"); hud.fitDeviceSafeArea = true;
                    session.SaveBeforeNavigation(); saved = session.SavedRun(PopGameMode.Classic);
                    mouse = InputSystem.AddDevice<Mouse>("Classic HUD QA Mouse");
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = PopEndlessQueueView.ScreenBounds((RectTransform)hud.pauseButton.transform).center }.WithButton(MouseButton.Left)); Arm(6, .15); break;
                case 6:
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = PopEndlessQueueView.ScreenBounds((RectTransform)hud.pauseButton.transform).center }); Arm(7, .2); break;
                case 7:
                    Check(PopGameplayUI.IsModalOpen && Time.timeScale == 0 && ui.View(PopUIPage.Pause).gameObject.activeInHierarchy && !hud.gameObject.activeInHierarchy, "Real mouse click on new Pause opens sheet and freezes game");
                    InputSystem.RemoveDevice(mouse); mouse = null;
                    Canvas.ForceUpdateCanvases(); ScreenCapture.CaptureScreenshot(Output + "/classic-pause.png"); Arm(70, .3); break;
                case 70:
                    var pauseView = ui.View(PopUIPage.Pause); var pause = pauseView.GetComponent<PopPausePresentation>();
                    Canvas.ForceUpdateCanvases();
                    Check(Pose(pause.card) == pausePose, "Pause authored position/size survive reinstall and Play");
                    Check(Mathf.Abs(pause.volume.handleRect.rect.width - pause.volume.handleRect.rect.height) < .1f && pause.volume.fillRect.rect.height <= 14.1f, "Volume keeps a round thumb and a thin track, not stretched slider artwork");
                    Check(Inside(Screen.safeArea, PopEndlessQueueView.ScreenBounds(pause.card)), "Pause card fits the device: " + PopEndlessQueueView.ScreenBounds(pause.card));
                    foreach (TMP_Text text in pause.GetComponentsInChildren<TMP_Text>()) { text.ForceMeshUpdate(); Check(!text.isTextOverflowing, "Pause text fits: " + text.name); }
                    saved = session.SavedRun(PopGameMode.Classic);
                    int sound = PlayerPrefs.GetInt("blokkipop.home.sound.v1", 1), haptics = PlayerPrefs.GetInt("blokkipop.haptics.enabled", 1);
                    pauseView.Element("Sound").button.onClick.Invoke(); Check(PlayerPrefs.GetInt("blokkipop.home.sound.v1", 1) != sound, "Sound toggle uses existing Home preference"); pauseView.Element("Sound").button.onClick.Invoke();
                    pauseView.Element("Haptics").button.onClick.Invoke(); Check(PlayerPrefs.GetInt("blokkipop.haptics.enabled", 1) != haptics, "Haptics toggle uses existing preference"); pauseView.Element("Haptics").button.onClick.Invoke();
                    float oldVolume = AudioManager.Instance.sfxSource.volume; pause.volume.value = .37f; Check(Mathf.Abs(AudioManager.Instance.sfxSource.volume - .37f) < .01f, "Volume slider still controls SFX"); pause.volume.value = oldVolume;
                    pauseView.Element("Close").button.onClick.Invoke(); Check(!PopGameplayUI.IsModalOpen && Time.timeScale > 0 && hud.gameObject.activeInHierarchy, "Close restores HUD and input");
                    session.SaveCurrentRun(); Check(JsonUtility.ToJson(saved) == JsonUtility.ToJson(session.SavedRun(PopGameMode.Classic)), "UI/settings/pause preserve board, score, partial tray and RNG exactly");
                    Check(hud.GetComponentsInChildren<RectTransform>(true).Length == objectsBefore, "No runtime UI generation on repeated refresh/pause");
                    ui.ShowPause(); sizeIndex = 0; ConfigureSize(Sizes[sizeIndex]); Arm(71, .4); break;
                case 71:
                    if (Screen.width != Sizes[sizeIndex].x || Screen.height != Sizes[sizeIndex].y) return;
                    Canvas.ForceUpdateCanvases(); var sheet = ui.View(PopUIPage.Pause).GetComponent<PopPausePresentation>();
                    Check(Pose(sheet.card) == pausePose, "Pause card keeps designer offsets across screen ratios");
                    Check(Inside(Screen.safeArea, PopEndlessQueueView.ScreenBounds(sheet.card)), "Pause card fits " + Screen.width + "x" + Screen.height);
                    foreach (TMP_Text text in sheet.GetComponentsInChildren<TMP_Text>()) { text.ForceMeshUpdate(); Check(!text.isTextOverflowing, "Pause typography fits: " + text.name); }
                    ScreenCapture.CaptureScreenshot(Output + "/pause-" + Screen.width + "x" + Screen.height + ".png"); Arm(72, .15); break;
                case 72:
                    if (++sizeIndex < Sizes.Length) { ConfigureSize(Sizes[sizeIndex]); Arm(71, .4); }
                    else { ui.View(PopUIPage.Pause).Element("Home").button.onClick.Invoke(); Arm(8, .5); } break;
                case 8:
                    if (SceneManager.GetActiveScene().buildIndex != 0 || !ui.View(PopUIPage.Home).gameObject.activeInHierarchy) return;
                    Check(!hud.gameObject.activeInHierarchy && !ui.EndlessQueue.gameObject.activeInHierarchy, "Gameplay overlays do not leak to Home"); session.PlayClassic(true); Arm(9, .5); break;
                case 9:
                    if (!session.IsActive || session.Mode != PopGameMode.Classic || SceneManager.GetActiveScene().buildIndex != 1) return;
                    session.SaveCurrentRun(); Check(JsonUtility.ToJson(saved) == JsonUtility.ToJson(session.SavedRun(PopGameMode.Classic)), "Continue resumes same Classic run after navigation");
                    manager.AddCoin(5000); Arm(10, .2); break;
                case 10:
                    Check(hud.score.text == manager.Score.ToString("N0") && hud.best.text == manager.HighScore.ToString("N0") && hud.newRecord.gameObject.activeInHierarchy, "Score/best/new-record update live");
                    manager.GameOver(); Arm(11, 2); break;
                case 11:
                    Check(hud.gameObject.activeInHierarchy&&!hud.pauseButton.interactable, "New HUD remains behind Game Over with Pause disabled");
                    Check(hud.score.text==manager.Score.ToString("N0")&&hud.best.text==manager.HighScore.ToString("N0"),"Game-over backdrop retains final score and best in the new HUD");
                    CheckRetiredHUD();
                    var panel=GameOverPanel.Instance;
                    Check(panel != null && panel.gameObject.activeInHierarchy, "Original result popup still appears");
                    var resultCanvas=panel.GetComponent<Canvas>();
                    Check(resultCanvas!=null&&resultCanvas.enabled&&resultCanvas.overrideSorting&&resultCanvas.sortingOrder>ui.GetComponent<Canvas>().sortingOrder,"Game-over dimmer and card render above, not under, the new HUD");
                    Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Output+"/game-over-new-hud.png");Arm(110,.25);break;
                case 110:
                    PressResult("Play Again");Arm(111,.12);break;
                case 111:
                    ReleaseMouse();Arm(112,.7);break;
                case 112:
                    if(!session.IsActive||SceneManager.GetActiveScene().buildIndex!=1||!hud.gameObject.activeInHierarchy)return;
                    if(mouse!=null){InputSystem.RemoveDevice(mouse);mouse=null;}
                    Check(manager.Score==0&&hud.score.text=="0"&&hud.pauseButton.interactable,"Actual Restart click restores the new HUD and playable Pause");CheckRetiredHUD();
                    var offer=ReviveOfferPanel.Create(manager);
                    Check(offer.GetComponent<Canvas>().sortingOrder>ui.GetComponent<Canvas>().sortingOrder,"Revive dimmer/card also sort above the retained HUD");Object.Destroy(offer.gameObject);
                    manager.GameOver();Arm(113,2);break;
                case 113:
                    Check(hud.gameObject.activeInHierarchy,"Second Game Over never falls back to old score artwork");CheckRetiredHUD();PressResult("Home");Arm(114,.12);break;
                case 114:
                    ReleaseMouse();Arm(115,.7);break;
                case 115:
                    if(SceneManager.GetActiveScene().buildIndex!=0||!ui.View(PopUIPage.Home).gameObject.activeInHierarchy)return;
                    if(mouse!=null){InputSystem.RemoveDevice(mouse);mouse=null;}
                    Check(!hud.gameObject.activeInHierarchy&&GameOverPanel.Instance==null,"Actual Home click leaves no gameplay HUD/result popup behind");
                    EditorApplication.ExitPlaymode(); Arm(99, .5); break;
            }
        }
        catch (Exception e) { results.Add("CLASSIC PRESENTATION FAIL: " + e); File.WriteAllLines(Output + "/results.txt", results); Debug.LogError(results[^1]); Finish(1); }
    }
    private static void CheckLayout(PopGameplayUI ui, Rect safeArea)
    {
        var hud = ui.ClassicHUD; Compare(hud); Check(hud.HasPresentation && hud.gameObject.activeInHierarchy, "Dedicated Classic HUD is authored and active");
        Check(!ui.View(PopUIPage.Hud).gameObject.activeInHierarchy && !ui.EndlessQueue.gameObject.activeInHierarchy, "No old Classic footer or Endless queue");
        Rect top = PopEndlessQueueView.ScreenBounds(hud.header), bottom = PopEndlessQueueView.ScreenBounds(hud.stats);
        var blocks = Object.FindFirstObjectByType<Blocks>(); var renderer = (SpriteRenderer)typeof(Blocks).GetField("trayRenderer", Flags).GetValue(blocks);
        Rect tray = Pixels(renderer.bounds), board = Pixels(new Bounds(new Vector3(4, 4, 0), new Vector3(8.5f, 8.5f, 0)));
        Check(Inside(safeArea, top) && Inside(safeArea, bottom) && Inside(safeArea, board) && Inside(safeArea, tray), "All authored panels and world playfield fit safe area");
        Check(board.yMax < top.yMin && tray.yMin > bottom.yMax && !top.Overlaps(tray) && !bottom.Overlaps(board), "Header/stats never cover board/tray");
        foreach (TMP_Text text in hud.GetComponentsInChildren<TMP_Text>()) { text.ForceMeshUpdate(); Check(!text.isTextOverflowing, "HUD text fits: " + text.name); }
        Check(hud.score.fontSize == authoredFont && hud.score.color == authoredColor, "Designer font/color are not overwritten");
        Check(hud.score.text == GameFlowManager._instance.Score.ToString("N0") && hud.best.text == GameFlowManager._instance.HighScore.ToString("N0"), "Rendered score/best match game state");
        Check(hud.lines.text == PopGameplaySession.Instance.Lines.ToString() && hud.maxCombo.text == hud.comboPrefix + PopGameplaySession.Instance.MaxCombo, "Footer labels show actual cleared lines and BEST combo, not misleading current combo");
        CheckRetiredHUD();
        var comboPopup = Object.FindFirstObjectByType<ComboPopupController>(FindObjectsInactive.Include);
        Check(comboPopup != null && comboPopup.GetComponentInParent<Canvas>().enabled, "Combo popup still has a visible canvas");
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = tray.center }, hits);
        Check(!hits.Exists(hit => hit.gameObject.transform.IsChildOf(hud.transform)), "Decorative UI does not steal tray touches");
        results.Add(Screen.width + "x" + Screen.height + ": header=" + top + "; board=" + board + "; tray=" + tray + "; stats=" + bottom);
    }
    private static void PlayMove(PopGameplaySession session)
    {
        session.SaveCurrentRun(); var snapshot = session.SavedRun(PopGameMode.Classic); var board = Object.FindFirstObjectByType<Board>(); var blocks = Object.FindFirstObjectByType<Blocks>();
        for (int i = 0; i < 3; ++i) if (snapshot.trayAvailable[i])
        { var choices = BoardSimulation.GetPlacements(board.CreateOccupancySnapshot(), snapshot.trayShapes[i]); if (choices.Count > 0) { var p = choices[0]; Check(blocks.TryPlaceSlot(i, new Vector2Int(p.Column, p.Row)), "Real Classic placement under new HUD"); return; } }
        throw new Exception("QA sample has no playable shape.");
    }
    private static void CheckRetiredHUD()
    {
        var oldScores=Object.FindObjectsByType<TextScore>(FindObjectsInactive.Include,FindObjectsSortMode.None);
        Check(oldScores.Length==2,"Both retired score components are preserved, not deleted");
        foreach(var text in oldScores)Check(!text.transform.parent.gameObject.activeSelf&&!text.gameObject.activeInHierarchy,"Old score/best roots remain inactive in every gameplay/result state");
        var combo=Object.FindFirstObjectByType<ComboPopupController>(FindObjectsInactive.Include);
        Check(combo!=null&&combo.GetComponentInParent<Canvas>(true)!=null,"Combo/result host remains in the live scene hierarchy");
        var host=combo.GetComponentInParent<Canvas>(true).rootCanvas;
        Check(host.enabled,"Shared popup/effect canvas remains enabled");
        foreach(Transform child in host.transform)if(child.name=="Button")Check(!child.gameObject.activeSelf,"Old settings/menu button stays inactive");
    }
    private static void PressResult(string name)
    {
        var button=Array.Find(GameOverPanel.Instance.GetComponentsInChildren<UnityEngine.UI.Button>(),item=>item.name==name);
        Check(button!=null&&button.IsActive()&&button.IsInteractable(),"Result action remains interactable through its CanvasGroups: "+name);
        var point=PopEndlessQueueView.ScreenBounds((RectTransform)button.transform).center;
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==button,"Result receives the top UI raycast, not hidden HUD: "+name);
        // Batch-mode synthetic mouse focus is not reliable at timeScale zero.
        // Use the top raycast target's actual pointer handlers; do not call the
        // navigation method or Button.onClick directly.
        var pointer=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    private static void ReleaseMouse()
    {
        if(mouse==null)return;InputSystem.QueueStateEvent(mouse,new MouseState{position=mouse.position.ReadValue()});
        // Keep the device until navigation completes; removing it here can
        // cancel the pointer-up before the EventSystem has consumed it.
    }
    private static void ConfigureSize(Vector2Int size) => typeof(PopEndlessPresentationValidation).GetMethod("ConfigureSize", Flags).Invoke(null, new object[] { size });
    private static bool IsAuthored(RectTransform rect, PopClassicHUDView hud) => rect != hud.transform && rect.GetComponent<UnityEngine.UI.AspectRatioFitter>() == null;
    private static string Pose(RectTransform rect) => rect.anchorMin + "|" + rect.anchorMax + "|" + rect.anchoredPosition + "|" + rect.sizeDelta + "|" + rect.localScale;
    private static void Compare(PopClassicHUDView hud) { foreach (RectTransform rect in hud.GetComponentsInChildren<RectTransform>(true)) if (IsAuthored(rect, hud)) Check(authored[AnimationUtility.CalculateTransformPath(rect, hud.transform)] == Pose(rect), "Authored transform survives Play/reinstall: " + rect.name); }
    private static Rect Pixels(Bounds bounds) { Vector3 a = Camera.main.WorldToScreenPoint(bounds.min), b = Camera.main.WorldToScreenPoint(bounds.max); return Rect.MinMaxRect(a.x, a.y, b.x, b.y); }
    private static bool Inside(Rect outer, Rect inner) => inner.xMin >= outer.xMin - 1 && inner.xMax <= outer.xMax + 1 && inner.yMin >= outer.yMin - 1 && inner.yMax <= outer.yMax + 1;
    private static void Check(bool value, string message) { ++checks; if (!value) throw new Exception(message); }
    private static void Arm(int value, double delay) { stage = value; next = EditorApplication.timeSinceStartup + delay; }
    private static void OnLog(string text, string stack, LogType type) { if (!stack.Contains("UnityEditor.Search.SearchDatabase") && (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)) errors.Add(text); }
    private static void Finish(int code) { if (mouse != null) InputSystem.RemoveDevice(mouse); EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog; EditorApplication.Exit(code); }
}
