using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed partial class EditProfileController
{
    private const string AvatarIndexKey = "Profile.SelectedAvatarIndex";

    [Header("Avatar Selection")]
    [Tooltip("Grid с корнями Avatar. Порядок дочерних объектов совпадает с порядком спрайтов.")]
    [SerializeField] private Transform _avatarOptionsRoot;
    [Tooltip("Спрайты вариантов в том же порядке, что Avatar внутри Grid.")]
    [SerializeField] private Sprite[] _avatarSprites = Array.Empty<Sprite>();
    [Tooltip("Дополнительные Image пользователя вне экранов Profile/ProfileEdit.")]
    [SerializeField] private Image[] _additionalAvatarTargets = Array.Empty<Image>();
    [SerializeField] private bool _autoFindProfileAvatarTargets = true;
    [SerializeField, Min(0f)] private float _avatarFadeDuration = 0.24f;
    [SerializeField] private Ease _avatarFadeEase = Ease.InOutSine;

    private readonly List<Button> _avatarButtons = new List<Button>();
    private readonly List<UnityAction> _avatarListeners = new List<UnityAction>();
    private readonly List<Image> _avatarTargets = new List<Image>();
    private readonly List<float> _avatarTargetAlphas = new List<float>();
    private readonly List<Tween> _avatarTweens = new List<Tween>();
    private int _selectedAvatarIndex;

    private void InitializeAvatarSelection()
    {
        BindAvatarOptions();
        CollectAvatarTargets();
        int max = Mathf.Min(OptionCount(), _avatarSprites.Length) - 1;
        _selectedAvatarIndex = max >= 0
            ? Mathf.Clamp(PlayerPrefs.GetInt(AvatarIndexKey, 0), 0, max)
            : 0;
        ApplySelectedAvatar(false);
    }

    private void ShutdownAvatarSelection()
    {
        for (int i = 0; i < _avatarButtons.Count; i++)
            if (_avatarButtons[i] != null)
                _avatarButtons[i].onClick.RemoveListener(_avatarListeners[i]);
        _avatarButtons.Clear();
        _avatarListeners.Clear();
        StopAvatarTweens();
    }

    public void SelectAvatar(int index)
    {
        int max = Mathf.Min(OptionCount(), _avatarSprites.Length) - 1;
        if (index < 0 || index > max || _avatarSprites[index] == null)
            return;
        _selectedAvatarIndex = index;
        PlayerPrefs.SetInt(AvatarIndexKey, index);
        PlayerPrefs.Save();
        CollectAvatarTargets();
        ApplySelectedAvatar(true);
    }

    public void RefreshAvatar()
    {
        CollectAvatarTargets();
        ApplySelectedAvatar(false);
    }

    private void BindAvatarOptions()
    {
        int count = Mathf.Min(OptionCount(), _avatarSprites.Length);
        for (int i = 0; i < count; i++)
        {
            Transform root = _avatarOptionsRoot.GetChild(i);
            Image preview = FindAvatarImage(root);
            if (preview != null && _avatarSprites[i] != null)
                preview.sprite = _avatarSprites[i];
            Button button = root.GetComponent<Button>();
            if (button == null)
                button = root.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            if (button.targetGraphic == null)
                button.targetGraphic = preview;
            int optionIndex = i;
            UnityAction listener = () => SelectAvatar(optionIndex);
            button.onClick.AddListener(listener);
            _avatarButtons.Add(button);
            _avatarListeners.Add(listener);
        }
    }

    private void CollectAvatarTargets()
    {
        StopAvatarTweens();
        _avatarTargets.Clear();
        AddTargets(_additionalAvatarTargets);
        if (_autoFindProfileAvatarTargets)
        {
            Image[] images = FindObjectsOfType<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null || image.name != "Avatar" || IsOptionPreview(image.transform))
                    continue;
                UIScreenMarker marker = image.GetComponentInParent<UIScreenMarker>(true);
                if (marker != null && (marker.ScreenId == "Profile" || marker.ScreenId == "ProfileEdit"))
                    AddTarget(image);
            }
        }
        _avatarTargetAlphas.Clear();
        _avatarTweens.Clear();
        for (int i = 0; i < _avatarTargets.Count; i++)
        {
            _avatarTargetAlphas.Add(_avatarTargets[i].color.a);
            _avatarTweens.Add(null);
        }
    }

    private void ApplySelectedAvatar(bool animated)
    {
        if (_selectedAvatarIndex < 0 || _selectedAvatarIndex >= _avatarSprites.Length)
            return;
        Sprite sprite = _avatarSprites[_selectedAvatarIndex];
        if (sprite == null)
            return;
        for (int i = 0; i < _avatarTargets.Count; i++)
            ApplyAvatar(_avatarTargets[i], sprite, i, animated);
    }

    private void ApplyAvatar(Image image, Sprite sprite, int index, bool animated)
    {
        if (image == null)
            return;
        float alpha = _avatarTargetAlphas[index];
        if (!animated || _avatarFadeDuration <= 0f || !image.gameObject.activeInHierarchy)
        {
            image.sprite = sprite;
            SetImageAlpha(image, alpha);
            return;
        }
        float half = _avatarFadeDuration * 0.5f;
        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Append(image.DOFade(0f, half).SetEase(_avatarFadeEase));
        sequence.AppendCallback(() => { if (image != null) image.sprite = sprite; });
        sequence.Append(image.DOFade(alpha, half).SetEase(_avatarFadeEase));
        _avatarTweens[index] = sequence;
    }

    private void StopAvatarTweens()
    {
        for (int i = 0; i < _avatarTweens.Count; i++)
            _avatarTweens[i]?.Kill();
        for (int i = 0; i < _avatarTargets.Count && i < _avatarTargetAlphas.Count; i++)
            SetImageAlpha(_avatarTargets[i], _avatarTargetAlphas[i]);
    }

    private int OptionCount() => _avatarOptionsRoot != null ? _avatarOptionsRoot.childCount : 0;

    private bool IsOptionPreview(Transform target)
    {
        return _avatarOptionsRoot != null && target.IsChildOf(_avatarOptionsRoot);
    }

    private void AddTargets(Image[] targets)
    {
        if (targets == null)
            return;
        for (int i = 0; i < targets.Length; i++)
            AddTarget(targets[i]);
    }

    private void AddTarget(Image target)
    {
        if (target != null && !_avatarTargets.Contains(target))
            _avatarTargets.Add(target);
    }

    private static Image FindAvatarImage(Transform root)
    {
        Image[] images = root != null ? root.GetComponentsInChildren<Image>(true) : Array.Empty<Image>();
        for (int i = 0; i < images.Length; i++)
            if (images[i].name == "Avatar")
                return images[i];
        return images.Length > 0 ? images[0] : null;
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
            return;
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }
}
