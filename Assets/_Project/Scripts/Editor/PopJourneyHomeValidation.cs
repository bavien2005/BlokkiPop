using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Object=UnityEngine.Object;

public static class PopJourneyHomeValidation
{
    private static int stage,checks;
    private static double next,deadline;
    private static readonly List<string> errors=new();
    private static readonly string output=Path.GetFullPath("../journey-home-20261015");
    private static PopRunSnapshot classic;
    private static bool openedDuringIntro;
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    public static void Run()
    {
        if(Application.companyName!="BlokkiPopRegression"||!Application.dataPath.Replace('\\','/').Contains("/.utmp/domain-reload-validation-20261009/"))throw new Exception("QA project only.");
        Directory.CreateDirectory(output);
        var legacyConfig=AssetDatabase.LoadAssetAtPath<PopGameplayConfiguration>(PopGameplayConfiguration.AssetPath);legacyConfig.endless.replaceJourneyHomeButton=false;EditorUtility.SetDirty(legacyConfig);AssetDatabase.SaveAssets();
        // Refresh only these early QA-generated defaults before publishing the new prefab.
        GameObject contents=PrefabUtility.LoadPrefabContents(PopJourneyHomeAuthoring.PrefabPath);
        var home=contents.GetComponent<PopGameplayUI>().View(PopUIPage.Home);home.Element("Levels").updateText=false;
        home.Element("Stats").rect.anchorMin=home.Element("Stats").rect.anchorMax=new(.06f,.962f);home.Element("Stats").rect.pivot=new(0,.5f);
        PopJourneyHomeAuthoring.Polish(home,home.GetComponent<PopJourneyHomeView>());
        var font=(TMP_FontAsset)typeof(PopJourneyHomeAuthoring).GetMethod("Font",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{"Accent Bold","Assets/TextMesh Pro/Examples & Extras/Fonts/Roboto-Bold.ttf"});
        TMP_FontAsset titleFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PopJourneyHomeAuthoring.ArtPath+"/Bangers.asset");titleFont.fallbackFontAssetTable=new(){font};EditorUtility.SetDirty(titleFont);AssetDatabase.SaveAssets();
        PrefabUtility.SaveAsPrefabAsset(contents,PopJourneyHomeAuthoring.PrefabPath);PrefabUtility.UnloadPrefabContents(contents);
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Home.unity");
        var ui=Object.FindFirstObjectByType<PopGameplayUI>();PopJourneyHomeAuthoring.Validate(ui);
        Check(ui.View(PopUIPage.Home).GetComponent<PopJourneyHomeView>()!=null,"Home is an authored Journey Focus prefab before Play");
        typeof(PopGameplayValidation).GetMethod("ConfigurePortraitGameView",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
        EditorApplication.isPaused=false;Application.runInBackground=true;Application.logMessageReceived+=OnLog;EditorApplication.update+=Tick;deadline=EditorApplication.timeSinceStartup+150;
        EditorApplication.EnterPlaymode();next=EditorApplication.timeSinceStartup+.5;
    }
    private static void Tick()
    {
        try
        {
            if(EditorApplication.isPlaying)EditorApplication.QueuePlayerLoopUpdate();
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Journey Home timeout stage "+stage);
            if(EditorApplication.timeSinceStartup<next||EditorApplication.isCompiling)return;
            if(stage==9)
            {if(EditorApplication.isPlayingOrWillChangePlaymode)return;Check(errors.Count==0,string.Join(";",errors));File.WriteAllText(output+"/results.txt","JOURNEY HOME PASS: "+checks+" checks; authored objects, real progress, no Tap prompt, settings, Classic save/resume, suggested Journey, safe-area button bounds, back-to-Home instant reveal.");Debug.Log(File.ReadAllText(output+"/results.txt"));EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;EditorApplication.Exit(0);return;}
            if(!EditorApplication.isPlaying||PopGameplayUI.Instance==null)return;
            PopGameplayUI ui=PopGameplayUI.Instance;PopGameplaySession session=PopGameplaySession.Instance;
            PopUIView view=ui.View(PopUIPage.Home);PopJourneyHomeView home=view.GetComponent<PopJourneyHomeView>();
            if(stage==0)
            {
                if(home!=null&&view.gameObject.activeInHierarchy&&home.actions.alpha<.99f&&!openedDuringIntro)
                {view.Element("Settings").button.onClick.Invoke();openedDuringIntro=true;}
                if(home==null||!view.gameObject.activeInHierarchy||home.actions.alpha<.99f)return;
                if(openedDuringIntro)Check(home.IsSettingsOpen,"Intro completion does not dismiss settings opened during the entrance");home.OpenSettings(false);
                Check(Object.FindFirstObjectByType<BlokkiPopIntroController>().IsAnimationFinished,"Home entrance completes with visible authored hero");
                foreach(RectTransform cell in home.cells)Check(cell.gameObject.activeInHierarchy&&cell.GetComponent<CanvasGroup>().alpha>.99f,"Every colored intro block is visible after landing");
                Check(Read<Object>(Object.FindFirstObjectByType<BlokkiPopIntroController>(),"startButton")==null,"New Home has no Tap-to-Start button binding");
                session.Progress.journey.Clear();for(int i=0;i<2;i++)PopSaveStore.RecordWin(session.Progress.journey,session.Configuration.journeyLevels[i].id,3,1,43);
                session.SelectTheme("sky");ui.ShowHome();
                Check(home.stars.text=="6"&&home.SuggestedLevel==2,"Stars and next Journey come from real saved progress, not mockup numbers");
                Check(home.nodes[0].star.gameObject.activeSelf&&home.nodes[1].star.gameObject.activeSelf&&home.nodes[2].play.gameObject.activeSelf&&home.nodes[3].locked.gameObject.activeSelf,"Journey track shows two completed levels, current play, next lock");
                Check(!home.nodes[3].button.interactable,"Locked progress node cannot be played");
                foreach(TMP_Text text in view.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate();
                foreach(string key in new[]{"Journey","Continue","Classic","Daily","Rewards","Settings","Levels"})
                {Rect r=Bounds(view.Element(key).rect);Check(r.xMin>=-.5f&&r.xMax<=Screen.width+.5f&&r.yMin>=-.5f&&r.yMax<=Screen.height+.5f,key+" fits portrait screen");if(view.Element(key).rect.gameObject.activeInHierarchy){var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=r.center},hits);Check(hits.Exists(hit=>hit.gameObject.GetComponentInParent<UnityEngine.UI.Button>()==view.Element(key).button),key+" receives real UI pointer raycasts, including transparent icon buttons");}}
                classic=session.SavedRun(PopGameMode.Classic);Read<PopSaveStore>(session,"store").ClearRun(PopGameMode.Classic);ui.ShowHome();Check(!view.Element("Continue").rect.gameObject.activeSelf,"Continue is hidden when there is no Classic run");
                if(classic!=null){Read<PopSaveStore>(session,"store").SaveRun(classic);ui.ShowHome();Check(view.Element("Continue").rect.gameObject.activeSelf,"Continue reappears for a valid saved run");}
                view.Element("Settings").button.onClick.Invoke();Check(home.IsSettingsOpen&&PopGameplayUI.IsModalOpen,"Gear opens the real settings sheet and blocks input behind it");
                int sound=PlayerPrefs.GetInt("blokkipop.home.sound.v1",1);view.Element("Sound").button.onClick.Invoke();Check(PlayerPrefs.GetInt("blokkipop.home.sound.v1")==1-sound,"Sound toggle persists");view.Element("Sound").button.onClick.Invoke();
                bool vibration=GameHaptics.IsEnabled;view.Element("Haptics").button.onClick.Invoke();Check(GameHaptics.IsEnabled!=vibration,"Haptics toggle persists");view.Element("Haptics").button.onClick.Invoke();
                ScreenCapture.CaptureScreenshot(output+"/settings.png");stage=1;next=EditorApplication.timeSinceStartup+.25;
            }
            else if(stage==1)
            {view.Element("SettingsClose").button.onClick.Invoke();Check(!home.IsSettingsOpen,"Settings close restores Home");ScreenCapture.CaptureScreenshot(output+"/home.png");stage=2;next=EditorApplication.timeSinceStartup+.3;}
            else if(stage==2)
            {
                view.Element("Journey").button.onClick.Invoke();Check(ui.View(PopUIPage.Level).gameObject.activeInHierarchy&&ui.View(PopUIPage.Level).title.text.Contains("3"),"Primary Journey opens the actual next level's goals");
                ui.View(PopUIPage.Level).Element("Play").button.onClick.Invoke();stage=3;next=EditorApplication.timeSinceStartup+.7;
            }
            else if(stage==3)
            {
                if(SceneManager.GetActiveScene().buildIndex!=1||!session.IsActive)return;
                Check(session.Mode==PopGameMode.Journey&&session.LevelId==session.Configuration.journeyLevels[2].id,"Primary Journey enters the preloaded correct level");session.GoHome();stage=4;next=EditorApplication.timeSinceStartup+.5;
            }
            else if(stage==4)
            {
                if(SceneManager.GetActiveScene().buildIndex!=0||home==null||home.actions.alpha<.99f)return;
                Check(Object.FindFirstObjectByType<BlokkiPopIntroController>().IsAnimationFinished,"Returning Home skips the cold-start intro");
                view.Element("Levels").button.onClick.Invoke();Check(ui.View(PopUIPage.Journey).gameObject.activeInHierarchy,"Journey heading opens the full level picker");ui.View(PopUIPage.Journey).Element("Back").button.onClick.Invoke();
                view.Element("Daily").button.onClick.Invoke();Check(ui.View(PopUIPage.Daily).gameObject.activeInHierarchy,"Daily shortcut works");ui.View(PopUIPage.Daily).Element("Back").button.onClick.Invoke();
                view.Element("Rewards").button.onClick.Invoke();Check(ui.View(PopUIPage.Rewards).gameObject.activeInHierarchy,"Rewards shortcut works");ui.View(PopUIPage.Rewards).Element("Back").button.onClick.Invoke();
                if(classic!=null){view.Element("Continue").button.onClick.Invoke();stage=5;next=EditorApplication.timeSinceStartup+.7;}
                else{EditorApplication.ExitPlaymode();stage=9;}
            }
            else if(stage==5)
            {
                if(SceneManager.GetActiveScene().buildIndex!=1||!session.IsActive)return;
                session.SaveCurrentRun();Check(JsonUtility.ToJson(session.SavedRun(PopGameMode.Classic))==JsonUtility.ToJson(classic),"Classic continue preserves board/tray/RNG/score exactly");EditorApplication.ExitPlaymode();stage=9;
            }
        }
        catch(Exception e){Debug.LogError("JOURNEY HOME FAIL: "+e);File.WriteAllText(output+"/results.txt",e.ToString());EditorApplication.update-=Tick;EditorApplication.Exit(1);}
    }
    private static T Read<T>(object o,string name)=>(T)o.GetType().GetField(name,Private).GetValue(o);
    private static Rect Bounds(RectTransform r){var p=new Vector3[4];r.GetWorldCorners(p);Vector2 a=RectTransformUtility.WorldToScreenPoint(null,p[0]),b=RectTransformUtility.WorldToScreenPoint(null,p[2]);return Rect.MinMaxRect(a.x,a.y,b.x,b.y);}
    private static void Check(bool value,string reason){++checks;if(!value)throw new Exception(reason);}
    private static void OnLog(string text,string stack,LogType type){if(!stack.Contains("UnityEditor.Search.SearchDatabase")&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))errors.Add(text);}
}
