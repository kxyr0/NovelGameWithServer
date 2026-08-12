using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Profile Collection Renderer")]
public sealed class PlayerCollectionRenderer : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private PlayerCollectionKind _kind;
    [SerializeField] private PlayerCollectionCatalog _catalog;

    [Header("Spawn")]
    [SerializeField] private Transform _content;
    [SerializeField] private GameObject _itemPrefab;
    [SerializeField, Min(0), Tooltip("Zero displays the full collection.")]
    private int _maxVisible;
    [SerializeField, Tooltip("Displays the catalog from bottom to top.")]
    private bool _reverseCatalogOrder;
    [SerializeField, Tooltip("Hides designer placeholder children at runtime.")]
    private bool _hideExistingChildren = true;

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private bool _contentPrepared;

    private void OnEnable()
    {
        PlayerCollectionState.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        PlayerCollectionState.Changed -= Refresh;
    }

    public void Refresh()
    {
        if (_catalog == null || _content == null || _itemPrefab == null)
            return;

        PrepareContent();
        ClearSpawned();

        IReadOnlyList<PlayerCollectionItemDefinition> items = _catalog.Items;
        int shown = 0;
        int start = _reverseCatalogOrder ? items.Count - 1 : 0;
        int end = _reverseCatalogOrder ? -1 : items.Count;
        int step = _reverseCatalogOrder ? -1 : 1;

        for (int i = start; i != end; i += step)
        {
            PlayerCollectionItemDefinition item = items[i];
            if (item == null || item.Kind != _kind ||
                !item.IsConfigured || !PlayerCollectionState.IsOwned(item))
                continue;

            Spawn(item);
            shown++;
            if (_maxVisible > 0 && shown >= _maxVisible)
                break;
        }

        if (_content is RectTransform rect)
            LayoutRebuilder.MarkLayoutForRebuild(rect);
    }

    private void PrepareContent()
    {
        if (_contentPrepared) return;
        _contentPrepared = true;
        if (!_hideExistingChildren) return;

        for (int i = 0; i < _content.childCount; i++)
        {
            GameObject child = _content.GetChild(i).gameObject;
            if (child != gameObject)
                child.SetActive(false);
        }
    }

    private void Spawn(PlayerCollectionItemDefinition item)
    {
        GameObject instance = Instantiate(_itemPrefab, _content, false);
        instance.SetActive(true);
        PlayerCollectionItemView view =
            instance.GetComponent<PlayerCollectionItemView>();
        if (view == null)
            view = instance.AddComponent<PlayerCollectionItemView>();
        view.Bind(item);

        PlayerCollectionItemNavigation navigation =
            instance.GetComponent<PlayerCollectionItemNavigation>();
        if (navigation == null)
            navigation = instance.AddComponent<PlayerCollectionItemNavigation>();
        navigation.Bind(item);

        _spawned.Add(instance);
    }

    private void ClearSpawned()
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] != null)
                Destroy(_spawned[i]);
        }
        _spawned.Clear();
    }
}
