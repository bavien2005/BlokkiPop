using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Persists a simple full-screen fade across the Home-to-Game scene activation.</summary>
public sealed class BlokkiPopSceneTransition : MonoBehaviour
{
    [Tooltip("CanvasGroup on this transition canvas. Alpha is animated across both scenes.")]
    [SerializeField] private CanvasGroup coverGroup;

    [Tooltip("Fade duration before the gameplay scene is activated.")]
    [SerializeField, Min(0f)] private float fadeToGameSeconds = 0.22f;

    [Tooltip("Fade duration revealing the gameplay scene after it has loaded.")]
    [SerializeField, Min(0f)] private float fadeIntoGameSeconds = 0.28f;

    private bool transitionStarted;

    public void Play(AsyncOperation loadOperation)
    {
        if (transitionStarted || loadOperation == null)
            return;

        transitionStarted = true;
        DontDestroyOnLoad(gameObject);
        StartCoroutine(Transition(loadOperation));
    }

    private IEnumerator Transition(AsyncOperation loadOperation)
    {
        if (coverGroup != null)
        {
            coverGroup.alpha = 0f;
            coverGroup.blocksRaycasts = true;
            yield return Fade(0f, 1f, fadeToGameSeconds);
        }

        loadOperation.allowSceneActivation = true;
        while (!loadOperation.isDone)
            yield return null;

        // Keep the opaque cover over the first rendered gameplay frame, then reveal it smoothly.
        yield return null;
        if (coverGroup != null)
        {
            yield return Fade(1f, 0f, fadeIntoGameSeconds);
            coverGroup.blocksRaycasts = false;
        }

        Destroy(gameObject);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (coverGroup == null)
            yield break;

        if (duration <= 0f)
        {
            coverGroup.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            coverGroup.alpha = Mathf.Lerp(from, to, eased);
            yield return null;
        }

        coverGroup.alpha = to;
    }
}
