using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        instance = null;
    }
    public static CameraShake instance;
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
    public void ShackCamera(float duration , float magnitude)
    {
        StartCoroutine(Shake(duration , magnitude));
    }

    private IEnumerator Shake(float duration , float magnitude)
    {
        Vector3 originalPos = transform.localPosition;
        float elapsed = 0.0f; 

        while(elapsed < duration)
        {
            float x = Random.Range(-1f , 1f)  *magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            transform.localPosition = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localPosition = originalPos; 
    }
}
