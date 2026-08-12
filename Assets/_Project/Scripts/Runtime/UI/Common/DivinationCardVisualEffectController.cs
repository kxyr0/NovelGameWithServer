using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Divination Card Visual Effect Controller")]
public sealed class DivinationCardVisualEffectController : MonoBehaviour
{
    private const string LogPrefix = "[Divination]";

    [Header("Цели эффекта")]
    [SerializeField]
    [Tooltip("UI Graphic/Image, который получает цвет вспышки или замену материала.")]
    private Graphic _targetGraphic;

    [SerializeField]
    [Tooltip("Необязательный SpriteRenderer для карты в мировом пространстве/3D.")]
    private SpriteRenderer _targetSpriteRenderer;

    [SerializeField]
    [Tooltip("Необязательный существующий SpriteFrameAnimator. Если назначен, режим Flash может запускать старую анимацию флешки/карты.")]
    private SpriteFrameAnimator _spriteFrameAnimator;

    [Header("Вспышка")]
    [SerializeField]
    [Tooltip("Если включено и SpriteFrameAnimator назначен, режим Flash вызывает TriggerAnimation.")]
    private bool _triggerSpriteFrameAnimatorForFlash = true;

    [SerializeField]
    [Tooltip("Цвет простой запасной вспышки, когда SpriteFrameAnimator не используется.")]
    private Color _flashColor = Color.white;

    [SerializeField, Min(0f)]
    [Tooltip("Сколько секунд запасная вспышка держится на полной яркости.")]
    private float _flashHoldSeconds = 0.04f;

    [SerializeField, Min(0f)]
    [Tooltip("Сколько секунд запасная вспышка затухает.")]
    private float _flashFadeSeconds = 0.16f;

    [Header("Шейдер / материал")]
    [SerializeField]
    [Tooltip("Материал по умолчанию для режима Shader. Материал из настройки карты имеет приоритет.")]
    private Material _shaderMaterial;

    [SerializeField]
    [Tooltip("Шейдер по умолчанию для создания runtime-материала, если Shader Material пустой.")]
    private Shader _shader;

    [SerializeField]
    [Tooltip("Материал по умолчанию для режима MaterialSwap. Материал из настройки карты имеет приоритет.")]
    private Material _materialSwap;

    [SerializeField, Min(0f)]
    [Tooltip("Сколько секунд MaterialSwap остается активным до восстановления исходного материала. 0 означает не восстанавливать автоматически.")]
    private float _materialSwapSeconds = 0.2f;

    [SerializeField]
    [Tooltip("Восстанавливать исходный материал после длительности MaterialSwap.")]
    private bool _restoreMaterialAfterSwap = true;

    [Header("Пользовательский эффект")]
    [SerializeField]
    [Tooltip("Вызывается, когда выбран режим Custom.")]
    private UnityEvent _customEffect = new UnityEvent();

    private Coroutine _effectRoutine;
    private Color _originalGraphicColor;
    private Color _originalSpriteColor;
    private Material _originalGraphicMaterial;
    private Material _originalSpriteMaterial;
    private Material _ownedShaderMaterial;
    private bool _capturedOriginals;

    private void Awake()
    {
        ResolveTargets();
        CaptureOriginals();
    }

    private void OnValidate()
    {
        _flashHoldSeconds = Mathf.Max(0f, _flashHoldSeconds);
        _flashFadeSeconds = Mathf.Max(0f, _flashFadeSeconds);
        _materialSwapSeconds = Mathf.Max(0f, _materialSwapSeconds);
    }

    private void OnDestroy()
    {
        StopEffectRoutine();
        if (_ownedShaderMaterial != null)
            Destroy(_ownedShaderMaterial);
    }

    public void Play(DivinationCardVisualEffectMode mode)
    {
        Play(mode, null, null);
    }

    public void Play(DivinationCardVisualEffectMode mode, Material materialOverride, Shader shaderOverride)
    {
        ResolveTargets();
        CaptureOriginals();

        switch (mode)
        {
            case DivinationCardVisualEffectMode.None:
                return;
            case DivinationCardVisualEffectMode.Flash:
                PlayFlash();
                return;
            case DivinationCardVisualEffectMode.Shader:
                ApplyShader(materialOverride, shaderOverride);
                return;
            case DivinationCardVisualEffectMode.MaterialSwap:
                ApplyMaterialSwap(materialOverride);
                return;
            case DivinationCardVisualEffectMode.Custom:
                _customEffect.Invoke();
                Debug.Log(LogPrefix + " visual effect applied: Custom.", this);
                return;
            default:
                Debug.LogWarning(LogPrefix + " unknown visual effect mode: " + mode + ".", this);
                return;
        }
    }

    public void RestoreOriginalMaterial()
    {
        CaptureOriginals();

        if (_targetGraphic != null)
            _targetGraphic.material = _originalGraphicMaterial;

        if (_targetSpriteRenderer != null)
            _targetSpriteRenderer.material = _originalSpriteMaterial;
    }

