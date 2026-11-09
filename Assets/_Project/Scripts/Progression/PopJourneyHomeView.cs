using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Journey Focus's authored scene references and state colors. Never owns layout.</summary>
[ExecuteAlways]
public sealed class PopJourneyHomeView : MonoBehaviour
{
    [Serializable] public sealed class Node
    {
        public Button button;
        public Graphic star, play, locked, ring;
        public int levelIndex;
    }
    [Header("Scene references")]
    public CanvasGroup actions;
    public GameObject settings;
    public Node[] nodes;
    public TMP_Text stars, journeySubtitle, soundStatus, hapticsStatus;
    public CanvasGroup brand;
    public RectTransform logo;
    public TMP_Text logoText, tagline;
    public RectTransform[] cells;
    public CanvasGroup[] cellGroups;
    [Header("Inspector preview (edit mode only)")]
    [SerializeField] private bool previewSettings;
    [Header("Home labels")]
    public string heading = "HÀNH TRÌNH CỦA BẠN";
    public string journeyLabel = "CHƠI POP JOURNEY";
    public string journeyHint = "Chinh phục màn tiếp theo";
    public string continueClassic = "Tiếp tục ván Classic";
    public string classicLabel = "CLASSIC", dailyLabel = "DAILY", rewardsLabel = "GIAO DIỆN";
    public string practiceLabel = "BÀI LUYỆN";
    public string learnLabel = "Hướng dẫn chơi", privacyLabel = "Quyền riêng tư";
    [Header("Progress colors")]
    public Color completeColor = new(.05f,.24f,.32f), currentColor = new(1f,.85f,.36f), lockedColor = new(.05f,.24f,.32f);
    public Color ink = new(.04f,.2f,.29f), white = new(.97f,.99f,1f);
    public bool IsSettingsOpen => settings != null && settings.activeInHierarchy;
    public int SuggestedLevel { get; private set; }
    private void OnEnable() { if (!Application.isPlaying) Preview(); }
    private void Update() { if (!Application.isPlaying && settings != null && settings.activeSelf != previewSettings) Preview(); }
    private void Preview() { if(settings!=null)settings.SetActive(previewSettings); SetNavigationReady(true); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RestoreSound() { AudioListener.volume=PlayerPrefs.GetInt("blokkipop.home.sound.v1",1)==1?1:0; }
    public void SetNavigationReady(bool value)
    { if(actions==null)return;actions.alpha=value?1:0;actions.interactable=value;actions.blocksRaycasts=value; }
    public void OpenSettings(bool value) { if(settings!=null)settings.SetActive(value); }
    public void Refresh(PopGameplaySession session)
    {
        int count=session.Configuration.journeyLevels.Count; SuggestedLevel=0;
        while(SuggestedLevel<count-1&&session.Stars(session.Configuration.journeyLevels[SuggestedLevel].id)>0)++SuggestedLevel;
        PopRunSnapshot saved=session.SavedRun(PopGameMode.Journey);
        int savedIndex=saved==null?-1:session.Configuration.journeyLevels.FindIndex(level=>level.id==saved.levelId);
        if(savedIndex>=0&&session.LevelUnlocked(savedIndex))SuggestedLevel=savedIndex;
        if(stars!=null)stars.text=session.TotalJourneyStars.ToString();
        if(journeySubtitle!=null)journeySubtitle.text=journeyHint;
        if(session.Configuration.endless.replaceJourneyHomeButton)
        {
            PopRunSnapshot endless=session.SavedRun(PopGameMode.EndlessPop);
            int current=endless!=null?endless.endlessWave-1:session.Progress.endlessBestWave;
            int first=Mathf.Max(0,current-2);
            if(nodes!=null)for(int i=0;i<nodes.Length;i++)
            {
                Node node=nodes[i];int index=first+i;bool active=index==current,done=index<current;
                node.levelIndex=index;node.button.gameObject.SetActive(true);node.button.interactable=done||active;
                if(node.button.targetGraphic!=null)node.button.targetGraphic.color=active?currentColor:done?completeColor:lockedColor;
                node.star.gameObject.SetActive(done);node.play.gameObject.SetActive(active);node.locked.gameObject.SetActive(!done&&!active);node.ring.gameObject.SetActive(active);
                node.play.color=ink;node.star.color=white;node.locked.color=white;
            }
            RefreshSettings();return;
        }
        int start=Mathf.Clamp(SuggestedLevel-2,0,Mathf.Max(0,count-(nodes?.Length??0)));
        if(nodes!=null)for(int i=0;i<nodes.Length;i++)
        {
            Node node=nodes[i];int index=start+i;bool exists=index<count;node.button.gameObject.SetActive(exists);if(!exists)continue;
            bool completed=session.Stars(session.Configuration.journeyLevels[index].id)>0,current=index==SuggestedLevel;
            node.levelIndex=index; node.button.interactable=session.LevelUnlocked(index);
            if(node.button.targetGraphic!=null)node.button.targetGraphic.color=current?currentColor:completed?completeColor:lockedColor;
            node.star.gameObject.SetActive(completed&&!current);node.play.gameObject.SetActive(current);node.locked.gameObject.SetActive(!completed&&!current);node.ring.gameObject.SetActive(current);
            node.play.color=ink;node.star.color=white;node.locked.color=white;
        }
        RefreshSettings();
    }
    public void RefreshSettings()
    {
        if(soundStatus!=null)soundStatus.text=PlayerPrefs.GetInt("blokkipop.home.sound.v1",1)==1?"BẬT":"TẮT";
        if(hapticsStatus!=null)hapticsStatus.text=GameHaptics.IsEnabled?"BẬT":"TẮT";
    }
    public void ToggleSound()
    {
        bool on=PlayerPrefs.GetInt("blokkipop.home.sound.v1",1)==0;PlayerPrefs.SetInt("blokkipop.home.sound.v1",on?1:0);PlayerPrefs.Save();
        AudioListener.volume=on?1:0;RefreshSettings();
    }
    public void ToggleHaptics()
    { PlayerPrefs.SetInt("blokkipop.haptics.enabled",GameHaptics.IsEnabled?0:1);PlayerPrefs.Save();RefreshSettings(); }
}
