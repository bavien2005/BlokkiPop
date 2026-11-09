using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Only the isolated fixture may exercise scores, saves and scene navigation.</summary>
public static class PopAmbientValidation
{
    private static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    private static string Output => Path.GetFullPath("../ambient-pop-20261018");
    private static readonly List<string> errors = new();
    private static int checks, stage, dailyMove, cycles;
    private static double next, deadline;
    private static string boardBefore, savedId;
    private static uint savedRandom, witnessSeed;
    private static int savedStage, savedScore;
    private static float pausedTime, pausedAlpha;
    private static Vector3 cameraBefore;
    private static bool recording;
    private static int recordFrame = -1, recordIndex;
    private static double nextRecord;

    public static void Run()
    {
        if (Application.companyName != "BlokkiPopRegression" || !Application.dataPath.Replace('\\','/').Contains("/.utmp/domain-reload-validation-20261009/")) throw new Exception("Never run score/save QA on the real player's project.");
        Directory.CreateDirectory(Output); PopAmbientAuthoring.Install(); PopAmbientAuthoring.Install();
        var config = AssetDatabase.LoadAssetAtPath<PopAmbientConfiguration>(PopAmbientConfiguration.AssetPath); config.enabled = true; config.reducedMotion = false;
        Pure(config); EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
        var gameplay = AssetDatabase.LoadAssetAtPath<PopGameplayConfiguration>(PopGameplayConfiguration.AssetPath); gameplay.runs.classicRewardedRevive = false; EditorUtility.SetDirty(gameplay); AssetDatabase.SaveAssets();
        // Verify authored edit-mode scenery/particles without entering Play.
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity");
        var preview = Object.FindFirstObjectByType<PopAmbientBackground>();
        Check(preview != null && preview.HasPresentation, "Authored scene rig has all art/particle references");
        preview.scenePreview = 1; typeof(PopAmbientBackground).GetMethod("Preview",Flags).Invoke(preview,null);
        Check(preview.layerA.sprite == preview.colorSurface && preview.snow.particleCount > 0, "Snow colour and particles can be previewed before Play");
        config.particleDensity=0;typeof(PopAmbientConfiguration).GetMethod("OnValidate",Flags).Invoke(config,null);
        typeof(PopAmbientBackground).GetMethod("Preview",Flags).Invoke(preview,null);
        Check(preview.snow.particleCount==0,"Inspector density zero also stops particles in edit-mode preview");
        config.particleDensity=1;typeof(PopAmbientConfiguration).GetMethod("OnValidate",Flags).Invoke(config,null);
        typeof(PopAmbientBackground).GetMethod("Preview",Flags).Invoke(preview,null);
        Check(preview.snow.particleCount>0,"Inspector particle edits refresh the preview without toggling Scene Preview");
        preview.scenePreview = 0; typeof(PopAmbientBackground).GetMethod("Preview",Flags).Invoke(preview,null);
        Check(preview.layerA.sprite == null && preview.layerB.sprite == null, "Default edit preview restores the authored default background");
        Check(PopAmbientBackground.BounceAt(0,2)==0&&PopAmbientBackground.BounceAt(1,2)==0&&PopAmbientBackground.BounceAt(.1f,2)>.7f&&PopAmbientBackground.BounceAt(.3f,2)<-.4f,"POP has a visible rebound and settles exactly to zero");
        preview.scenePreview=2;preview.refreshPreview=.15f;typeof(PopAmbientBackground).GetMethod("Preview",Flags).Invoke(preview,null);
        Check(preview.IsTransitioning&&Mathf.Abs(preview.PresentationBounce)>.2f&&preview.transitionBurst.particleCount>0,"Editor scrub shows a real POP wave, bounce and star burst without Play; transition="+preview.IsTransitioning+", bounce="+preview.PresentationBounce+", burst="+preview.transitionBurst.particleCount);
        preview.scenePreview=0;preview.refreshPreview=1;typeof(PopAmbientBackground).GetMethod("Preview",Flags).Invoke(preview,null);
        typeof(PopEndlessPresentationValidation).GetMethod("ConfigureSize",Flags).Invoke(null,new object[]{new Vector2Int(720,1600)});
        Application.runInBackground = true; EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        Application.logMessageReceived += OnLog; EditorApplication.update += Tick; deadline = EditorApplication.timeSinceStartup + 340;
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/Home.unity"); EditorApplication.EnterPlaymode(); Arm(0,.8);
    }
    private static void Pure(PopAmbientConfiguration config)
    {
        Check(config.StageFor(0)==0 && config.StageFor(999)==0 && config.StageFor(1000)==1 && config.StageFor(1999)==1 && config.StageFor(2000)==2, "Milestones use current run score at exact boundaries");
        Check(config.StageFor(int.MaxValue)>0, "Large scores cannot overflow milestone arithmetic");
        var randomBefore=UnityEngine.Random.state;
        var state=new PopAmbientRunState{randomState=12345};
        for(int i=1;i<=300;i++)
        {
            string previous=state.sceneId; Check(state.Advance(config,i*1000),"Advance score milestone "+i);
            Check(state.stage==i && state.sceneId!=previous && config.Find(state.sceneId)!=null,"Random scene is valid and never immediately repeats");
            uint seed=state.randomState; string id=state.sceneId; Check(!state.Advance(config,i*1000+99) && state.randomState==seed && state.sceneId==id,"No reroll within the same milestone");
            var restored=state.Copy(); Check(!restored.Advance(config,i*1000) && restored.sceneId==state.sceneId,"Resume does not reroll an earned scene");
        }
        Check(UnityEngine.Random.state.Equals(randomBefore),"Cosmetic RNG never consumes Unity Random / block-color RNG");
        // Find one reproducible production-RNG sequence, not a forced renderer override.
        for(uint seed=1;seed<10000;seed++)
        {
            var probe=new PopAmbientRunState{randomState=seed}; probe.Advance(config,1000); if(probe.sceneId!="snow")continue;
            probe.Advance(config,2000); if(probe.sceneId!="stars")continue; probe.Advance(config,3000); if(probe.sceneId!="sunset")continue;
            witnessSeed=seed; break;
        }
        Check(witnessSeed!=0,"A deterministic random witness visits all three art scenes");
        Check(!config.AppliesTo(PopGameMode.Daily) && !config.AppliesTo(PopGameMode.Journey) && config.AppliesTo(PopGameMode.Classic) && config.AppliesTo(PopGameMode.EndlessPop),"Only the two endless modes use score ambience");
        Check(!ShaderUtil.ShaderHasError(AssetDatabase.LoadAssetAtPath<Shader>(PopAmbientAuthoring.ArtPath+"/AmbientParticles.shader")),"Ambient particle shader compiles");
        Check(!ShaderUtil.ShaderHasError(AssetDatabase.LoadAssetAtPath<Shader>(PopAmbientAuthoring.ArtPath+"/AmbientColour.shader")),"Colour transition shader compiles");
    }
    private static void Tick()
    {
        try
        {
            if(EditorApplication.isPlaying)EditorApplication.QueuePlayerLoopUpdate();
            if(recording&&EditorApplication.isPlaying&&Time.frameCount!=recordFrame&&EditorApplication.timeSinceStartup>=nextRecord)
            {recordFrame=Time.frameCount;nextRecord=EditorApplication.timeSinceStartup+.05;Capture("refresh-frame-"+(recordIndex++).ToString("D3"));}
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timeout stage "+stage);
            if(EditorApplication.timeSinceStartup<next || EditorApplication.isCompiling)return;
            if(stage==99)
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                Check(ReferenceEquals(PopGameplaySession.Instance,null),"Session clears with Domain Reload off");
                if(cycles++==0){EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity"); EditorApplication.EnterPlaymode();Arm(90,.8);return;}
                Check(errors.Count==0,"No game/shader/particle runtime errors: "+string.Join(";",errors));
                var snowPixel=SampleBackground("snow");var starsPixel=SampleBackground("stars");var fireflyPixel=SampleBackground("sunset");
                Check(Vector3.Distance(snowPixel,starsPixel)>.12f&&Vector3.Distance(starsPixel,fireflyPixel)>.12f&&Vector3.Distance(snowPixel,fireflyPixel)>.12f,"Native screenshot pixels actually change colour between all three palettes, not just renderer properties");
                string result="AMBIENT PASS: "+checks+" checks; 1000-point boundaries, independent random/no repeats, 3 actual themed renders + particles, pause, exact Classic/Endless resume, game-over/new-run reset, reduced motion, no new runtime hierarchy, edit preview and Daily buttons. Two Domain-Reload-off Play cycles.";
                File.WriteAllText(Output+"/results.txt",result);Debug.Log(result);Finish(0);return;
            }
            var session=PopGameplaySession.Instance;var ui=PopGameplayUI.Instance;var manager=GameFlowManager._instance;
            if(!EditorApplication.isPlaying||session==null||ui==null)return;
            var rig=Object.FindFirstObjectByType<PopAmbientBackground>();
            switch(stage)
            {
                case 0:
                    if(SceneManager.GetActiveScene().buildIndex!=0||!ui.View(PopUIPage.Home).gameObject.activeInHierarchy)return;
                    session.Progress.selectedTheme="night-glow";Check(session.SelectedTheme.id=="sky","A legacy manual theme cannot override the default new-run background/palette");
                    Check(ui.View(PopUIPage.Home).Element("Rewards").text.text.Contains("LUYỆN"),"Home no longer offers manual theme selection");
                    ui.View(PopUIPage.Home).Element("Rewards").button.onClick.Invoke();Check(ui.View(PopUIPage.Journey).gameObject.activeInHierarchy&&!ui.View(PopUIPage.Rewards).gameObject.activeInHierarchy,"Old palette tile routes to existing practice, not theme shop");
                    ui.View(PopUIPage.Journey).Element("Back").button.onClick.Invoke();session.PlayClassic(false);Arm(1,.6);break;
                case 1:
                    if(rig==null||!rig.IsReady||!session.IsActive||session.Mode!=PopGameMode.Classic)return;
                    Check(rig.HasPresentation&&rig.CurrentSceneId==""&&session.Ambience.stage==0,"New Classic run begins on default blue");
                    session.Ambience.randomState=witnessSeed;boardBefore=Fingerprint(session);cameraBefore=Camera.main.transform.position;
                    manager.AddCoin(999);Check(session.Ambience.stage==0,"999 points do not trigger a scene");
                    manager.AddCoin(1);Check(session.Ambience.stage==1&&session.Ambience.sceneId=="snow","1000 points trigger the randomly selected snow scene");Arm(101,.09);break;
                case 101:
                    if(!rig.IsTransitioning||rig.TransitionProgress<=0)return;
                    Check(rig.TransitionProgress<1&&rig.transitionBurst.particleCount>0,"Milestone plays POP and two-sided star cannons, not just a fade");
                    var matrixBefore=Camera.main.projectionMatrix;var pointBefore=Camera.main.ScreenToWorldPoint(new Vector3(100,400,10));
                    typeof(PopAmbientBackground).GetMethod("OnCameraBegin",Flags).Invoke(rig,new object[]{Camera.main});
                    Check(Camera.main.projectionMatrix!=matrixBefore,"Render callback applies actual screen rebound");
                    typeof(PopAmbientBackground).GetMethod("OnCameraEnd",Flags).Invoke(rig,new object[]{Camera.main});
                    Check(Camera.main.projectionMatrix==matrixBefore&&Camera.main.ScreenToWorldPoint(new Vector3(100,400,10))==pointBefore,"Projection is restored before raycasts; input coordinates remain exact");
                    float sizeBefore=Camera.main.orthographicSize;Camera.main.orthographicSize=sizeBefore*1.1f;
                    Check(Camera.main.projectionMatrix!=matrixBefore,"Pulse restores automatic camera projection, so future responsive resize still works");Camera.main.orthographicSize=sizeBefore;
                    var input=typeof(BlockPointerInput).GetField("instance",Flags).GetValue(null);
                    var dragField=typeof(BlockPointerInput).GetField("dragging",Flags);var oldDrag=dragField.GetValue(input);
                    try {dragField.SetValue(input,Object.FindFirstObjectByType<Blocks>().GetComponentInChildren<Block>());Check(BlockPointerInput.IsDragging&&rig.PresentationBounce==0,"Picking up a block immediately removes visual bounce without changing input ownership");}
                    finally {dragField.SetValue(input,oldDrag);}
                    Capture("refresh-impact");Arm(2,1.1);break;
                case 2:
                    CheckScene(rig,session,"snow",rig.snow);Capture("snow");
                    Check(Camera.main.transform.position==cameraBefore&&rig.PresentationBounce==0,"POP settles exactly and never alters the authored camera transform");Check(Fingerprint(session)==boardBefore,"Scenery never changes board/tray/gameplay RNG");
                    Arm(20,.25);break;
                case 20:
                    uint seed=session.Ambience.randomState;manager.AddCoin(50);Check(session.Ambience.sceneId=="snow"&&session.Ambience.randomState==seed,"1050 points do not trigger another random draw");
                    ui.ShowPause();pausedTime=rig.snow.time;Arm(3,.4);break;
                case 3:
                    Check(Mathf.Abs(rig.snow.time-pausedTime)<.02f&&rig.CurrentSceneId=="snow","Pause freezes snow without resetting the theme");
                    ui.View(PopUIPage.Pause).Element("Resume").button.onClick.Invoke();ui.ShowPause();ui.View(PopUIPage.Pause).Element("Home").button.onClick.Invoke();Arm(4,.6);break;
                case 4:
                    if(SceneManager.GetActiveScene().buildIndex!=0)return;
                    var saved=session.SavedRun(PopGameMode.Classic);savedId=saved.ambience.sceneId;savedStage=saved.ambience.stage;savedRandom=saved.ambience.randomState;savedScore=saved.score;
                    Check(savedId=="snow"&&savedStage==1,"Navigating Home retains run-local ambience in the Classic save");session.PlayClassic(true);Arm(5,.6);break;
                case 5:
                    if(rig==null||!rig.IsReady||!session.IsActive||SceneManager.GetActiveScene().buildIndex!=1)return;
                    Check(session.Ambience.sceneId==savedId&&session.Ambience.stage==savedStage&&session.Ambience.randomState==savedRandom&&manager.Score==savedScore,"Classic Continue restores the exact score/theme/cosmetic RNG");
                    Check(rig.CurrentSceneId=="snow"&&!rig.IsTransitioning&&rig.transitionBurst.particleCount==0,"Continue does not replay a milestone celebration; actual="+rig.CurrentSceneId+", fade="+rig.IsTransitioning+", burst="+rig.transitionBurst.particleCount+", ready="+session.IsInitialized);
                    boardBefore=Fingerprint(session);manager.AddCoin(2000-manager.Score);Arm(6,.25);break;
                case 6:
                    Check(session.Ambience.sceneId=="stars"&&session.Ambience.stage==2,"2000 points choose another scene with the independent RNG");
                    ui.ShowPause();pausedAlpha=rig.TransitionProgress;Arm(60,.35);break;
                case 60:
                    float alpha=rig.TransitionProgress;
                    Check(Mathf.Abs(alpha-pausedAlpha)<.01f&&rig.PresentationBounce==0,"Pause freezes the radial refresh and removes bounce while its menu is open");ui.View(PopUIPage.Pause).Element("Resume").button.onClick.Invoke();Arm(7,1.6);break;
                case 7:
                    CheckScene(rig,session,"stars",rig.stars);Capture("stars");Check(Fingerprint(session)==boardBefore,"Second milestone keeps tray/deck/RNG untouched");
                    Arm(73,.25);break;
                case 73:
                    recording=true;recordIndex=0;recordFrame=-1;nextRecord=0;manager.AddCoin(1000);Arm(8,1.7);break;
                case 8:
                    recording=false;Check(recordIndex>=10,"Native transition recording contains enough live frames to show movement");
                    CheckScene(rig,session,"sunset",rig.fireflies);Capture("sunset");
                    Arm(81,.25);break;
                case 81:
                    rig.configuration.reducedMotion=true;Arm(80,.2);break;
                case 80:
                    Check(rig.snow.particleCount+rig.stars.particleCount+rig.fireflies.particleCount+rig.transitionBurst.particleCount==0,"Reduced motion immediately disables all ambient particles");
                    manager.AddCoin(10000);Arm(9,.2);break;
                case 9:
                    Check(session.Ambience.stage==13&&!rig.IsTransitioning,"A large score jump coalesces to one latest scene, no flash/animation backlog");
                    rig.configuration.reducedMotion=false;manager.GameOver();Arm(10,.8);break;
                case 10:
                    Check(!session.IsActive&&session.Ambience.stage==0&&rig.CurrentSceneId==""&&rig.layerA.sprite==null&&rig.layerB.sprite==null,"Terminal loss returns to authored default even at timeScale zero");
                    session.PlayClassic(false);Arm(11,.6);break;
                case 11:
                    if(!session.IsActive||rig==null||!rig.IsReady)return;
                    Check(manager.Score==0&&session.Ambience.stage==0&&rig.CurrentSceneId=="","New game does not inherit the previous run background");session.PlayEndless(false);Arm(12,.6);break;
                case 12:
                    if(!session.IsActive||!session.IsEndless||rig==null||!rig.IsReady)return;
                    session.Ambience.randomState=witnessSeed;boardBefore=Fingerprint(session);manager.AddCoin(1000);Arm(13,1.6);break;
                case 13:
                    CheckScene(rig,session,"snow",rig.snow);Check(Fingerprint(session)==boardBefore,"Endless Pop witness/deck/generator RNG unchanged by ambience");
                    session.SaveBeforeNavigation();var endless=session.SavedRun(PopGameMode.EndlessPop);savedRandom=endless.ambience.randomState;savedId=endless.ambience.sceneId;
                    session.GoHome();Arm(14,.5);break;
                case 14:
                    if(SceneManager.GetActiveScene().buildIndex!=0)return;session.PlayEndless(true);Arm(15,.6);break;
                case 15:
                    if(!session.IsActive||!session.IsEndless||rig==null||!rig.IsReady)return;
                    Check(session.Ambience.sceneId==savedId&&session.Ambience.randomState==savedRandom&&rig.CurrentSceneId==savedId,"Endless Pop Continue also keeps its own background save");manager.GameOver();Arm(16,.8);break;
                case 16:
                    Check(rig.CurrentSceneId==""&&!session.IsActive,"Endless Pop loss resets background");session.PlayDaily(false);dailyMove=0;Arm(17,.6);break;
                case 17:
                    if(!session.IsActive&& !manager.IsGameOver||session.Mode!=PopGameMode.Daily||rig==null||!rig.IsReady)return;
                    if(session.IsActive)
                    {
                        if(dailyMove==0){manager.AddCoin(5000);Check(session.Ambience.stage==0&&rig.CurrentSceneId=="","Daily never changes background from score");}
                        PlayWitness(session,ref dailyMove);Arm(17,.06);break;
                    }
                    Check(ui.View(PopUIPage.Victory).gameObject.activeInHierarchy&&!ui.View(PopUIPage.Victory).Element("Next").rect.gameObject.activeSelf,"Daily victory hides the reused Next/Home button");
                    Check(ui.View(PopUIPage.Victory).Element("Home").button.gameObject.activeInHierarchy&&ui.View(PopUIPage.Victory).Element("Retry").button.gameObject.activeInHierarchy,"Daily victory has one Home and one Retry");
                    Check(rig.CurrentSceneId==""&&session.Ambience.stage==0,"Win also leaves the default background");Capture("daily-win");Arm(18,.25);break;
                case 18:
                    EditorApplication.ExitPlaymode();Arm(99,.6);break;
                case 90:
                    if(!session.IsActive||rig==null||!rig.IsReady)return;
                    Check(session.Mode==PopGameMode.Classic&&manager.Score==0&&rig.CurrentSceneId==""&&session.Ambience.stage==0,"Second Play with Domain Reload off starts clean");EditorApplication.ExitPlaymode();Arm(99,.5);break;
            }
        }
        catch(Exception e){string message="AMBIENT FAIL stage "+stage+": "+e;File.WriteAllText(Output+"/results.txt",message);Debug.LogError(message);Finish(1);}
    }
    private static void CheckScene(PopAmbientBackground rig,PopGameplaySession session,string id,ParticleSystem ps)
    {
        Check(rig.CurrentSceneId==id&&!rig.IsTransitioning&&session.Ambience.sceneId==id,"Actual renderer settled on "+id);
        var visible=rig.layerA.color.a>.99f&&rig.layerA.sprite!=null?rig.layerA:rig.layerB;
        Check(visible.sprite==rig.colorSurface&&visible.color.a>.99f&&visible.sharedMaterial.shader.name=="Blokki Pop/Ambient Colour","Theme uses a simple shader colour surface, not a landscape illustration");
        var properties=new MaterialPropertyBlock();visible.GetPropertyBlock(properties);
        Check(properties.GetColor("_Top")==session.AmbientConfiguration.Find(id).topColor,"Actual renderer uses the Inspector palette colours");
        Check(ps.isPlaying&&ps.particleCount>0&&ps.particleCount<=ps.main.maxParticles,"Live "+id+" particles are emitted within their budget");
        Check(ps.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.isSupported,"Particle material is supported");
        Check(rig.transform.childCount==6&&Object.FindObjectsByType<PopAmbientBackground>(FindObjectsSortMode.None).Length==1,"Exactly one authored rig; no runtime object generation");
        foreach(var renderer in rig.GetComponentsInChildren<Renderer>())Check(renderer.sortingOrder<0,"Background art/particles remain behind board, never on top of tray/input");
    }
    private static string Fingerprint(PopGameplaySession session)
    {
        session.SaveCurrentRun();var snapshot=session.SavedRun(session.Mode);snapshot.score=0;snapshot.ambience=null;return JsonUtility.ToJson(snapshot);
    }
    private static void PlayWitness(PopGameplaySession session,ref int step)
    {
        Check(step<session.Definition.verifiedSolution.Count,"Daily witness still has a legal move");var move=session.Definition.verifiedSolution[step];
        var blocks=Object.FindFirstObjectByType<Blocks>();session.SaveCurrentRun();var run=session.SavedRun(session.Mode);
        for(int slot=0;slot<3;slot++)if(run.trayAvailable[slot]&&run.trayShapes[slot]==move.shape)
        {Check(blocks.TryPlaceSlot(slot,move.origin),"Daily uses real gameplay placement");step++;return;}
        throw new Exception("Witness shape missing from current tray.");
    }
    private static void Capture(string name){Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(Output+"/"+name+".png");}
    private static Vector3 SampleBackground(string name)
    {
        var texture=new Texture2D(2,2);
        try { Check(texture.LoadImage(File.ReadAllBytes(Output+"/"+name+".png")),"Native screenshot exists: "+name);Color color=texture.GetPixel(8,texture.height/2);return new Vector3(color.r,color.g,color.b); }
        finally {Object.DestroyImmediate(texture);}
    }
    private static void Check(bool value,string reason){checks++;if(!value)throw new Exception(reason);}
    private static void Arm(int value,double delay){stage=value;next=EditorApplication.timeSinceStartup+delay;}
    private static void OnLog(string message,string stack,LogType type){if(!stack.Contains("UnityEditor.Search.SearchDatabase")&&(type==LogType.Error||type==LogType.Assert||type==LogType.Exception))errors.Add(message);}
    private static void Finish(int code){EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;EditorApplication.Exit(code);}
}
