using UnityEngine;

[AddComponentMenu("Nocturne/Ads/Banner Ad Screen Binder")]
public sealed class BannerAdScreenBinder : MonoBehaviour
{
    [SerializeField] private AdsServiceBehaviour _adsService;
    [SerializeField] private string _placementId = "banner_menu";
    [SerializeField] private bool _showOnEnable = true;
    [SerializeField] private bool _hideOnDisable = true;
    [SerializeField] private bool _destroyOnDestroy;

    private void OnEnable()
    {
        if (_showOnEnable)
            ShowBanner();
    }

    private void OnDisable()
    {
        if (_hideOnDisable)
            ResolveService()?.HideBanner();
    }

    private void OnDestroy()
    {
        if (_destroyOnDestroy)
            ResolveService()?.DestroyBanner();
    }

    public void ShowBanner()
    {
        ResolveService()?.ShowBanner(_placementId);
    }

    public void HideBanner()
    {
        ResolveService()?.HideBanner();
    }

    public void DestroyBanner()
    {
        ResolveService()?.DestroyBanner();
    }

    private IAdsService ResolveService()
    {
        if (_adsService != null)
            return _adsService;

        return AdsServiceBehaviour.TryGetGlobal(out IAdsService service) ? service : null;
    }
}
