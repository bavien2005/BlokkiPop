using UnityEngine;

public static class MobilePerformance
{

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]

    private static void Configure()
    {
#if UNITY_ANDROID || UNITY_IOS
        Application.targetFrameRate = 120;
#endif
    }
}
