using UnityEngine;
using System.Collections;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance;
    private Vector3 originalPos;
    private Coroutine _shakeRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        originalPos = transform.localPosition;
    }

    private void OnDisable()
    {
        StopShake();
    }

    private void OnDestroy()
    {
        StopShake();

        if (Instance == this)
            Instance = null;
    }

    public void PlayEffect(EffectNode node)
    {
        if (node == null)
            return;

        if (node.effect == EffectType.Shake)
        {
            StopShake();

            _shakeRoutine = StartCoroutine(Shake(Mathf.Max(0f, node.duration), Mathf.Max(0f, node.intensity)));
        }

        if (node.effect == EffectType.Vibration)
        {
            if (!SettingsScreenController.VibrationEnabled)
                return;

            try
            {
                Handheld.Vibrate();
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"EffectManager: vibration failed: {exception.Message}", this);
            }
        }
    }

    private IEnumerator Shake(float duration, float intensity)
    {
        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            transform.localPosition = originalPos + Random.insideUnitSphere * intensity;
            yield return null;
        }
        transform.localPosition = originalPos;
        _shakeRoutine = null;
    }

    private void StopShake()
    {
        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            _shakeRoutine = null;
        }

        transform.localPosition = originalPos;
    }
}