    private void PlayFlash()
    {
        if (_spriteFrameAnimator != null && _triggerSpriteFrameAnimatorForFlash)
        {
            _spriteFrameAnimator.TriggerAnimation();
            Debug.Log(LogPrefix + " visual effect applied: Flash via SpriteFrameAnimator.", this);
            return;
        }

        if (_targetGraphic == null && _targetSpriteRenderer == null)
        {
            Debug.LogWarning(LogPrefix + " visual effect skipped: Flash target is missing.", this);
            return;
        }

        StopEffectRoutine();
        if (Application.isPlaying)
            _effectRoutine = StartCoroutine(FlashRoutine());
        else
            ApplyFlashColor(1f);

        Debug.Log(LogPrefix + " visual effect applied: Flash.", this);
    }

    private void ApplyShader(Material materialOverride, Shader shaderOverride)
    {
        Material material = materialOverride != null ? materialOverride : _shaderMaterial;
        if (material == null)
        {
            Shader shader = shaderOverride != null ? shaderOverride : _shader;
            if (shader != null)
            {
                if (_ownedShaderMaterial != null)
                    Destroy(_ownedShaderMaterial);

                _ownedShaderMaterial = new Material(shader);
                material = _ownedShaderMaterial;
            }
        }

        if (material == null)
        {
            Debug.LogWarning(LogPrefix + " visual effect skipped: Shader mode has no material or shader.", this);
            return;
        }

        ApplyMaterial(material);
        Debug.Log(LogPrefix + " visual effect applied: Shader.", this);
    }

    private void ApplyMaterialSwap(Material materialOverride)
    {
        Material material = materialOverride != null ? materialOverride : _materialSwap;
        if (material == null)
        {
            Debug.LogWarning(LogPrefix + " visual effect skipped: MaterialSwap mode has no material.", this);
            return;
        }

        ApplyMaterial(material);

        if (_restoreMaterialAfterSwap && _materialSwapSeconds > 0f && Application.isPlaying)
        {
            StopEffectRoutine();
            _effectRoutine = StartCoroutine(MaterialRestoreRoutine(_materialSwapSeconds));
        }

        Debug.Log(LogPrefix + " visual effect applied: MaterialSwap.", this);
    }

    private IEnumerator FlashRoutine()
    {
        ApplyFlashColor(1f);

        if (_flashHoldSeconds > 0f)
            yield return new WaitForSecondsRealtime(_flashHoldSeconds);

        float elapsed = 0f;
        while (_flashFadeSeconds > 0f && elapsed < _flashFadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            ApplyFlashColor(1f - Mathf.Clamp01(elapsed / _flashFadeSeconds));
            yield return null;
        }

        RestoreOriginalColors();
        _effectRoutine = null;
    }

    private IEnumerator MaterialRestoreRoutine(float delaySeconds)
    {
        yield return new WaitForSecondsRealtime(delaySeconds);
        RestoreOriginalMaterial();
        _effectRoutine = null;
    }

    private void ApplyFlashColor(float strength)
    {
        Color graphicColor = Color.Lerp(_originalGraphicColor, _flashColor, Mathf.Clamp01(strength));
        Color spriteColor = Color.Lerp(_originalSpriteColor, _flashColor, Mathf.Clamp01(strength));

        if (_targetGraphic != null)
            _targetGraphic.color = graphicColor;

        if (_targetSpriteRenderer != null)
            _targetSpriteRenderer.color = spriteColor;
    }

    private void RestoreOriginalColors()
    {
        if (_targetGraphic != null)
            _targetGraphic.color = _originalGraphicColor;

        if (_targetSpriteRenderer != null)
            _targetSpriteRenderer.color = _originalSpriteColor;
    }

    private void ApplyMaterial(Material material)
    {
        if (_targetGraphic != null)
            _targetGraphic.material = material;

        if (_targetSpriteRenderer != null)
            _targetSpriteRenderer.material = material;
    }

    private void StopEffectRoutine()
    {
        if (_effectRoutine == null)
            return;

        StopCoroutine(_effectRoutine);
        _effectRoutine = null;
    }

    private void ResolveTargets()
    {
        if (_targetGraphic == null)
            _targetGraphic = GetComponent<Graphic>();

        if (_targetSpriteRenderer == null)
            _targetSpriteRenderer = GetComponent<SpriteRenderer>();

        if (_spriteFrameAnimator == null)
            _spriteFrameAnimator = GetComponent<SpriteFrameAnimator>();
    }

    private void CaptureOriginals()
    {
        if (_capturedOriginals)
            return;

        if (_targetGraphic != null)
        {
            _originalGraphicColor = _targetGraphic.color;
            _originalGraphicMaterial = _targetGraphic.material;
        }
        else
        {
            _originalGraphicColor = Color.white;
        }

        if (_targetSpriteRenderer != null)
        {
            _originalSpriteColor = _targetSpriteRenderer.color;
            _originalSpriteMaterial = _targetSpriteRenderer.material;
        }
        else
        {
            _originalSpriteColor = Color.white;
        }

        _capturedOriginals = true;
    }
}
