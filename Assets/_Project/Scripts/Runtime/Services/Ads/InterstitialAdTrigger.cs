using UnityEngine;
using UnityEngine.Events;

[AddComponentMenu("Nocturne/Ads/Interstitial Ad Trigger")]
public sealed class InterstitialAdTrigger : MonoBehaviour
{
    [SerializeField] private AdsServiceBehaviour _adsService;
    [SerializeField] private string _placementId = "interstitial_transition";
    [SerializeField] private string _reason = "natural_transition";
    [SerializeField] private bool _showOnEnable;
    [SerializeField, Min(0f)] private float _showOnEnableDelaySeconds;
    [SerializeField] private UnityEvent _shown = new UnityEvent();
    [SerializeField] private UnityEvent _skipped = new UnityEvent();

    private Coroutine _showRoutine;

    private void OnEnable()
    {
        if (_showOnEnable)
            _showRoutine = StartCoroutine(ShowAfterDelay());
    }

    private void OnDisable()
    {
        if (_showRoutine != null)
        {
            StopCoroutine(_showRoutine);
            _showRoutine = null;
        }
    }

    public bool TryShow()
    {
        return TryShow(_reason);
    }

    public bool TryShow(string reason)
    {
        IAdsService service = ResolveService();
        if (service == null)
        {
            _skipped.Invoke();
            return false;
        }

        bool shown = service.TryShowInterstitial(_placementId, reason);
        if (shown)
            _shown.Invoke();
        else
            _skipped.Invoke();

        return shown;
    }

    private System.Collections.IEnumerator ShowAfterDelay()
    {
        if (_showOnEnableDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(_showOnEnableDelaySeconds);

        TryShow();
        _showRoutine = null;
    }

    private IAdsService ResolveService()
    {
        if (_adsService != null)
            return _adsService;

        return AdsServiceBehaviour.TryGetGlobal(out IAdsService service) ? service : null;
    }
}
