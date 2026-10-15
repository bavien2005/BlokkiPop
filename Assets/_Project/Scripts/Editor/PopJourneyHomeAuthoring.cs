using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicit, backed-up Home-only migration. Never rebuilds this design once installed.</summary>
public static class PopJourneyHomeAuthoring
{
    public const string PrefabPath="Assets/_Project/Prefabs/UI/PopJourneyFocusUI.prefab";
    public const string ArtPath="Assets/_Project/UI/JourneyFocus";
    private const string HomePath="Assets/_Project/Scenes/Home.unity";
    private static TMP_FontAsset heading,body;
    private static readonly Color Ink=new(.04f,.2f,.29f),White=new(.97f,.99f,1),Gold=new(1,.84f,.36f),Dark=new(.05f,.24f,.32f);
    [MenuItem("Tools/Blokki Pop/UI/Install Journey Focus Home")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||BuildPipeline.isBuildingPlayer)throw new InvalidOperationException("Stop Play/Build before installing Home.");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save open scenes first. Home installer refuses to overwrite unsaved edits.");
        SceneSetup[] setup=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Scene scene=EditorSceneManager.OpenScene(HomePath);
            PopGameplayUI old=Object.FindFirstObjectByType<PopGameplayUI>();
            if(old!=null&&old.View(PopUIPage.Home).GetComponent<PopJourneyHomeView>()!=null)
            { EnsureRaycastFlags(); Validate(old); Debug.Log("[Journey Home] Already installed; existing designer edits preserved."); return; }
            string backup=Path.GetFullPath(".utmp/journey-home-20261015/backups/Home.unity");Directory.CreateDirectory(Path.GetDirectoryName(backup));if(!File.Exists(backup))File.Copy(HomePath,backup);
            if(old==null)throw new InvalidOperationException("The authored Home UI is missing; refusing an ambiguous replacement.");
            EnsurePrefab(old);EnsureRaycastFlags();
            Undo.DestroyObjectImmediate(old.gameObject);
            GameObject root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),scene);root.name="Blokki Pop UI";
            PopGameplayUI ui=root.GetComponent<PopGameplayUI>();ui.ScenePreview=PopUIPage.Home;ui.PreviewScenePage();PrefabUtility.RecordPrefabInstancePropertyModifications(ui);
            PopJourneyHomeView design=ui.View(PopUIPage.Home).GetComponent<PopJourneyHomeView>();
            BlokkiPopIntroController intro=Object.FindFirstObjectByType<BlokkiPopIntroController>();
            if(intro==null)throw new InvalidOperationException("Home's existing scene preloader is missing.");
            foreach(Canvas canvas in intro.GetComponentsInChildren<Canvas>(true))
            { canvas.gameObject.name="Legacy Intro Canvas (preserved)";canvas.gameObject.SetActive(false); }
            var serialized=new SerializedObject(intro);
            Set(serialized,"brandGroup",design.brand);Set(serialized,"logoRoot",design.logo);Set(serialized,"mainLogoText",design.logoText);Set(serialized,"popLogoText",null);Set(serialized,"taglineText",design.tagline);
            Set(serialized,"progressFill",null);Set(serialized,"loadingLabel",null);Set(serialized,"startButton",null);
            SetArray(serialized,"animatedCells",design.cells);SetArray(serialized,"animatedCellGroups",design.cellGroups);
            serialized.FindProperty("authoredHomeMenu").boolValue=true;serialized.FindProperty("applyInspectorFontSizes").boolValue=false;
            serialized.FindProperty("overlapCellLandings").boolValue=true;serialized.FindProperty("cellDropDistance").floatValue=145;
            serialized.FindProperty("cellDropVariation").floatValue=28;serialized.FindProperty("cellStaggerSeconds").floatValue=.065f;
            serialized.FindProperty("cellLandingDuration").floatValue=.32f;serialized.FindProperty("logoRevealDuration").floatValue=.24f;
            serialized.FindProperty("minimumIntroSeconds").floatValue=0;serialized.FindProperty("progressAnimationDuration").floatValue=.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(intro);
            Validate(ui);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("[Journey Home] Installed editable Journey Focus in Home only. Game scene/HUD and player saves untouched. Backup: "+backup);
        }
        finally { if(Array.Exists(setup,s=>s.isLoaded&&s.isActive))EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }
    private static void EnsurePrefab(PopGameplayUI template)
    {
        if(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)!=null)return;
        Folder("Assets/_Project/UI");Folder(ArtPath);
        body=Font("Body","Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
        heading=Font("Bangers","Assets/TextMesh Pro/Examples & Extras/Fonts/Bangers.ttf");
        heading.fallbackFontAssetTable=new(){Font("Accent Bold","Assets/TextMesh Pro/Examples & Extras/Fonts/Roboto-Bold.ttf")};EditorUtility.SetDirty(heading);
        Scene preview=EditorSceneManager.NewPreviewScene();
        GameObject root=Object.Instantiate(template.gameObject);root.name="Blokki Pop UI";
        if(PrefabUtility.IsPartOfPrefabInstance(root))PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        SceneManager.MoveGameObjectToScene(root,preview);root.SetActive(true);
        try
        {
            PopGameplayUI controller=root.GetComponent<PopGameplayUI>();PopUIView oldHome=controller.View(PopUIPage.Home);Transform parent=oldHome.transform.parent;Object.DestroyImmediate(oldHome.gameObject);
            RectTransform safe=(RectTransform)parent;safe.anchorMin=Vector2.zero;safe.anchorMax=Vector2.one;safe.anchoredPosition=Vector2.zero;safe.sizeDelta=Vector2.zero;
            RectTransform background=Rect("Journey Focus Backdrop",root.transform,Vector2.zero,Vector2.zero);Stretch(background);background.SetAsFirstSibling();
            PopHomeGraphic gradient=background.gameObject.AddComponent<PopHomeGraphic>();gradient.Shape=PopHomeGraphic.Kind.Rounded;gradient.Radius=0;gradient.color=Hex("144963");gradient.BottomTint=Hex("267890");gradient.raycastTarget=false;
            Blob("Cloud Left",background,new(-.03f,.69f),new(450,350));Blob("Cloud Right",background,new(.99f,.43f),new(680,410));
            RectTransform rect=Rect("Home",parent,Vector2.zero,Vector2.zero);Stretch(rect);rect.SetAsFirstSibling();PopUIView view=rect.gameObject.AddComponent<PopUIView>();view.page=PopUIPage.Home;
            PopJourneyHomeView home=rect.gameObject.AddComponent<PopJourneyHomeView>();
            RectTransform card=Rect("Card",rect,Vector2.zero,Vector2.zero);Stretch(card); // compatibility name; deliberately no opaque panel
            TMP_Text best=Text("Best",card,"BEST · 0",new(.06f,.962f),new(420,70),37,body,White);best.rectTransform.pivot=new(0,.5f);best.alignment=TextAlignmentOptions.Left;Bind(view,"Stats",best);
            Shape("Stars Icon",card,PopHomeGraphic.Kind.Play,new(.74f,.962f),new(36,36),White).gameObject.SetActive(false);
            RectTransform star=Rect("Stars Icon",card,new(.735f,.962f),new(38,38));var sg=star.gameObject.AddComponent<PopStarGraphic>();sg.color=White;sg.raycastTarget=false;
            home.stars=Text("Stars",card,"0",new(.795f,.962f),new(100,70),37,body,White);
            MakeButton(view,"Settings",card,"",new(.905f,.962f),new(128,128),Dark,PopHomeGraphic.Kind.Gear,0,body);
            RectTransform brand=Rect("Brand and Mini Board",card,Vector2.zero,Vector2.zero);Stretch(brand);home.brand=brand.gameObject.AddComponent<CanvasGroup>();home.brand.blocksRaycasts=false;
            home.logo=Rect("Logo",brand,new(.5f,.854f),new(930,160));
            TMP_Text shadow=Text("Logo Shadow",home.logo,"BLOKKI POP",new(.5f,.5f),new(930,160),133,heading,Dark);shadow.rectTransform.anchoredPosition=new(0,-12);
            home.logoText=Text("Logo Text",home.logo,"BLOKKI <color=#FFD765>POP</color>",new(.5f,.5f),new(930,160),133,heading,White);home.logo.localEulerAngles=new(0,0,3);
            home.tagline=Text("Tagline",brand,"DROP · MATCH · POP",new(.5f,.787f),new(850,65),42,heading,White);
            RectTransform grid=Rect("Mini Board",brand,new(.5f,.635f),new(370,370));grid.localEulerAngles=new(0,0,5);
            var cells=new System.Collections.Generic.List<RectTransform>();var groups=new System.Collections.Generic.List<CanvasGroup>();
            for(int i=0;i<16;i++)
            {
                RectTransform cell=Rect("Grid "+i,grid,new(.5f,.5f),new(81,81));cell.anchoredPosition=new((i%4-1.5f)*93,(1.5f-i/4)*93);
                PopHomeGraphic square=cell.gameObject.AddComponent<PopHomeGraphic>();square.Radius=5;square.color=new(.64f,.88f,.97f,.19f);square.raycastTarget=false;
                bool cyan=i==4||i==5||i==9,gold=i==2||i==3||i==7;if(!cyan&&!gold)continue;
                RectTransform block=Rect(cyan?"Cyan Block "+i:"Gold Block "+i,grid,new(.5f,.5f),new(81,81));block.anchoredPosition=cell.anchoredPosition;
                var graphic=block.gameObject.AddComponent<PopHomeGraphic>();graphic.Radius=4;graphic.color=cyan?Hex("18D7E4"):Gold;graphic.raycastTarget=false;
                Shape("Highlight",block,PopHomeGraphic.Kind.Rounded,new(.5f,.5f),new(75,5),new(1,1,1,.65f)).rectTransform.anchoredPosition=new(0,37);
                cells.Add(block);groups.Add(block.gameObject.AddComponent<CanvasGroup>());
            }
            home.cells=cells.ToArray();home.cellGroups=groups.ToArray();
            RectTransform actions=Rect("Navigation",card,Vector2.zero,Vector2.zero);Stretch(actions);home.actions=actions.gameObject.AddComponent<CanvasGroup>();
            MakeButton(view,"Levels",actions,home.heading,new(.5f,.493f),new(950,90),Color.clear,null,70,heading);
            view.Element("Levels").updateText=false; // Title owns the same label; honor its designer Update Text flag.
            TMP_Text title=view.Element("Levels").text;view.title=title;view.elements.Add(new PopUIElement{key="Title",rect=title.rectTransform,text=title,graphic=title});
            Shape("Journey Track",actions,PopHomeGraphic.Kind.Rounded,new(.5f,.412f),new(720,14),new(.64f,.88f,.97f,.2f));
            home.nodes=new PopJourneyHomeView.Node[4];
            for(int i=0;i<4;i++)
            {
                string key="Progress"+(i+1).ToString("00");Vector2 anchor=new(.5f+(i-1.5f)*.16f,.412f);
                PopHomeGraphic ring=Shape("Current Ring "+i,actions,PopHomeGraphic.Kind.Circle,anchor,new(201,201),new(.64f,.88f,.97f,.25f));
                MakeButton(view,key,actions,"",anchor,new(160,160),i==0?Gold:Dark,null,0,body);PopUIElement item=view.Element(key);
                PopHomeGraphic play=Shape("Play",item.rect,PopHomeGraphic.Kind.Play,new(.5f,.5f),new(65,65),Ink);
                RectTransform st=Rect("Completed Star",item.rect,new(.5f,.5f),new(65,65));var starGraphic=st.gameObject.AddComponent<PopStarGraphic>();starGraphic.color=White;starGraphic.raycastTarget=false;
                PopHomeGraphic locked=Shape("Lock",item.rect,PopHomeGraphic.Kind.Lock,new(.5f,.5f),new(65,65),White);
                play.gameObject.SetActive(i==0);st.gameObject.SetActive(false);locked.gameObject.SetActive(i!=0);ring.gameObject.SetActive(i==0);
                home.nodes[i]=new(){button=item.button,play=play,star=starGraphic,locked=locked,ring=ring};
            }
            Shape("Journey Button Shadow",actions,PopHomeGraphic.Kind.Rounded,new(.5f,.286f),new(950,235),Hex("A97C30")).rectTransform.anchoredPosition=new(0,-16);
            MakeButton(view,"Journey",actions,home.journeyLabel,new(.5f,.286f),new(950,235),Gold,null,78,heading);
            PopUIElement journey=view.Element("Journey");journey.text.color=Ink;journey.text.rectTransform.anchoredPosition=new(0,28);journey.text.rectTransform.sizeDelta=new(910,110);
            home.journeySubtitle=Text("Journey Subtitle",journey.rect,home.journeyHint,new(.5f,.5f),new(900,60),37,body,Ink);home.journeySubtitle.rectTransform.anchoredPosition=new(0,-62);
            MakeButton(view,"Continue",actions,home.continueClassic,new(.5f,.18f),new(950,135),Dark,null,42,body);
            RectTransform resume=view.Element("Continue").rect;Shape("Play Icon",resume,PopHomeGraphic.Kind.Play,new(.19f,.5f),new(38,42),White);Shape("Arrow",resume,PopHomeGraphic.Kind.Chevron,new(.84f,.5f),new(35,40),White);
            string[] keys={"Classic","Daily","Rewards"},labels={home.classicLabel,home.dailyLabel,home.rewardsLabel};PopHomeGraphic.Kind[] icons={PopHomeGraphic.Kind.Blocks,PopHomeGraphic.Kind.Calendar,PopHomeGraphic.Kind.Palette};
            for(int i=0;i<3;i++){MakeButton(view,keys[i],actions,labels[i],new(.5f+(i-1)*.32f,.072f),new(280,160),Color.clear,null,34,body);PopUIElement item=view.Element(keys[i]);item.text.rectTransform.anchoredPosition=new(0,-48);item.text.rectTransform.sizeDelta=new(270,60);Shape("Icon",item.rect,icons[i],new(.5f,.5f),new(68,68),White).rectTransform.anchoredPosition=new(0,30);}
            BuildSettings(view,home,card);
            Polish(view,home);
            SerializedObject serialized=new(controller);SerializedProperty refs=serialized.FindProperty("views");for(int i=0;i<refs.arraySize;i++)if(refs.GetArrayElementAtIndex(i).objectReferenceValue==null)refs.GetArrayElementAtIndex(i).objectReferenceValue=view;
            serialized.FindProperty("scenePreview").enumValueIndex=(int)PopUIPage.Home;serialized.ApplyModifiedPropertiesWithoutUndo();
            controller.PreviewScenePage();NormalizeRoot((RectTransform)root.transform);PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }
        finally {Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview);}
        AssetDatabase.SaveAssets();
    }
    private static void BuildSettings(PopUIView view,PopJourneyHomeView home,Transform parent)
    {
        RectTransform overlay=Rect("Settings Sheet",parent,Vector2.zero,Vector2.zero);Stretch(overlay);home.settings=overlay.gameObject;
        var shade=overlay.gameObject.AddComponent<Image>();shade.color=new(.01f,.05f,.08f,.75f);shade.raycastTarget=true;
        RectTransform panel=Shape("Panel",overlay,PopHomeGraphic.Kind.Rounded,new(.5f,.5f),new(950,1120),Hex("173D52")).rectTransform;
        Text("Heading",panel,"CÀI ĐẶT",new(.5f,.91f),new(700,100),80,heading,White);
        MakeButton(view,"SettingsClose",panel,"×",new(.91f,.91f),new(105,105),Dark,null,65,body);
        string[] keys={"Sound","Haptics","Learn","Privacy"},labels={"Âm thanh","Rung",home.learnLabel,home.privacyLabel};
        for(int i=0;i<4;i++){MakeButton(view,keys[i],panel,labels[i],new(.5f,.72f-i*.18f),new(810,145),Dark,null,45,body);view.Element(keys[i]).text.alignment=TextAlignmentOptions.Left;view.Element(keys[i]).text.rectTransform.sizeDelta=new(620,100);}
        home.soundStatus=Text("Sound State",view.Element("Sound").rect,"BẬT",new(.84f,.5f),new(130,80),35,body,Gold);
        home.hapticsStatus=Text("Haptics State",view.Element("Haptics").rect,"BẬT",new(.84f,.5f),new(130,80),35,body,Gold);
        overlay.gameObject.SetActive(false);
    }
    public static void Validate(PopGameplayUI ui)
    {
        PopSceneUIAuthoring.Validate(ui,AssetDatabase.LoadAssetAtPath<PopGameplayConfiguration>(PopGameplayConfiguration.AssetPath));
        var home=ui.View(PopUIPage.Home).GetComponent<PopJourneyHomeView>();if(home==null||home.cells?.Length!=6||home.nodes?.Length!=4||home.actions==null||home.settings==null)throw new InvalidOperationException("Incomplete Journey Focus Home references.");
        foreach(string key in new[]{"Levels","Settings","SettingsClose","Sound","Haptics","Progress01","Progress02","Progress03","Progress04"})if(ui.View(PopUIPage.Home).Element(key).button==null)throw new InvalidOperationException("Missing Home button: "+key);
    }
    private static void EnsureRaycastFlags()
    {
        GameObject root=PrefabUtility.LoadPrefabContents(PrefabPath);bool changed=false;
        try
        {foreach(Button button in root.GetComponentsInChildren<Button>(true))if(button.targetGraphic!=null&&button.targetGraphic.canvasRenderer.cullTransparentMesh){button.targetGraphic.canvasRenderer.cullTransparentMesh=false;changed=true;}if(changed)PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);}
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    public static void Polish(PopUIView view,PopJourneyHomeView home)
    {
        foreach(var item in view.elements)if(item.button!=null&&item.button.targetGraphic!=null)item.button.targetGraphic.canvasRenderer.cullTransparentMesh=false;
        foreach(string name in new[]{"Journey","Continue"})
        {
            RectTransform r=view.Element(name).rect;Vector2 min=r.anchorMin,max=r.anchorMax;min.x=.06f;max.x=.94f;r.anchorMin=min;r.anchorMax=max;r.sizeDelta=new(0,r.sizeDelta.y);
            RectTransform label=view.Element(name).text.rectTransform;label.anchorMin=new(.04f,.5f);label.anchorMax=new(.96f,.5f);label.sizeDelta=new(0,label.sizeDelta.y);
        }
        RectTransform shade=(RectTransform)home.actions.transform.Find("Journey Button Shadow");shade.anchorMin=new(.06f,.286f);shade.anchorMax=new(.94f,.286f);shade.sizeDelta=new(0,235);
        home.journeySubtitle.rectTransform.anchorMin=new(.04f,.5f);home.journeySubtitle.rectTransform.anchorMax=new(.96f,.5f);home.journeySubtitle.rectTransform.sizeDelta=new(0,60);
        foreach(var node in home.nodes){((RectTransform)node.button.transform).sizeDelta=new(132,132);node.ring.rectTransform.sizeDelta=new(180,180);}
        ((RectTransform)home.actions.transform.Find("Journey Track")).sizeDelta=new(650,14);
        ((PopHomeGraphic)view.Element("Settings").graphic).Radius=100;
        ((PopHomeGraphic)view.Element("SettingsClose").graphic).Radius=100;
    }
    private static TMP_FontAsset Font(string name,string source)
    {
        string path=ArtPath+"/"+name+".asset";TMP_FontAsset font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);if(font!=null)return font;
        font=TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(source),90,9,GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);
        font.name="Journey Focus "+name;AssetDatabase.CreateAsset(font,path);AssetDatabase.AddObjectToAsset(font.material,font);foreach(Texture2D atlas in font.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,font);
        font.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ·×:<>/ÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚĂĐĨŨƠƯẠẢẤẦẨẪẬẮẰẲẴẶẸẺẼẾỀỂỄỆỈỊỌỎỐỒỔỖỘỚỜỞỠỢỤỦỨỪỬỮỰỲỴỶỸàáâãèéêìíòóôõùúăđĩũơưạảấầẩẫậắằẳẵặẹẻẽếềểễệỉịọỏốồổỗộớờởỡợụủứừửữựỳỵỷỹ",out _);EditorUtility.SetDirty(font);return font;
    }
    private static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;Folder(Path.GetDirectoryName(path).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));}
    private static Color Hex(string html){ColorUtility.TryParseHtmlString("#"+html,out Color c);return c;}
    private static void NormalizeRoot(RectTransform r){r.localScale=Vector3.one;r.anchorMin=r.anchorMax=r.pivot=new(.5f,.5f);r.anchoredPosition=Vector2.zero;r.sizeDelta=new(1080,1920);}
    private static RectTransform Rect(string name,Transform parent,Vector2 anchor,Vector2 size){var go=new GameObject(name,typeof(RectTransform));var r=(RectTransform)go.transform;r.SetParent(parent,false);r.anchorMin=r.anchorMax=anchor;r.pivot=new(.5f,.5f);r.sizeDelta=size;return r;}
    private static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    private static PopHomeGraphic Shape(string name,Transform parent,PopHomeGraphic.Kind kind,Vector2 anchor,Vector2 size,Color color){var r=Rect(name,parent,anchor,size);var g=r.gameObject.AddComponent<PopHomeGraphic>();g.Shape=kind;g.color=color;g.raycastTarget=false;return g;}
    private static void Blob(string name,Transform parent,Vector2 anchor,Vector2 size){Shape(name,parent,PopHomeGraphic.Kind.Circle,anchor,size,new(.74f,.9f,.94f,.26f));}
    private static TMP_Text Text(string name,Transform parent,string value,Vector2 anchor,Vector2 size,float fontSize,TMP_FontAsset font,Color color)
    {var r=Rect(name,parent,anchor,size);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=fontSize;t.enableAutoSizing=false;t.color=color;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Overflow;return t;}
    private static void Bind(PopUIView view,string key,TMP_Text text){view.elements.Add(new PopUIElement{key=key,rect=text.rectTransform,text=text,graphic=text});}
    private static void MakeButton(PopUIView view,string key,Transform parent,string label,Vector2 anchor,Vector2 size,Color color,PopHomeGraphic.Kind? icon,float fontSize,TMP_FontAsset font)
    {var g=Shape(key,parent,PopHomeGraphic.Kind.Rounded,anchor,size,color);g.Radius=key=="Journey"?60:key.StartsWith("Progress")?100:38;g.raycastTarget=true;var b=g.gameObject.AddComponent<Button>();b.targetGraphic=g;b.transition=Selectable.Transition.ColorTint;var colors=b.colors;colors.disabledColor=new(.7f,.7f,.7f,.65f);b.colors=colors;var n=b.navigation;n.mode=Navigation.Mode.None;b.navigation=n;TMP_Text t=Text("Label",g.transform,label,new(.5f,.5f),size-new Vector2(28,14),fontSize>0?fontSize:1,font,White);if(icon.HasValue)Shape("Icon",g.transform,icon.Value,new(.5f,.5f),size*.43f,White);view.elements.Add(new PopUIElement{key=key,rect=g.rectTransform,text=t,graphic=g,button=b});}
    private static void Set(SerializedObject o,string key,Object value){o.FindProperty(key).objectReferenceValue=value;}
    private static void SetArray<T>(SerializedObject o,string key,T[] values)where T:Object{var a=o.FindProperty(key);a.arraySize=values.Length;for(int i=0;i<values.Length;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=values[i];}
}
