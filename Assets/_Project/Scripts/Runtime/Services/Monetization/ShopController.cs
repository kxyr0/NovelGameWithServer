using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.UI;

[ExecuteAlways]
public class ShopController : MonoBehaviour
{
	const int MaxRemoteShopItems = 100;
	const int MaxShopTextChars = 96;
	const int MaxOrderQuantity = 99;

	public static ShopController Instance;

	[Header("References")]
	public GameObject panel;
	public Transform itemContainer;
	public Button closeButton;
	public Button restoreButton;
	public TMP_Text titleText;

	[Header("Balances")]
	public TMP_Text heartsBalanceText;
	public TMP_Text candlesBalanceText;
	[Tooltip("При открытии магазина сначала синхронизировать баланс с сервером, чтобы верхние счётчики не показывали старые локальные тестовые значения.")]
	[SerializeField] private bool syncBalanceOnOpen = false;
	[Tooltip("Пока идёт SyncBalance при открытии, показать плейсхолдер вместо старого локального баланса.")]
	[SerializeField] private bool showBalanceLoadingWhileSyncing = true;
	[Tooltip("Текст плейсхолдера баланса во время синхронизации.")]
	[SerializeField] private string balanceLoadingText = "...";
	[Tooltip("Автоматически поменять местами Hearts Balance Text и Candles Balance Text, если по имени или пути объекта видно, что они назначены наоборот.")]
	[SerializeField] private bool autoCorrectSwappedBalanceTexts = true;

	[Header("Items")]
	public List<ShopItemData> shopItems = new List<ShopItemData>();
	public ShopItemView shopItemPrefab;

	[Header("Fake Server Testing")]
	[InspectorName("Фейковый сервер покупок")]
	[Tooltip("Editor/Development only. Если включено, покупка идет через тестовую серверную цепочку: клик -> fake POST /shop/orders -> валидация -> локальная выдача валюты.")]
	[SerializeField] private bool useFakeServerPurchases = false;
	[InspectorName("Авто fake в Editor")]
	[Tooltip("Если включено, в Unity Editor покупки идут через fake server даже без настоящего IAP/аккаунта. Это нужно для быстрой проверки кнопок магазина.")]
	[SerializeField] private bool autoUseFakeServerPurchasesInEditor = true;
	[InspectorName("Фейковый сервер требует вход")]
	[Tooltip("Если включено, fake server будет вести себя как боевой путь и требовать авторизацию. Если выключено, можно тестировать покупки без аккаунта.")]
	[SerializeField] private bool fakeServerRequiresAuthentication = false;
	[InspectorName("Начислять тестовую валюту")]
	[Tooltip("Если включено, успешный fake server response добавит ресурс из карточки товара в локальный PlayerData.")]
	[SerializeField] private bool fakeServerGrantsCurrency = true;
	[InspectorName("Синхронизировать после fake")]
	[Tooltip("Обычно выключено: реальная синхронизация может перезаписать локальную тестовую выдачу балансом сервера. Включай только если нужно проверить настоящий SyncBalance после fake покупки.")]
	[SerializeField] private bool fakeServerSyncsRealBalance = false;
	[InspectorName("Задержка fake ответа")]
	[Tooltip("Пауза перед ответом фейкового сервера, чтобы проверить состояние 'заказ выполняется' и двойные клики.")]
	[SerializeField] private float fakeServerResponseDelaySeconds = 0.35f;
	[InspectorName("Чинить фон при открытии")]
	[Tooltip("Если включено, при открытии магазина скрипт заново включает Image-объекты фона с именами Background/Back/ShopBackground. Это защищает сцену от случайного выключения фона карточками товара.")]
	[SerializeField] private bool keepShopBackgroundImagesEnabled = true;

	public Action onClose;

	Tween _panelFadeTween;
	bool _loadingRemoteShop;
	bool _orderInFlight;
	bool _missingShopViewLogged;
	bool _hasLoadedRemoteShopItems;
	NativeIapManager _nativeIap;

	struct BoundProductViewState
	{
		public readonly ShopProductButtonView View;
		public readonly int SortOrder;
		public readonly int OriginalIndex;

		public BoundProductViewState(ShopProductButtonView view, int sortOrder, int originalIndex)
		{
			View = view;
			SortOrder = sortOrder;
			OriginalIndex = originalIndex;
		}
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	static void ResetStaticState()
	{
		Instance = null;
	}

	void Awake()
	{
		ResolveSceneReferences();
		RegisterInstance();
		BindRuntimeEvents();
	}

	void Start()
	{
		if (!Application.isPlaying)
		{
			ResolveSceneReferences();
			RegisterInstance();
			if (Instance == this)
				RefreshBalance();
			return;
		}

		if (Instance != this)
			return;

		ResolveSceneReferences();

		if (closeButton != null)
			closeButton.onClick.AddListener(Close);
		if (restoreButton != null)
			restoreButton.onClick.AddListener(RestoreNativePurchases);

		if (panel != null)
			panel.SetActive(false);

		_nativeIap = NativeIapManager.GetOrCreate();
		if (_nativeIap != null)
		{
			_nativeIap.ProductsUpdated += HandleNativeIapProductsUpdated;
			_nativeIap.ConfigureProducts(shopItems);
		}

		BuildShop();
		RefreshBalance();

		AppLogger.Info(
			AppLogCategory.Shop,
			nameof(ShopController),
			nameof(Start),
			"[SHOP][INIT] Shop controller initialized.",
			BuildShopMetadata("start"));
	}

	void OnEnable()
	{
		ResolveSceneReferences();
		RegisterInstance();
		BindRuntimeEvents();
		if (Instance != this)
			return;

		HandleCurrentScreenChanged(UIScreenState.CurrentScreenId);
	}

	void OnDisable()
	{
		UnbindRuntimeEvents();
	}

	void OnDestroy()
	{
		UnbindRuntimeEvents();

		if (closeButton != null)
			closeButton.onClick.RemoveListener(Close);
		if (restoreButton != null)
			restoreButton.onClick.RemoveListener(RestoreNativePurchases);

		if (_nativeIap != null)
			_nativeIap.ProductsUpdated -= HandleNativeIapProductsUpdated;

		_panelFadeTween?.Kill();

		if (Instance == this)
			Instance = null;
	}

	void BuildShop()
	{
		ResolveSceneReferences();

		List<ShopProductButtonView> boundProductViews = FindBoundProductViews();
		if (boundProductViews.Count > 0)
		{
			BuildBoundShop(boundProductViews);
			return;
		}

		if (itemContainer == null || shopItemPrefab == null)
		{
			if (!_missingShopViewLogged)
			{
				_missingShopViewLogged = true;
				AppLogger.Warn(
					AppLogCategory.Shop,
					nameof(ShopController),
					nameof(BuildShop),
					"[SHOP][BUILD_SKIPPED] Shop item container or prefab is not assigned.",
					BuildShopMetadata("build_missing_references"),
					recoverable: true);
			}

			return;
		}

		ApplyNativePricesToShopItems();

		foreach (Transform t in itemContainer)
			Destroy(t.gameObject);

		if (shopItems == null)
			return;

		foreach (var item in shopItems)
		{
			if (item == null)
				continue;

			var view = Instantiate(shopItemPrefab, itemContainer);
			if (view != null)
				view.Setup(item, OnBuyItem);
		}

		AppLogger.Info(
			AppLogCategory.Shop,
			nameof(ShopController),
			nameof(BuildShop),
			"[SHOP][BUILD] Shop item views rebuilt.",
			BuildShopMetadata("build"));
	}

	private void BuildBoundShop(List<ShopProductButtonView> productViews)
	{
		if (productViews == null || productViews.Count == 0)
			return;

		ApplyNativePricesToShopItems();

		Dictionary<string, ShopItemData> serverItemsByButtonId = BuildShopItemMap(shopItems, useButtonId: true);
		Dictionary<string, ShopItemData> serverItemsByProductId = BuildShopItemMap(shopItems, useButtonId: false);
		var mergedItems = new List<ShopItemData>(productViews.Count);
		var sortedStates = new List<BoundProductViewState>(productViews.Count);
		var usedButtonIds = new HashSet<string>(StringComparer.Ordinal);
		var usedProductIds = new HashSet<string>(StringComparer.Ordinal);

		for (int i = 0; i < productViews.Count; i++)
		{
			ShopProductButtonView view = productViews[i];
			if (view == null)
				continue;

			ShopItemData localItem = view.BuildLocalData();
			string buttonId = SaveDataSanitizer.SanitizeIdentifier(localItem != null ? localItem.buttonId : "");
			string productId = SaveDataSanitizer.SanitizeIdentifier(localItem != null ? localItem.productId : "");
			bool hasServerItem = TryResolveServerShopItem(
				buttonId,
				productId,
				serverItemsByButtonId,
				serverItemsByProductId,
				out ShopItemData serverItem);

			ShopItemData item = MergeShopItemData(localItem, hasServerItem ? serverItem : null);
			ApplyNativePriceToShopItem(item);

			bool isAvailableFromServer = !_hasLoadedRemoteShopItems || hasServerItem;
			view.Setup(item, OnBuyItem, isAvailableFromServer);

			if (!string.IsNullOrEmpty(buttonId))
				usedButtonIds.Add(buttonId);
			if (!string.IsNullOrEmpty(productId))
				usedProductIds.Add(productId);

			mergedItems.Add(item);
			sortedStates.Add(new BoundProductViewState(
				view,
				view.ResolveSortOrder(item),
				i));
		}

		if (shopItems != null)
		{
			for (int i = 0; i < shopItems.Count; i++)
			{
				ShopItemData item = shopItems[i];
				string buttonId = SaveDataSanitizer.SanitizeIdentifier(item != null ? item.buttonId : "");
				string productId = SaveDataSanitizer.SanitizeIdentifier(item != null ? item.productId : "");
				bool usedByButton = !string.IsNullOrEmpty(buttonId) && usedButtonIds.Contains(buttonId);
				bool usedByProduct = !string.IsNullOrEmpty(productId) && usedProductIds.Contains(productId);
				if (!usedByButton && !usedByProduct)
					mergedItems.Add(item);
			}
		}

		shopItems = mergedItems;
		SortBoundProductViews(sortedStates);

		AppLogger.Info(
			AppLogCategory.Shop,
			nameof(ShopController),
			nameof(BuildBoundShop),
			"[SHOP][BUILD_STATIC] Static shop product buttons bound by buttonId/productId.",
			BuildShopMetadata("build_static_buttons"));
	}

	private List<ShopProductButtonView> FindBoundProductViews()
	{
		Transform root = panel != null ? panel.transform : transform;
		var result = new List<ShopProductButtonView>();
		if (root == null)
			return result;

		ShopProductButtonView[] views = root.GetComponentsInChildren<ShopProductButtonView>(true);
		for (int i = 0; i < views.Length; i++)
		{
			ShopProductButtonView view = views[i];
			if (view != null && !result.Contains(view))
				result.Add(view);
		}

		return result;
	}

	static Dictionary<string, ShopItemData> BuildShopItemMap(IEnumerable<ShopItemData> items, bool useButtonId)
	{
		var result = new Dictionary<string, ShopItemData>(StringComparer.Ordinal);
		if (items == null)
			return result;

		foreach (ShopItemData item in items)
		{
			string rawKey = "";
			if (item != null)
				rawKey = useButtonId ? item.buttonId : item.productId;

			string key = SaveDataSanitizer.SanitizeIdentifier(rawKey);
			if (string.IsNullOrEmpty(key))
				continue;

			result[key] = item;
		}

		return result;
	}

	static bool TryResolveServerShopItem(
		string buttonId,
		string productId,
		Dictionary<string, ShopItemData> serverItemsByButtonId,
		Dictionary<string, ShopItemData> serverItemsByProductId,
		out ShopItemData item)
	{
		item = null;

		if (!string.IsNullOrEmpty(buttonId)
			&& serverItemsByButtonId != null
			&& serverItemsByButtonId.TryGetValue(buttonId, out item))
			return true;

		if (!string.IsNullOrEmpty(productId)
			&& serverItemsByProductId != null
			&& serverItemsByProductId.TryGetValue(productId, out item))
			return true;

		return false;
	}

	static ShopItemData MergeShopItemData(ShopItemData fallback, ShopItemData overrideItem)
	{
		ShopItemData result = CloneShopItemData(fallback) ?? new ShopItemData();
		if (overrideItem == null)
			return result;

		string overrideProductId = SaveDataSanitizer.SanitizeIdentifier(overrideItem.productId);
		if (!string.IsNullOrEmpty(overrideProductId))
			result.productId = overrideProductId;
		string overrideButtonId = SaveDataSanitizer.SanitizeIdentifier(overrideItem.buttonId);
		if (!string.IsNullOrEmpty(overrideButtonId))
			result.buttonId = overrideButtonId;
		if (!string.IsNullOrWhiteSpace(overrideItem.label) && overrideItem.label != overrideProductId)
			result.label = overrideItem.label;
		if (overrideItem.icon != null)
			result.icon = overrideItem.icon;
		if (overrideItem.amount > 0)
		{
			result.amount = overrideItem.amount;
			if (string.IsNullOrWhiteSpace(overrideItem.amountDisplay))
				result.amountDisplay = "";
		}
		if (!string.IsNullOrWhiteSpace(overrideItem.amountDisplay))
			result.amountDisplay = overrideItem.amountDisplay;
		if (!string.IsNullOrWhiteSpace(overrideItem.currencyLabel))
			result.currencyLabel = overrideItem.currencyLabel;
		if (!string.IsNullOrWhiteSpace(overrideItem.priceLabel))
			result.priceLabel = overrideItem.priceLabel;
		if (overrideItem.quantity > 0)
			result.quantity = overrideItem.quantity;
		if (overrideItem.hasSortOrder)
		{
			result.sortOrder = overrideItem.sortOrder;
			result.hasSortOrder = true;
		}

		result.currency = overrideItem.currency;
		result.productType = overrideItem.productType;
		return result;
	}

	static List<ShopItemData> MergeShopItemLists(IEnumerable<ShopItemData> baseItems, IEnumerable<ShopItemData> overrideItems)
	{
		var result = new List<ShopItemData>();
		var indexByProductId = new Dictionary<string, int>(StringComparer.Ordinal);

		if (baseItems != null)
		{
			foreach (ShopItemData item in baseItems)
			{
				ShopItemData clone = CloneShopItemData(item);
				string key = ResolveShopItemMergeKey(clone);
				if (string.IsNullOrEmpty(key))
					continue;

				indexByProductId[key] = result.Count;
				result.Add(clone);
			}
		}

		if (overrideItems != null)
		{
			foreach (ShopItemData item in overrideItems)
			{
				string key = ResolveShopItemMergeKey(item);
				if (string.IsNullOrEmpty(key))
					continue;

				if (indexByProductId.TryGetValue(key, out int index))
					result[index] = MergeShopItemData(result[index], item);
				else
				{
					indexByProductId[key] = result.Count;
					result.Add(CloneShopItemData(item));
				}
			}
		}

		return result;
	}

	static string ResolveShopItemMergeKey(ShopItemData item)
	{
		string buttonId = SaveDataSanitizer.SanitizeIdentifier(item != null ? item.buttonId : "");
		if (!string.IsNullOrEmpty(buttonId))
			return "button:" + buttonId;

		string productId = SaveDataSanitizer.SanitizeIdentifier(item != null ? item.productId : "");
		return string.IsNullOrEmpty(productId) ? "" : "product:" + productId;
	}

	static ShopItemData CloneShopItemData(ShopItemData item)
	{
		if (item == null)
			return null;

		return new ShopItemData
		{
			buttonId = item.buttonId,
			label = item.label,
			icon = item.icon,
			amount = item.amount,
			amountDisplay = item.amountDisplay,
			currency = item.currency,
			currencyLabel = item.currencyLabel,
			priceLabel = item.priceLabel,
			productId = item.productId,
			productType = item.productType,
			quantity = item.quantity,
			sortOrder = item.sortOrder,
			hasSortOrder = item.hasSortOrder
		};
	}

	void SortBoundProductViews(List<BoundProductViewState> states)
	{
		if (states == null || states.Count <= 1)
			return;

		var statesByParent = new Dictionary<Transform, List<BoundProductViewState>>();
		for (int i = 0; i < states.Count; i++)
		{
			BoundProductViewState state = states[i];
			if (state.View == null)
				continue;

			Transform parent = state.View.transform.parent;
			if (parent == null)
				continue;

			if (!statesByParent.TryGetValue(parent, out List<BoundProductViewState> parentStates))
			{
				parentStates = new List<BoundProductViewState>();
				statesByParent[parent] = parentStates;
			}

			parentStates.Add(state);
		}

		foreach (KeyValuePair<Transform, List<BoundProductViewState>> pair in statesByParent)
		{
			List<BoundProductViewState> parentStates = pair.Value;
			if (parentStates.Count <= 1)
				continue;

			parentStates.Sort(CompareBoundProductViews);
			int firstSiblingIndex = int.MaxValue;
			for (int i = 0; i < parentStates.Count; i++)
				firstSiblingIndex = Mathf.Min(firstSiblingIndex, parentStates[i].View.transform.GetSiblingIndex());

			if (firstSiblingIndex == int.MaxValue)
				continue;

			for (int i = 0; i < parentStates.Count; i++)
				parentStates[i].View.transform.SetSiblingIndex(firstSiblingIndex + i);
		}
	}

	static int CompareBoundProductViews(BoundProductViewState left, BoundProductViewState right)
	{
		int sortCompare = left.SortOrder.CompareTo(right.SortOrder);
		return sortCompare != 0 ? sortCompare : left.OriginalIndex.CompareTo(right.OriginalIndex);
	}

	public void BuyFromProductButtonView(ShopItemData item, ShopProductButtonView sourceView, string clickSource)
	{
		IDictionary<string, object> metadata = BuildShopItemMetadata(item, "product_button_view_fallback");
		metadata["sourceView"] = sourceView != null ? sourceView.name : "";
		metadata["sourceViewPath"] = sourceView != null ? GetHierarchyPath(sourceView.transform) : "";
		metadata["clickSource"] = clickSource ?? "";
		AppLogger.Warn(
			AppLogCategory.Shop,
			nameof(ShopController),
			nameof(BuyFromProductButtonView),
			"[SHOP][BUY_VIEW_FALLBACK] Shop product view invoked purchase through controller fallback.",
			metadata,
			recoverable: true);

		OnBuyItem(item);
	}

	void OnBuyItem(ShopItemData item)
	{
		if (item == null)
			return;

		AppLogger.Info(
			AppLogCategory.Shop,
			nameof(ShopController),
			nameof(OnBuyItem),
			"[SHOP][BUY_CLICK] Shop item clicked.",
			BuildShopItemMetadata(item, "buy_click"));

		bool fakeServerPurchase = IsFakeServerPurchaseEnabled();
		bool isAuthenticated = NetworkManager.Instance != null && NetworkManager.IsAuthenticated;
		if (!isAuthenticated && (!fakeServerPurchase || fakeServerRequiresAuthentication))
		{
			AppLogger.Warn(
				AppLogCategory.Shop,
				nameof(ShopController),
				nameof(OnBuyItem),
				"[SHOP][BUY_DENIED] Player is not authenticated.",
				BuildShopItemMetadata(item, "not_authenticated"),
				recoverable: true);
			ShowShopMessage("\u0412\u043e\u0439\u0434\u0438\u0442\u0435 \u0432 \u0430\u043a\u043a\u0430\u0443\u043d\u0442 \u0434\u043b\u044f \u043f\u043e\u043a\u0443\u043f\u043a\u0438");
			return;
		}

		if (fakeServerPurchase)
		{
			if (!isAuthenticated)
			{
				AppLogger.Info(
					AppLogCategory.Shop,
					nameof(ShopController),
					nameof(OnBuyItem),
					"[SHOP][FAKE_SERVER_AUTH_BYPASS] Fake server purchase continues without authentication.",
					BuildShopItemMetadata(item, "fake_server_auth_bypass"));
			}

			StartCoroutine(CreateFakeServerOrder(item));
			return;
		}

		StartNativePurchase(item);
	}

	IEnumerator CreateServerOrder(ShopItemData item)
	{
		if (_orderInFlight)
			yield break;

		string productId = SaveDataSanitizer.SanitizeIdentifier(item != null ? item.productId : "");
		if (string.IsNullOrEmpty(productId))
		{
			Debug.LogWarning("[Shop] Refused server order without a productId.");
			ShowShopMessage("Товар временно недоступен");
			yield break;
		}

		_orderInFlight = true;
		string error = null;
		string payload = null;
		int quantity = Mathf.Clamp(item.quantity <= 0 ? 1 : item.quantity, 1, MaxOrderQuantity);
		yield return NetworkManager.Instance.CreateShopOrder(productId, quantity, (json, err) =>
		{
			payload = json;
			error = err;
		});

		_orderInFlight = false;

		string apiError = NetworkJson.GetString(payload, "error");
		if (!string.IsNullOrEmpty(error) || !string.IsNullOrEmpty(apiError) || string.IsNullOrEmpty(payload))
		{
			Debug.LogWarning("[Shop] Server order failed: " + FirstRaw(error, apiError));
			ShowShopMessage("Покупка сейчас недоступна");
			yield break;
		}

		if (NetworkManager.Instance != null)
			yield return NetworkManager.Instance.SyncBalance(_ => RefreshBalance());
		else
			RefreshBalance();
		string paymentUrl = NetworkJson.GetString(payload, "paymentUrl");
		if (!string.IsNullOrEmpty(paymentUrl))
			Debug.Log("[Shop] Server order created with payment URL. Open payment flow in platform-specific UI.");

		ShowShopMessage("Заказ создан");
	}

	IEnumerator CreateFakeServerOrder(ShopItemData item)
	{
		if (_orderInFlight)
		{
			AppLogger.Warn(
				AppLogCategory.Shop,
				nameof(ShopController),
				nameof(CreateFakeServerOrder),
				"[SHOP][FAKE_SERVER_BLOCKED] Another order is already in flight.",
				BuildShopItemMetadata(item, "fake_server_order_in_flight"),
				recoverable: true);
			yield break;
		}

		string validationError = ValidateFakeServerPurchase(item, out string productId, out int quantity);
		if (!string.IsNullOrEmpty(validationError))
		{
			IDictionary<string, object> rejectedMetadata = BuildShopItemMetadata(item, "fake_server_rejected");
			AddFakeServerRequestMetadata(rejectedMetadata, productId, quantity);
			rejectedMetadata["validationError"] = validationError;
			AppLogger.Warn(
				AppLogCategory.Shop,
				nameof(ShopController),
				nameof(CreateFakeServerOrder),
				"[SHOP][FAKE_SERVER_REJECTED] Fake shop server rejected order.",
				rejectedMetadata,
				recoverable: true);
			ShowShopMessage("Товар временно недоступен");
			yield break;
		}

		_orderInFlight = true;
		IDictionary<string, object> requestMetadata = BuildShopItemMetadata(item, "fake_server_request");
		AddFakeServerRequestMetadata(requestMetadata, productId, quantity);
		requestMetadata["balanceBeforeHearts"] = PlayerData.Hearts;
		requestMetadata["balanceBeforeCandles"] = PlayerData.Candles;
		requestMetadata["knownCatalogItem"] = IsKnownShopItem(item);
		AppLogger.Info(
			AppLogCategory.Shop,
			nameof(ShopController),
			nameof(CreateFakeServerOrder),
			"[SHOP][FAKE_SERVER_REQUEST] Sending fake shop order request.",
			requestMetadata);

		float delay = Mathf.Clamp(fakeServerResponseDelaySeconds, 0f, 10f);
		if (delay > 0f)
			yield return new WaitForSecondsRealtime(delay);
		else
			yield return null;

		IDictionary<string, object> responseMetadata = BuildShopItemMetadata(item, "fake_server_accepted");
		AddFakeServerRequestMetadata(responseMetadata, productId, quantity);
		responseMetadata["statusCode"] = 200;
		responseMetadata["fakeOrderId"] = "fake_shop_order_" + DateTime.UtcNow.Ticks;
		responseMetadata["balanceBeforeHearts"] = PlayerData.Hearts;
		responseMetadata["balanceBeforeCandles"] = PlayerData.Candles;
		responseMetadata["knownCatalogItem"] = IsKnownShopItem(item);
		AppLogger.Info(
			AppLogCategory.Shop,
			nameof(ShopController),
			nameof(CreateFakeServerOrder),
			"[SHOP][FAKE_SERVER_ACCEPTED] Fake shop server accepted order.",
			responseMetadata);

		if (fakeServerGrantsCurrency)
			GrantFakeServerCurrency(item);
		else
			AppLogger.Info(
				AppLogCategory.Shop,
				nameof(ShopController),
				nameof(CreateFakeServerOrder),
				"[SHOP][FAKE_SERVER_GRANT_SKIPPED] Fake server grant is disabled in inspector.",
				BuildShopItemMetadata(item, "fake_server_grant_skipped"));

		if (fakeServerSyncsRealBalance && NetworkManager.Instance != null && NetworkManager.IsAuthenticated)
		{
			AppLogger.Info(
				AppLogCategory.Shop,
				nameof(ShopController),
				nameof(CreateFakeServerOrder),
				"[SHOP][FAKE_SERVER_SYNC_REAL_BALANCE] Syncing real server balance after fake purchase.",
				BuildShopItemMetadata(item, "fake_server_sync_real_balance"));
			yield return NetworkManager.Instance.SyncBalance(_ => RefreshBalance());
		}
		else
		{
			RefreshBalance();
		}

		IDictionary<string, object> completeMetadata = BuildShopItemMetadata(item, "fake_server_complete");
		AddFakeServerRequestMetadata(completeMetadata, productId, quantity);
		completeMetadata["balanceAfterHearts"] = PlayerData.Hearts;
		completeMetadata["balanceAfterCandles"] = PlayerData.Candles;
		AppLogger.Info(
			AppLogCategory.Shop,
			nameof(ShopController),
			nameof(CreateFakeServerOrder),
			"[SHOP][FAKE_SERVER_COMPLETE] Fake shop purchase completed.",
			completeMetadata);

		_orderInFlight = false;
		ShowShopMessage("Тестовая покупка завершена");
	}

	string ValidateFakeServerPurchase(ShopItemData item, out string productId, out int quantity)
	{
		productId = SaveDataSanitizer.SanitizeIdentifier(item != null ? item.productId : "");
		quantity = item != null && item.quantity > 0 ? item.quantity : 1;

		if (item == null)
			return "missing_item";

		if (string.IsNullOrEmpty(productId))
			return "missing_product_id";

		if (quantity > MaxOrderQuantity)
			return "quantity_too_large";

		if (item.amount <= 0)
			return "non_positive_reward_amount";

		return "";
	}

	bool IsKnownShopItem(ShopItemData target)
	{
		if (target == null || shopItems == null || shopItems.Count == 0)
			return target != null;

		string targetProductId = SaveDataSanitizer.SanitizeIdentifier(target.productId);
		string targetButtonId = SaveDataSanitizer.SanitizeIdentifier(target.buttonId);

		for (int i = 0; i < shopItems.Count; i++)
		{
			ShopItemData item = shopItems[i];
			if (item == null)
				continue;

			string productId = SaveDataSanitizer.SanitizeIdentifier(item.productId);
			if (!string.IsNullOrEmpty(targetProductId) && targetProductId == productId)
				return true;

			string buttonId = SaveDataSanitizer.SanitizeIdentifier(item.buttonId);
			if (!string.IsNullOrEmpty(targetButtonId) && targetButtonId == buttonId)
				return true;
		}

		return false;
	}

	void GrantFakeServerCurrency(ShopItemData item)
	{
		if (item == null || item.amount <= 0)
			return;

		int heartsBefore = PlayerData.Hearts;
		int candlesBefore = PlayerData.Candles;

		switch (item.currency)
		{
			case ShopCurrency.Hearts:
				PlayerData.AddHeartValue(item.amount);
				break;
			case ShopCurrency.Candles:
				PlayerData.AddCandlesValue(item.amount);
				break;
		}

		IDictionary<string, object> grantMetadata = BuildShopItemMetadata(item, "fake_server_grant");
		grantMetadata["serverMode"] = "fake";
		grantMetadata["heartsBefore"] = heartsBefore;
		grantMetadata["candlesBefore"] = candlesBefore;
		grantMetadata["heartsAfter"] = PlayerData.Hearts;
		grantMetadata["candlesAfter"] = PlayerData.Candles;
		AppLogger.Info(
			AppLogCategory.Shop,
			nameof(ShopController),
			nameof(GrantFakeServerCurrency),
			"[SHOP][FAKE_SERVER_GRANT] Fake server granted local test currency.",
			grantMetadata);

		if (ToastManager.Instance != null)
			ToastManager.Instance.ShowSystemMessage($"+{item.amount} {item.currencyLabel}");
	}

	bool IsFakeServerPurchaseEnabled()
	{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
#if UNITY_EDITOR
		if (autoUseFakeServerPurchasesInEditor)
			return true;
#endif
		return useFakeServerPurchases;
#else
        return false;
#endif
	}

	static void AddFakeServerRequestMetadata(IDictionary<string, object> metadata, string productId, int quantity)
	{
		if (metadata == null)
			return;

		metadata["serverMode"] = "fake";
		metadata["httpMethod"] = "POST";
		metadata["endpoint"] = ApiRoutes.ShopOrders;
		metadata["payloadProductId"] = productId ?? "";
		metadata["payloadQuantity"] = quantity;
		metadata["wouldUseRealEndpoint"] = true;
	}

	void StartNativePurchase(ShopItemData item)
	{
		if (_orderInFlight)
		{
			AppLogger.Warn(
				AppLogCategory.Shop,
				nameof(ShopController),
				nameof(StartNativePurchase),
				"[SHOP][PURCHASE_BLOCKED] Another order is already in flight.",
				BuildShopItemMetadata(item, "order_in_flight"),
				recoverable: true);
			return;
		}

		_nativeIap = NativeIapManager.GetOrCreate();
		if (_nativeIap == null)
		{
			AppLogger.Warn(
				AppLogCategory.Shop,
				nameof(ShopController),
				nameof(StartNativePurchase),
				"[SHOP][PURCHASE_DENIED] Native IAP manager is unavailable.",
				BuildShopItemMetadata(item, "iap_unavailable"),
				recoverable: true);
			ShowShopMessage("Покупки временно недоступны");
			return;
		}

		AppLogger.Info(
			AppLogCategory.Shop,
			nameof(ShopController),
			nameof(StartNativePurchase),
			"[SHOP][PURCHASE_START] Starting native IAP purchase.",
			BuildShopItemMetadata(item, "purchase_start"));

		_nativeIap.ConfigureProducts(shopItems);
		_orderInFlight = true;
		_nativeIap.Purchase(item, (ok, message) =>
		{
			_orderInFlight = false;
			AppLogger.Info(
				AppLogCategory.Shop,
				nameof(ShopController),
				nameof(StartNativePurchase),
				"[SHOP][PURCHASE_CALLBACK] Native IAP purchase callback received.",
				AddShopMessageMetadata(BuildShopItemMetadata(item, ok ? "purchase_ok" : "purchase_failed"), ok, message));
			if (ok)
			{
				if (NetworkManager.Instance != null)
					StartCoroutine(NetworkManager.Instance.SyncBalance(_ => RefreshBalance()));
				else
					RefreshBalance();
			}

			ShowShopMessage(string.IsNullOrEmpty(message)
				? ok ? "Покупка завершена" : "Покупка сейчас недоступна"
				: message);
		});
	}

	public void RestoreNativePurchases()
	{
		if (_orderInFlight)
		{
			AppLogger.Warn(
				AppLogCategory.Shop,
				nameof(ShopController),
				nameof(RestoreNativePurchases),
				"[SHOP][RESTORE_BLOCKED] Another order is already in flight.",
				BuildShopMetadata("restore_order_in_flight"),
				recoverable: true);
			return;
		}

		if (NetworkManager.Instance == null || !NetworkManager.IsAuthenticated)
		{
			AppLogger.Warn(
				AppLogCategory.Shop,
				nameof(ShopController),
				nameof(RestoreNativePurchases),
				"[SHOP][RESTORE_DENIED] Player is not authenticated.",
				BuildShopMetadata("restore_not_authenticated"),
				recoverable: true);
			ShowShopMessage("Войдите в аккаунт для восстановления покупок");
			return;
		}

		_nativeIap = NativeIapManager.GetOrCreate();
		if (_nativeIap == null)
		{
			AppLogger.Warn(
				AppLogCategory.Shop,
				nameof(ShopController),
				nameof(RestoreNativePurchases),
				"[SHOP][RESTORE_DENIED] Native IAP manager is unavailable.",
				BuildShopMetadata("restore_iap_unavailable"),
				recoverable: true);
			ShowShopMessage("Покупки временно недоступны");
			return;
		}

		_orderInFlight = true;
		AppLogger.Info(
			AppLogCategory.Shop,
			nameof(ShopController),
			nameof(RestoreNativePurchases),
			"[SHOP][RESTORE_START] Restoring native purchases.",
			BuildShopMetadata("restore_start"));
		_nativeIap.Restore((ok, message) =>
		{
			_orderInFlight = false;
			AppLogger.Info(
				AppLogCategory.Shop,
				nameof(ShopController),
				nameof(RestoreNativePurchases),
				"[SHOP][RESTORE_CALLBACK] Restore callback received.",
				AddShopMessageMetadata(BuildShopMetadata(ok ? "restore_ok" : "restore_failed"), ok, message));
			if (ok && NetworkManager.Instance != null)
				StartCoroutine(NetworkManager.Instance.SyncBalance(_ => RefreshBalance()));
			else
				RefreshBalance();

			ShowShopMessage(string.IsNullOrEmpty(message)
				? ok ? "Покупки восстановлены" : "Восстановление сейчас недоступно"
				: message);
		});
	}

	[Obsolete("Shop purchases must be confirmed by the server IAP endpoint.")]
	void TryGrantPrototypeCurrency(ShopItemData item)
	{
		if (!PrototypeFeatureFlags.ShopCurrencyGrantsEnabled)
		{
			Debug.LogWarning("[Shop] Prototype currency grants are disabled. Route this purchase through IAP/API.");
			ShowShopMessage("Магазин требует подключения к серверу");
			return;
		}

		if (item.amount <= 0)
		{
			Debug.LogWarning("[Shop] Ignored item with non-positive amount: " + item.label, this);
			return;
		}

		Debug.Log($"[Shop] Purchase: {item.label} ({item.amount} {item.currency})");

		switch (item.currency)
		{
			case ShopCurrency.Hearts:
				PlayerData.AddHeartValue(item.amount);
				break;
			case ShopCurrency.Candles:
				PlayerData.AddCandlesValue(item.amount);
				break;
		}

		RefreshBalance();

		if (ToastManager.Instance != null)
			ToastManager.Instance.ShowSystemMessage($"+{item.amount} {item.currencyLabel}");
	}

	public void Open(Action onCloseCallback = null)
	{
		ResolveSceneReferences();

		onClose = onCloseCallback;

		AppLogger.Info(
			AppLogCategory.Shop,
			nameof(ShopController),
			nameof(Open),
			"[SHOP][OPEN] Shop opened.",
			BuildShopMetadata("open"));

		if (panel == null)
		{
			AppLogger.Warn(
				AppLogCategory.Shop,
				nameof(ShopController),
				nameof(Open),
				"[SHOP][OPEN_FAILED] Shop panel is not assigned.",
				BuildShopMetadata("open_missing_panel"),
				recoverable: true);
			Debug.LogWarning("ShopController: panel is not assigned.", this);
			Close();
			return;
		}

		PrepareShopPresentation("open", allowBalanceSync: true, ensurePanelActive: true);

		var cg = panel.GetComponent<CanvasGroup>();
		if (cg != null)
		{
			_panelFadeTween?.Kill();
			cg.alpha = 0f;
			_panelFadeTween = cg.DOFade(1f, 0.25f);
		}
	}

	public void Close()
	{
		AppLogger.Info(
			AppLogCategory.Shop,
			nameof(ShopController),
			nameof(Close),
			"[SHOP][CLOSE] Shop closed.",
			BuildShopMetadata("close"));

		_panelFadeTween?.Kill();
		_panelFadeTween = null;

		if (panel != null)
		{
			ApplyPanelInputState(false);
			panel.SetActive(false);
		}

		var callback = onClose;
		onClose = null;
		try
		{
			callback?.Invoke();
		}
		catch (Exception exception)
		{
			Debug.LogWarning($"ShopController: close callback failed: {exception.Message}", this);
		}
	}

	public void RefreshDisplayedBalance()
	{
		RefreshBalance();
	}

	void HandlePlayerBalanceChanged()
	{
		if (Instance != this)
			return;

		RefreshBalance();
	}

	void HandleCurrentScreenChanged(string screenId)
	{
		if (Instance != this)
			return;

		if (!string.Equals(UIScreenState.NormalizeScreenId(screenId), "Shop", StringComparison.Ordinal))
			return;

		PrepareShopPresentation("screen_current_shop", allowBalanceSync: false, ensurePanelActive: true);
	}

	void BindRuntimeEvents()
	{
		if (Instance != this)
			return;

		PlayerData.BalanceChanged -= HandlePlayerBalanceChanged;
		PlayerData.BalanceChanged += HandlePlayerBalanceChanged;

		UIScreenState.CurrentScreenChanged -= HandleCurrentScreenChanged;
		UIScreenState.CurrentScreenChanged += HandleCurrentScreenChanged;
	}

	void UnbindRuntimeEvents()
	{
		PlayerData.BalanceChanged -= HandlePlayerBalanceChanged;
		UIScreenState.CurrentScreenChanged -= HandleCurrentScreenChanged;
	}

	void PrepareShopPresentation(string reason, bool allowBalanceSync, bool ensurePanelActive)
	{
		ResolveSceneReferences();

		if (ensurePanelActive && panel != null)
		{
			panel.SetActive(true);
			ApplyPanelInputState(true);
		}

		if (allowBalanceSync)
			RefreshBalanceOnOpen();
		else
			RefreshBalance();

		EnsureShopBackgroundImagesVisible(reason);

		if (NetworkManager.Instance != null && NetworkManager.IsAuthenticated)
			StartCoroutine(RefreshServerShopItems());
		else if (_nativeIap != null)
			_nativeIap.ConfigureProducts(shopItems);
	}

	void RefreshBalanceOnOpen()
	{
		if (syncBalanceOnOpen && NetworkManager.Instance != null && NetworkManager.IsAuthenticated)
		{
			if (showBalanceLoadingWhileSyncing)
				SetBalanceTexts(balanceLoadingText);

			StartCoroutine(SyncBalanceForShopOpen());
			return;
		}

		RefreshBalance();
	}

	IEnumerator SyncBalanceForShopOpen()
	{
		NetworkManager network = NetworkManager.Instance;
		if (network == null || !NetworkManager.IsAuthenticated)
		{
			RefreshBalance();
			yield break;
		}

		bool synced = false;
		yield return network.SyncBalance(ok => synced = ok);
		RefreshBalance();

		if (!synced)
		{
			AppLogger.Warn(
				AppLogCategory.Shop,
				nameof(ShopController),
				nameof(SyncBalanceForShopOpen),
				"[SHOP][BALANCE_SYNC_FAILED] Failed to sync shop balance on open, using current local PlayerData.",
				BuildShopMetadata("balance_sync_failed"),
				recoverable: true);
		}
	}

	void RefreshBalance()
	{
		AutoCorrectBalanceTextReferences();

		if (heartsBalanceText != null)
			heartsBalanceText.text = PlayerData.Hearts.ToString();

		if (candlesBalanceText != null)
			candlesBalanceText.text = PlayerData.Candles.ToString();
	}

	void SetBalanceTexts(string value)
	{
		AutoCorrectBalanceTextReferences();

		value ??= "";

		if (heartsBalanceText != null)
			heartsBalanceText.text = value;

		if (candlesBalanceText != null)
			candlesBalanceText.text = value;
	}

	void RegisterInstance()
	{
		if (IsDetachedEmptyController())
		{
			AppLogger.Warn(
				AppLogCategory.Shop,
				nameof(ShopController),
				nameof(RegisterInstance),
				"[SHOP][DUPLICATE_IGNORED] Empty detached ShopController was disabled.",
				BuildShopMetadata("duplicate_empty_detached"),
				recoverable: true);
			DisableDuplicateComponent();
			return;
		}

		if (Instance == null || Instance == this)
		{
			Instance = this;
			return;
		}

		int currentScore = EvaluateControllerScore();
		int instanceScore = Instance.EvaluateControllerScore();

		if (currentScore > instanceScore)
		{
			ShopController previous = Instance;
			var metadata = BuildShopMetadata("duplicate_replaced_previous");
			metadata["previousControllerPath"] = GetHierarchyPath(previous != null ? previous.transform : null);
			metadata["previousControllerScore"] = instanceScore;

			Instance = this;

			AppLogger.Warn(
				AppLogCategory.Shop,
				nameof(ShopController),
				nameof(RegisterInstance),
				"[SHOP][DUPLICATE_REPLACED] Better ShopController replaced an earlier duplicate.",
				metadata,
				recoverable: true);

			if (previous != null)
				previous.DisableDuplicateComponent();
			return;
		}

		var ignoredMetadata = BuildShopMetadata("duplicate_ignored_lower_score");
		ignoredMetadata["activeControllerPath"] = GetHierarchyPath(Instance.transform);
		ignoredMetadata["activeControllerScore"] = instanceScore;

		AppLogger.Warn(
			AppLogCategory.Shop,
			nameof(ShopController),
			nameof(RegisterInstance),
			"[SHOP][DUPLICATE_IGNORED] Lower-priority ShopController was disabled without destroying its GameObject.",
			ignoredMetadata,
			recoverable: true);
		DisableDuplicateComponent();
	}

	void DisableDuplicateComponent()
	{
		if (Instance == this)
			Instance = null;

		enabled = false;
		Destroy(this);
	}

	void ResolveSceneReferences()
	{
		if (panel == null && ShouldUseSelfAsPanel())
			panel = gameObject;

		if (itemContainer == null)
			itemContainer = ResolveItemContainer();

		if (closeButton == null)
			closeButton = FindButtonByNames("Close", "Exit", "Back", "CloseButton", "ExitButton");

		if (titleText == null)
			titleText = FindTextByNames("Title", "Header", "ShopTitle");

		if (heartsBalanceText == null)
			heartsBalanceText = FindTextByNames("Hearts", "Heart");

		if (candlesBalanceText == null)
			candlesBalanceText = FindTextByNames("Candles", "Candle");

		AutoCorrectBalanceTextReferences();
	}

	void AutoCorrectBalanceTextReferences()
	{
		if (!autoCorrectSwappedBalanceTexts || heartsBalanceText == null || candlesBalanceText == null)
			return;

		bool heartsLooksLikeCandles = TextPathContainsAny(heartsBalanceText, "candle", "candles", "свеч");
		bool candlesLooksLikeHearts = TextPathContainsAny(candlesBalanceText, "heart", "hearts", "серд", "искр");
		if (!heartsLooksLikeCandles || !candlesLooksLikeHearts)
			return;

		TMP_Text tmp = heartsBalanceText;
		heartsBalanceText = candlesBalanceText;
		candlesBalanceText = tmp;
	}

	bool TextPathContainsAny(TMP_Text text, params string[] needles)
	{
		string path = BuildLocalTransformPath(text != null ? text.transform : null);
		if (string.IsNullOrEmpty(path) || needles == null)
			return false;

		for (int i = 0; i < needles.Length; i++)
		{
			string needle = needles[i];
			if (!string.IsNullOrEmpty(needle) && path.Contains(needle.ToLowerInvariant()))
				return true;
		}

		return false;
	}

	string BuildLocalTransformPath(Transform target)
	{
		if (target == null)
			return "";

		string path = "";
		Transform current = target;
		int guard = 0;
		while (current != null && current != transform && guard++ < 32)
		{
			path = string.IsNullOrEmpty(path) ? current.name : current.name + "/" + path;
			current = current.parent;
		}

		return path.ToLowerInvariant();
	}

	bool ShouldUseSelfAsPanel()
	{
		if (GetComponent<UIScreenMarker>() != null || GetComponent<CanvasGroup>() != null)
			return true;

		return transform is RectTransform && GetComponentInParent<Canvas>() != null;
	}

	Transform ResolveItemContainer()
	{
		ScrollRect scrollRect = GetComponentInChildren<ScrollRect>(true);
		if (scrollRect != null && scrollRect.content != null)
			return scrollRect.content;

		return FindChildTransformByNames("ItemContainer", "ShopItems", "Items", "Content");
	}

	Button FindButtonByNames(params string[] names)
	{
		Button[] buttons = GetComponentsInChildren<Button>(true);
		for (int n = 0; n < names.Length; n++)
		{
			string expected = names[n];
			for (int i = 0; i < buttons.Length; i++)
			{
				Button button = buttons[i];
				if (button != null && NameContains(button.name, expected))
					return button;
			}
		}

		return null;
	}

	TMP_Text FindTextByNames(params string[] names)
	{
		TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
		for (int n = 0; n < names.Length; n++)
		{
			string expected = names[n];
			for (int i = 0; i < texts.Length; i++)
			{
				TMP_Text text = texts[i];
				if (text != null && NameContains(text.name, expected))
					return text;
			}
		}

		return null;
	}

	Transform FindChildTransformByNames(params string[] names)
	{
		Transform[] children = GetComponentsInChildren<Transform>(true);
		for (int n = 0; n < names.Length; n++)
		{
			string expected = names[n];
			for (int i = 0; i < children.Length; i++)
			{
				Transform child = children[i];
				if (child != null && child != transform && NameContains(child.name, expected))
					return child;
			}
		}

		return null;
	}

	bool IsDetachedEmptyController()
	{
		return gameObject.activeInHierarchy
			&& transform.parent == null
			&& !(transform is RectTransform)
			&& GetComponent<UIScreenMarker>() == null
			&& GetComponent<CanvasGroup>() == null
			&& panel == null
			&& itemContainer == null
			&& closeButton == null
			&& restoreButton == null
			&& titleText == null
			&& heartsBalanceText == null
			&& candlesBalanceText == null
			&& shopItemPrefab == null
			&& (shopItems == null || shopItems.Count == 0);
	}

	int EvaluateControllerScore()
	{
		int score = 0;

		if (panel != null)
			score += 100;
		if (GetComponent<UIScreenMarker>() != null)
			score += 80;
		if (GetComponent<CanvasGroup>() != null)
			score += 40;
		if (transform is RectTransform)
			score += 20;
		if (GetComponentInParent<Canvas>() != null)
			score += 20;
		if (itemContainer != null)
			score += 30;
		if (shopItemPrefab != null)
			score += 20;
		if (closeButton != null)
			score += 10;
		if (shopItems != null && shopItems.Count > 0)
			score += 10;

		score += Mathf.Min(transform.childCount, 20);

		if (transform.parent == null && GetComponent<UIScreenMarker>() == null && panel == null)
			score -= 100;

		return score;
	}

	void ApplyPanelInputState(bool visible)
	{
		if (panel == null)
			return;

		CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
		if (canvasGroup == null)
			return;

		canvasGroup.interactable = visible;
		canvasGroup.blocksRaycasts = visible;
		if (!visible)
			canvasGroup.alpha = 0f;
	}

	void EnsureShopBackgroundImagesVisible(string reason)
	{
		if (!keepShopBackgroundImagesEnabled || panel == null)
			return;

		Image[] images = panel.GetComponentsInChildren<Image>(true);
		int restored = 0;
		for (int i = 0; i < images.Length; i++)
		{
			Image image = images[i];
			if (image == null || !IsLikelyShopBackgroundImage(image))
				continue;

			GameObject target = image.gameObject;
			if (target.activeSelf)
				continue;

			target.SetActive(true);
			restored++;
			AppLogger.Warn(
				AppLogCategory.Shop,
				nameof(ShopController),
				nameof(EnsureShopBackgroundImagesVisible),
				"[SHOP][BACKGROUND_RESTORED] Disabled shop background Image was re-enabled.",
				LogMetadata.Of(
					"reason", reason ?? "",
					"backgroundObject", target.name,
					"backgroundPath", GetHierarchyPath(target.transform),
					"sprite", image.sprite != null ? image.sprite.name : ""),
				recoverable: true);
		}

		if (restored > 0)
		{
			AppLogger.Info(
				AppLogCategory.Shop,
				nameof(ShopController),
				nameof(EnsureShopBackgroundImagesVisible),
				"[SHOP][BACKGROUND_CHECK] Shop background check completed.",
				LogMetadata.Of("reason", reason ?? "", "restored", restored));
		}
	}

	static bool IsLikelyShopBackgroundImage(Image image)
	{
		if (image == null)
			return false;

		string objectName = NormalizeShopObjectName(image.gameObject.name);
		if (objectName == "background"
			|| objectName == "shopbackground"
			|| objectName == "shopback"
			|| objectName == "panelbackground")
			return true;

		string spriteName = NormalizeShopObjectName(image.sprite != null ? image.sprite.name : "");
		return objectName == "background" && (spriteName == "back" || spriteName == "background");
	}

	static string NormalizeShopObjectName(string value)
	{
		if (string.IsNullOrEmpty(value))
			return "";

		return value
			.Replace(" ", "")
			.Replace("_", "")
			.Replace("-", "")
			.ToLowerInvariant();
	}

	static bool NameContains(string source, string expected)
	{
		return !string.IsNullOrEmpty(source)
			&& !string.IsNullOrEmpty(expected)
			&& source.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
	}

	IEnumerator RefreshServerShopItems()
	{
		if (_loadingRemoteShop || NetworkManager.Instance == null || !NetworkManager.IsAuthenticated)
			yield break;

		AppLogger.Info(
			AppLogCategory.Shop,
			nameof(ShopController),
			nameof(RefreshServerShopItems),
			"[SHOP][CATALOG] Loading remote shop catalog.",
			BuildShopMetadata("remote_catalog_start"));

		_loadingRemoteShop = true;
		List<ShopItemData> remoteItems = null;
		string error = null;
		yield return NetworkManager.Instance.FetchPurchaseProducts((json, err) =>
		{
			error = err;
			remoteItems = string.IsNullOrEmpty(err) ? ParseRemoteShopItems(json) : null;
		});

		if (!string.IsNullOrEmpty(error) || remoteItems == null || remoteItems.Count == 0)
		{
			string purchaseProductsError = error;
			yield return NetworkManager.Instance.FetchShopItems((json, err) =>
			{
				error = err;
				remoteItems = string.IsNullOrEmpty(err) ? ParseRemoteShopItems(json) : null;
			});

			if (!string.IsNullOrEmpty(purchaseProductsError) && string.IsNullOrEmpty(error))
			{
				AppLogger.Warn(
					AppLogCategory.Shop,
					nameof(ShopController),
					nameof(RefreshServerShopItems),
					"[SHOP][CATALOG] Purchase products endpoint failed; shop items fallback succeeded.",
					AddShopErrorMetadata(BuildShopMetadata("remote_catalog_fallback"), purchaseProductsError),
					recoverable: true);
				Debug.LogWarning("[Shop] " + ApiRoutes.PurchasesProducts + " failed, used " + ApiRoutes.ShopItems + " fallback: " + purchaseProductsError);
			}
		}

		List<ShopItemData> remotePrices = null;
		string priceError = null;
		yield return NetworkManager.Instance.FetchShopPrices((json, err) =>
		{
			priceError = err;
			remotePrices = string.IsNullOrEmpty(err) ? ParseRemoteShopItems(json) : null;
		});

		if (remotePrices != null && remotePrices.Count > 0)
		{
			remoteItems = MergeShopItemLists(remoteItems, remotePrices);
			error = remoteItems != null && remoteItems.Count > 0 ? null : error;
		}
		else if (!string.IsNullOrEmpty(priceError))
		{
			AppLogger.Warn(
				AppLogCategory.Shop,
				nameof(ShopController),
				nameof(RefreshServerShopItems),
				"[SHOP][PRICES_FAILED] Remote shop prices failed to load.",
				AddShopErrorMetadata(BuildShopMetadata("remote_prices_failed"), priceError),
				recoverable: true);
		}

		_loadingRemoteShop = false;

		if (!string.IsNullOrEmpty(error))
		{
			AppLogger.Warn(
				AppLogCategory.Shop,
				nameof(ShopController),
				nameof(RefreshServerShopItems),
				"[SHOP][CATALOG_FAILED] Remote shop catalog failed to load.",
				AddShopErrorMetadata(BuildShopMetadata("remote_catalog_failed"), error),
				recoverable: true);
			Debug.LogWarning("[Shop] Failed to load server shop items: " + error);
			yield break;
		}

		if (remoteItems == null || remoteItems.Count == 0)
		{
			AppLogger.Warn(
				AppLogCategory.Shop,
				nameof(ShopController),
				nameof(RefreshServerShopItems),
				"[SHOP][CATALOG_EMPTY] Remote shop catalog returned no items.",
				BuildShopMetadata("remote_catalog_empty"),
				recoverable: true);
			yield break;
		}

		shopItems = remoteItems;
		_hasLoadedRemoteShopItems = true;
		_nativeIap = NativeIapManager.GetOrCreate();
		if (_nativeIap != null)
			_nativeIap.ConfigureProducts(shopItems);
		BuildShop();

		AppLogger.Info(
			AppLogCategory.Shop,
			nameof(ShopController),
			nameof(RefreshServerShopItems),
			"[SHOP][CATALOG_LOADED] Remote shop catalog loaded.",
			BuildShopMetadata("remote_catalog_loaded"));
	}

	IDictionary<string, object> BuildShopMetadata(string reason)
	{
		return LogMetadata.Of(
			"reason", reason ?? "",
			"panelAssigned", panel != null,
			"panelActive", panel != null && panel.activeSelf,
			"itemContainerAssigned", itemContainer != null,
			"prefabAssigned", shopItemPrefab != null,
			"shopItems", shopItems != null ? shopItems.Count : 0,
			"staticProductButtons", FindBoundProductViews().Count,
			"remoteShopLoaded", _hasLoadedRemoteShopItems,
			"controllerObject", name,
			"controllerPath", GetHierarchyPath(transform),
			"controllerScore", EvaluateControllerScore(),
			"isActiveInstance", Instance == this,
			"loadingRemoteShop", _loadingRemoteShop,
			"orderInFlight", _orderInFlight,
			"useFakeServerPurchases", useFakeServerPurchases,
			"autoUseFakeServerPurchasesInEditor", autoUseFakeServerPurchasesInEditor,
			"fakeServerPurchasesEnabled", IsFakeServerPurchaseEnabled(),
			"fakeServerRequiresAuthentication", fakeServerRequiresAuthentication,
			"fakeServerGrantsCurrency", fakeServerGrantsCurrency,
			"fakeServerSyncsRealBalance", fakeServerSyncsRealBalance,
			"fakeServerResponseDelaySeconds", fakeServerResponseDelaySeconds,
			"keepShopBackgroundImagesEnabled", keepShopBackgroundImagesEnabled,
			"authenticated", NetworkManager.IsAuthenticated,
			"hasNetwork", NetworkManager.Instance != null,
			"hasNativeIap", _nativeIap != null,
			"nativeIapReady", _nativeIap != null && _nativeIap.IsReady,
			"hearts", PlayerData.Hearts,
			"candles", PlayerData.Candles);
	}

	IDictionary<string, object> BuildShopItemMetadata(ShopItemData item, string reason)
	{
		var metadata = BuildShopMetadata(reason);
		metadata["buttonId"] = SaveDataSanitizer.SanitizeIdentifier(item != null ? item.buttonId : "");
		metadata["productId"] = SaveDataSanitizer.SanitizeIdentifier(item != null ? item.productId : "");
		metadata["label"] = item != null ? item.label : "";
		metadata["amount"] = item != null ? item.amount : 0;
		metadata["amountDisplay"] = item != null ? item.amountDisplay : "";
		metadata["currency"] = item != null ? item.currency.ToString() : "";
		metadata["currencyLabel"] = item != null ? item.currencyLabel : "";
		metadata["priceLabel"] = item != null ? item.priceLabel : "";
		metadata["quantity"] = item != null ? item.quantity : 0;
		metadata["productType"] = item != null ? item.productType.ToString() : "";
		metadata["sortOrder"] = item != null ? item.sortOrder : 0;
		metadata["hasSortOrder"] = item != null && item.hasSortOrder;
		return metadata;
	}

	static IDictionary<string, object> AddShopMessageMetadata(IDictionary<string, object> metadata, bool ok, string message)
	{
		if (metadata == null)
			metadata = LogMetadata.Of();

		metadata["ok"] = ok;
		metadata["message"] = message ?? "";
		return metadata;
	}

	static IDictionary<string, object> AddShopErrorMetadata(IDictionary<string, object> metadata, string error)
	{
		if (metadata == null)
			metadata = LogMetadata.Of();

		metadata["error"] = error ?? "";
		return metadata;
	}

	static string GetHierarchyPath(Transform target)
	{
		if (target == null)
			return "";

		var parts = new List<string>();
		Transform current = target;
		while (current != null)
		{
			parts.Add(current.name);
			current = current.parent;
		}

		parts.Reverse();
		return string.Join("/", parts);
	}

	void HandleNativeIapProductsUpdated()
	{
		ApplyNativePricesToShopItems();
		if (panel != null && panel.activeInHierarchy)
			BuildShop();
	}

	void ApplyNativePricesToShopItems()
	{
		if (_nativeIap == null || shopItems == null)
			return;

		foreach (var item in shopItems)
		{
			ApplyNativePriceToShopItem(item);
		}
	}

	void ApplyNativePriceToShopItem(ShopItemData item)
	{
		if (_nativeIap == null || item == null || string.IsNullOrEmpty(item.productId))
			return;

		if (_nativeIap.TryGetLocalizedPrice(item.productId, out string localizedPrice))
			item.priceLabel = localizedPrice;
	}

	[Obsolete("Use server-confirmed IAP purchases instead of direct client grants.")]
	public void BuyCandles(int count)
	{
		if (NetworkManager.IsAuthenticated)
		{
			Debug.LogWarning("[Shop] Direct candle grants are blocked for authenticated players.");
			return;
		}

		if (!PrototypeFeatureFlags.ShopCurrencyGrantsEnabled || count <= 0)
			return;

		PlayerData.AddCandlesValue(count);
		RefreshBalance();
	}

	[Obsolete("Use server-confirmed IAP purchases instead of direct client grants.")]
	public void BuyHearts(int count)
	{
		if (NetworkManager.IsAuthenticated)
		{
			Debug.LogWarning("[Shop] Direct heart grants are blocked for authenticated players.");
			return;
		}

		if (!PrototypeFeatureFlags.ShopCurrencyGrantsEnabled || count <= 0)
			return;

		PlayerData.AddHeartValue(count);
		RefreshBalance();
	}

	public static List<ShopItemData> ParseRemoteShopItems(string json)
	{
		var result = new List<ShopItemData>();
		string rawItems = ResolveRemoteItemsArray(json);
		if (string.IsNullOrWhiteSpace(rawItems))
			return result;

		foreach (string rawItem in NetworkJson.GetArrayItems(rawItems))
		{
			if (result.Count >= MaxRemoteShopItems)
				break;

			var item = ParseRemoteShopItem(rawItem);
			if (item != null)
				result.Add(item);
		}

		return result;
	}

	static string ResolveRemoteItemsArray(string json)
	{
		if (string.IsNullOrWhiteSpace(json))
			return "";

		string trimmed = json.Trim();
		if (trimmed.StartsWith("[", StringComparison.Ordinal))
			return trimmed;

		return FirstRaw(
			NetworkJson.GetRawValue(trimmed, "items"),
			NetworkJson.GetRawValue(trimmed, "products"),
			NetworkJson.GetRawValue(trimmed, "shopItems"),
			NetworkJson.GetRawValue(trimmed, "data"));
	}

	static ShopItemData ParseRemoteShopItem(string raw)
	{
		if (string.IsNullOrWhiteSpace(raw) || !NetworkJson.LooksLikeJsonObject(raw))
			return null;

		if (NetworkJson.GetRawValue(raw, "isActive") != null && !NetworkJson.GetBool(raw, "isActive", true))
			return null;
		if (NetworkJson.GetRawValue(raw, "active") != null && !NetworkJson.GetBool(raw, "active", true))
			return null;
		if (NetworkJson.GetRawValue(raw, "enabled") != null && !NetworkJson.GetBool(raw, "enabled", true))
			return null;

		string buttonId = SaveDataSanitizer.SanitizeIdentifier(FirstString(raw, "buttonId", "buttonID", "slotId", "viewId", "clientButtonId", "unityButtonId"));
		string productId = SaveDataSanitizer.SanitizeIdentifier(FirstString(raw, "productId", "id", "sku", "storeProductId"));
		if (string.IsNullOrEmpty(productId) && string.IsNullOrEmpty(buttonId))
			return null;

		string currencyRaw = (FirstString(raw, "currency", "rewardCurrency", "resource", "type") ?? "").ToLowerInvariant();
		ShopCurrency currency = ResolveShopCurrency(currencyRaw, raw, productId);
		int amount = ResolveShopAmount(raw, currency);
		string amountDisplay = SanitizeShopText(FirstString(raw, "amountDisplay", "amountLabel", "rewardAmountLabel", "rewardDisplay", "countLabel"));
		string currencyLabel = SanitizeShopText(FirstString(raw, "currencyLabel", "resourceLabel"));
		if (string.IsNullOrEmpty(currencyLabel))
			currencyLabel = currency == ShopCurrency.Hearts ? "Искры" : "Свечи";

		string label = SanitizeShopText(FirstString(raw, "label", "title", "name", "displayName"));
		if (string.IsNullOrEmpty(label))
			label = amount > 0 ? amount + " " + currencyLabel : FirstRaw(productId, buttonId);

		string priceLabel = SanitizeShopText(FirstString(raw, "priceLabel", "displayPrice", "localizedPrice", "priceText", "price"));
		if (string.IsNullOrEmpty(priceLabel))
			priceLabel = " ";

		bool hasSortOrder = TryGetFirstInt(raw, out int sortOrder, "sortOrder", "order", "position", "displayOrder", "uiOrder");

		return new ShopItemData
		{
			buttonId = buttonId,
			label = label,
			amount = amount,
			amountDisplay = amountDisplay,
			currency = currency,
			currencyLabel = currencyLabel,
			priceLabel = priceLabel,
			productId = productId,
			productType = ResolveProductType(raw),
			quantity = Mathf.Clamp(NetworkJson.GetInt(raw, "quantity", 1), 1, MaxOrderQuantity),
			sortOrder = sortOrder,
			hasSortOrder = hasSortOrder
		};
	}

	static ShopCurrency ResolveShopCurrency(string currencyRaw, string raw, string productId)
	{
		if (currencyRaw.Contains("candle") || currencyRaw.Contains("свеч"))
			return ShopCurrency.Candles;
		if (currencyRaw.Contains("heart") || currencyRaw.Contains("искр"))
			return ShopCurrency.Hearts;
		if (NetworkJson.GetRawValue(raw, "candles") != null)
			return ShopCurrency.Candles;
		if (!string.IsNullOrEmpty(productId) && productId.ToLowerInvariant().Contains("candle"))
			return ShopCurrency.Candles;
		return ShopCurrency.Hearts;
	}

	static int ResolveShopAmount(string raw, ShopCurrency currency)
	{
		string specificKey = currency == ShopCurrency.Candles ? "candles" : "hearts";
		int amount = NetworkJson.GetInt(raw, specificKey, 0);
		if (amount <= 0)
			amount = NetworkJson.GetInt(raw, "amount", 0);
		if (amount <= 0)
			amount = NetworkJson.GetInt(raw, "value", 0);
		if (amount <= 0)
			amount = NetworkJson.GetInt(raw, "count", 0);
		return SaveDataSanitizer.ClampCurrencyValue(amount);
	}

	static ProductType ResolveProductType(string raw)
	{
		string type = (FirstString(raw, "productType", "iapType", "storeType", "kind") ?? "").Trim().ToLowerInvariant();
		if (type.Contains("subscription") || type.Contains("subs"))
			return ProductType.Subscription;
		if (type.Contains("nonconsumable") || type.Contains("non_consumable") || type.Contains("permanent"))
			return ProductType.NonConsumable;
		return ProductType.Consumable;
	}

	static bool TryGetFirstInt(string json, out int value, params string[] keys)
	{
		value = 0;
		if (keys == null)
			return false;

		for (int i = 0; i < keys.Length; i++)
		{
			string key = keys[i];
			if (string.IsNullOrEmpty(key) || NetworkJson.GetRawValue(json, key) == null)
				continue;

			value = NetworkJson.GetInt(json, key, 0);
			return true;
		}

		return false;
	}

	static string FirstString(string json, params string[] keys)
	{
		if (keys == null)
			return "";

		for (int i = 0; i < keys.Length; i++)
		{
			string value = NetworkJson.GetString(json, keys[i]);
			if (!string.IsNullOrWhiteSpace(value))
				return value;
		}

		return "";
	}

	static string FirstRaw(params string[] values)
	{
		if (values == null)
			return "";

		for (int i = 0; i < values.Length; i++)
		{
			if (!string.IsNullOrWhiteSpace(values[i]) && values[i] != "null")
				return values[i];
		}

		return "";
	}

	static string SanitizeShopText(string value)
	{
		value = SaveDataSanitizer.SanitizeContentText(value);
		if (string.IsNullOrEmpty(value))
			return "";
		return value.Length <= MaxShopTextChars ? value : value.Substring(0, MaxShopTextChars);
	}

	static void ShowShopMessage(string message)
	{
		if (ToastManager.Instance != null)
			ToastManager.Instance.ShowSystemMessage(message);
	}
}

[Serializable]
public class ShopItemData
{
	public string label;
	public Sprite icon;
	public int amount;
	public string amountDisplay;
	public ShopCurrency currency;
	public string currencyLabel;
	public string priceLabel;
	public string buttonId;
	public string productId;
	public ProductType productType = ProductType.Consumable;
	public int quantity = 1;
	public int sortOrder;
	public bool hasSortOrder;
}

public enum ShopCurrency
{
	[InspectorName("Искры/сердца")]
	Hearts,
	[InspectorName("Свечи")]
	Candles
}

public sealed class NativeIapManager : MonoBehaviour
{
	const int MaxCatalogProducts = 100;

	public static NativeIapManager Instance { get; private set; }
	public bool IsReady => _connected && _productsFetched && _catalogByProductId.Count > 0;
	public event Action ProductsUpdated;

	StoreController _storeController;
	bool _initializing;
	bool _connected;
	bool _productsFetched;
	bool _fetchingServerProducts;

	readonly Dictionary<string, ShopItemData> _catalogByProductId =
		new Dictionary<string, ShopItemData>(StringComparer.Ordinal);
	readonly Dictionary<string, Action<bool, string>> _purchaseCallbacksByProductId =
		new Dictionary<string, Action<bool, string>>(StringComparer.Ordinal);
	readonly HashSet<string> _confirmingTransactions =
		new HashSet<string>(StringComparer.Ordinal);

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	static void ResetStaticState()
	{
		Instance = null;
	}

	public static NativeIapManager GetOrCreate()
	{
		if (!IsNativeStoreRuntimeSupported())
			return null;

		if (Instance != null)
			return Instance;

		var host = new GameObject("NativeIapManager");
		DontDestroyOnLoad(host);
		return host.AddComponent<NativeIapManager>();
	}

	public static bool IsNativeStoreRuntimeSupported()
	{
		if (Application.isEditor)
			return false;

		return Application.platform == RuntimePlatform.Android ||
		       Application.platform == RuntimePlatform.IPhonePlayer;
	}

	void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}

		Instance = this;
		DontDestroyOnLoad(gameObject);
		InitializeIap();
	}

	void OnDestroy()
	{
		if (_storeController != null)
		{
			_storeController.OnPurchasePending -= OnPurchasePending;
			_storeController.OnPurchaseConfirmed -= OnPurchaseConfirmed;
			_storeController.OnPurchaseFailed -= OnPurchaseFailed;
			_storeController.OnPurchaseDeferred -= OnPurchaseDeferred;
			_storeController.OnStoreConnected -= OnStoreConnected;
			_storeController.OnStoreDisconnected -= OnStoreDisconnected;
			_storeController.OnProductsFetched -= OnProductsFetched;
			_storeController.OnProductsFetchFailed -= OnProductsFetchFailed;
			_storeController.OnPurchasesFetched -= OnPurchasesFetched;
			_storeController.OnPurchasesFetchFailed -= OnPurchasesFetchFailed;
		}

		if (Instance == this)
			Instance = null;
	}

	async void InitializeIap()
	{
		if (!IsNativeStoreRuntimeSupported())
			return;

		if (_initializing || _storeController != null)
			return;

		_initializing = true;
		try
		{
			_storeController = UnityIAPServices.StoreController();
			_storeController.OnPurchasePending += OnPurchasePending;
			_storeController.OnPurchaseConfirmed += OnPurchaseConfirmed;
			_storeController.OnPurchaseFailed += OnPurchaseFailed;
			_storeController.OnPurchaseDeferred += OnPurchaseDeferred;
			_storeController.OnStoreConnected += OnStoreConnected;
			_storeController.OnStoreDisconnected += OnStoreDisconnected;
			_storeController.OnProductsFetched += OnProductsFetched;
			_storeController.OnProductsFetchFailed += OnProductsFetchFailed;
			_storeController.OnPurchasesFetched += OnPurchasesFetched;
			_storeController.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
			_storeController.ProcessPendingOrdersOnPurchasesFetched(true);

			await _storeController.Connect();
		}
		catch (Exception exception)
		{
			Debug.LogWarning("[IAP] Store initialization failed: " + exception.Message, this);
		}
		finally
		{
			_initializing = false;
		}
	}

	public void ConfigureProducts(IEnumerable<ShopItemData> items, bool fetchStoreProducts = true)
	{
		if (items == null)
			return;

		bool changed = false;
		foreach (var item in items)
		{
			if (item == null)
				continue;

			string productId = SaveDataSanitizer.SanitizeIdentifier(item.productId);
			if (string.IsNullOrEmpty(productId))
				continue;

			item.productId = productId;
			if (!_catalogByProductId.ContainsKey(productId))
				changed = true;
			_catalogByProductId[productId] = item;
		}

		if (changed)
			_productsFetched = false;

		if (fetchStoreProducts)
			FetchStoreProductsIfReady();
	}

	public IEnumerator RefreshProductsFromServer()
	{
		if (_fetchingServerProducts || NetworkManager.Instance == null || !NetworkManager.IsAuthenticated)
			yield break;

		_fetchingServerProducts = true;
		List<ShopItemData> remoteItems = null;
		string error = null;
		yield return NetworkManager.Instance.FetchPurchaseProducts((json, err) =>
		{
			error = err;
			remoteItems = string.IsNullOrEmpty(err) ? ShopController.ParseRemoteShopItems(json) : null;
		});
		_fetchingServerProducts = false;

		if (!string.IsNullOrEmpty(error))
		{
			Debug.LogWarning("[IAP] Failed to load purchase products: " + error, this);
			yield break;
		}

		if (remoteItems != null && remoteItems.Count > 0)
			ConfigureProducts(remoteItems);
	}

	public bool TryGetLocalizedPrice(string productId, out string localizedPrice)
	{
		localizedPrice = "";
		productId = SaveDataSanitizer.SanitizeIdentifier(productId);
		if (string.IsNullOrEmpty(productId) || _storeController == null)
			return false;

		Product product = _storeController.GetProductById(productId);
		if (product == null || product.metadata == null || string.IsNullOrWhiteSpace(product.metadata.localizedPriceString))
			return false;

		localizedPrice = product.metadata.localizedPriceString;
		return true;
	}

	public bool CanPurchase(ShopItemData item)
	{
		string productId = SaveDataSanitizer.SanitizeIdentifier(item != null ? item.productId : "");
		if (string.IsNullOrEmpty(productId) || !IsReady || _storeController == null)
			return false;

		Product product = _storeController.GetProductById(productId);
		return product != null && product.availableToPurchase;
	}

	public void Purchase(ShopItemData item, Action<bool, string> callback)
	{
		string productId = SaveDataSanitizer.SanitizeIdentifier(item != null ? item.productId : "");
		if (string.IsNullOrEmpty(productId))
		{
			callback?.Invoke(false, "Товар временно недоступен");
			return;
		}

		if (NetworkManager.Instance == null || !NetworkManager.IsAuthenticated)
		{
			callback?.Invoke(false, "Войдите в аккаунт для покупки");
			return;
		}

		ConfigureProducts(new[] { item });
		if (!IsReady || _storeController == null)
		{
			InitializeIap();
			FetchStoreProductsIfReady();
			callback?.Invoke(false, "Магазин ещё загружается");
			return;
		}

		Product product = _storeController.GetProductById(productId);
		if (product == null || !product.availableToPurchase)
		{
			callback?.Invoke(false, "Товар недоступен в магазине");
			return;
		}

		_purchaseCallbacksByProductId[productId] = callback;
		try
		{
			_storeController.PurchaseProduct(product);
		}
		catch (Exception exception)
		{
			_purchaseCallbacksByProductId.Remove(productId);
			Debug.LogWarning("[IAP] Purchase start failed: " + exception.Message, this);
			callback?.Invoke(false, "Не удалось открыть покупку");
		}
	}

	public void Restore(Action<bool, string> callback)
	{
		if (NetworkManager.Instance == null || !NetworkManager.IsAuthenticated)
		{
			callback?.Invoke(false, "Войдите в аккаунт для восстановления покупок");
			return;
		}

		if (_storeController == null || !_connected)
		{
			InitializeIap();
			callback?.Invoke(false, "Магазин ещё загружается");
			return;
		}

		try
		{
			_storeController.RestoreTransactions((ok, error) =>
			{
				if (!ok)
				{
					callback?.Invoke(false, string.IsNullOrEmpty(error) ? "Восстановление не удалось" : error);
					return;
				}

				StartCoroutine(ConfirmRestoreOnServer(callback));
			});
		}
		catch (Exception exception)
		{
			Debug.LogWarning("[IAP] Restore failed: " + exception.Message, this);
			callback?.Invoke(false, "Восстановление сейчас недоступно");
		}
	}

	IEnumerator ConfirmRestoreOnServer(Action<bool, string> callback)
	{
		bool ok = false;
		string payload = null;
		yield return NetworkManager.Instance.RestorePurchases(GetStoreName(), null, (success, response) =>
		{
			ok = success;
			payload = response;
		});

		callback?.Invoke(ok, ok ? "Покупки восстановлены" : ExtractApiError(payload, "Восстановление отклонено сервером"));
	}

	void FetchStoreProductsIfReady()
	{
		if (!_connected || _storeController == null || _catalogByProductId.Count == 0)
			return;

		var definitions = _catalogByProductId.Values
			.Take(MaxCatalogProducts)
			.Where(item => item != null && !string.IsNullOrEmpty(item.productId))
			.Select(item => new ProductDefinition(item.productId, item.productType))
			.ToList();

		if (definitions.Count == 0)
			return;

		try
		{
			_storeController.FetchProducts(definitions);
		}
		catch (Exception exception)
		{
			Debug.LogWarning("[IAP] Products fetch failed to start: " + exception.Message, this);
		}
	}

	void FetchExistingPurchases()
	{
		if (_storeController == null || !_connected)
			return;

		try
		{
			_storeController.FetchPurchases();
		}
		catch (Exception exception)
		{
			Debug.LogWarning("[IAP] Purchases fetch failed to start: " + exception.Message, this);
		}
	}

	void OnStoreConnected()
	{
		_connected = true;
		FetchStoreProductsIfReady();
		FetchExistingPurchases();
	}

	void OnStoreDisconnected(StoreConnectionFailureDescription description)
	{
		_connected = false;
		_productsFetched = false;
		Debug.LogWarning("[IAP] Store disconnected: " + (description != null ? description.message : "unknown"), this);
	}

	void OnProductsFetched(List<Product> products)
	{
		_productsFetched = true;
		ProductsUpdated?.Invoke();
		Debug.Log("[IAP] Products fetched: " + (products != null ? products.Count : 0), this);
	}

	void OnProductsFetchFailed(ProductFetchFailed failure)
	{
		_productsFetched = false;
		string reason = failure != null ? failure.FailureReason : "unknown";
		Debug.LogWarning("[IAP] Products fetch failed: " + reason, this);
		ProductsUpdated?.Invoke();
	}

	void OnPurchasesFetched(Orders orders)
	{
		Debug.Log("[IAP] Existing purchases fetched.", this);
	}

	void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
	{
		Debug.LogWarning("[IAP] Existing purchases fetch failed: " + (failure != null ? failure.Message : "unknown"), this);
	}

	void OnPurchasePending(PendingOrder order)
	{
		Product product = GetFirstProductInOrder(order);
		string productId = GetProductId(product);
		if (string.IsNullOrEmpty(productId))
		{
			Debug.LogWarning("[IAP] Pending purchase without product id.", this);
			return;
		}

		StartCoroutine(ConfirmPendingOrderOnServer(order, productId));
	}

	IEnumerator ConfirmPendingOrderOnServer(PendingOrder order, string productId)
	{
		string receipt = order != null && order.Info != null ? order.Info.Receipt : "";
		string transactionId = order != null && order.Info != null ? order.Info.TransactionID : "";
		string transactionKey = string.IsNullOrEmpty(transactionId)
			? productId + ":" + StableHash(receipt)
			: transactionId;

		if (string.IsNullOrWhiteSpace(receipt))
		{
			NotifyPurchase(productId, false, "Чек покупки пустой");
			yield break;
		}

		if (NetworkManager.Instance == null || !NetworkManager.IsAuthenticated)
		{
			NotifyPurchase(productId, false, "Нет авторизации для подтверждения покупки");
			yield break;
		}

		if (_confirmingTransactions.Contains(transactionKey))
			yield break;

		_confirmingTransactions.Add(transactionKey);

		bool ok = false;
		string payload = null;
		yield return NetworkManager.Instance.ConfirmPurchase(
			ResolveStoreName(receipt),
			productId,
			transactionId,
			receipt,
			null,
			(success, response) =>
			{
				ok = success;
				payload = response;
			});

		_confirmingTransactions.Remove(transactionKey);

		if (!ok)
		{
			Debug.LogWarning("[IAP] Server rejected purchase " + productId + ": " + ExtractApiError(payload, "unknown"), this);
			NotifyPurchase(productId, false, ExtractApiError(payload, "Покупка отклонена сервером"));
			yield break;
		}

		try
		{
			_storeController.ConfirmPurchase(order);
		}
		catch (Exception exception)
		{
			Debug.LogWarning("[IAP] Store confirmation failed after server validation: " + exception.Message, this);
		}

		yield return NetworkManager.Instance.SyncBalance(_ => { });
		NotifyPurchase(productId, true, "Покупка завершена");
	}

	void OnPurchaseConfirmed(Order order)
	{
		Product product = GetFirstProductInOrder(order);
		string productId = GetProductId(product);
		if (order is FailedOrder failedOrder)
		{
			Debug.LogWarning("[IAP] Store confirmation failed for " + productId + ": " + failedOrder.Details, this);
			return;
		}

		Debug.Log("[IAP] Store confirmed purchase: " + productId, this);
	}

	void OnPurchaseFailed(FailedOrder order)
	{
		Product product = GetFirstProductInOrder(order);
		string productId = GetProductId(product);
		string details = order != null ? order.Details : "";
		Debug.LogWarning("[IAP] Purchase failed for " + productId + ": " + details, this);
		NotifyPurchase(productId, false, "Покупка отменена или не прошла");
	}

	void OnPurchaseDeferred(DeferredOrder order)
	{
		Product product = GetFirstProductInOrder(order);
		string productId = GetProductId(product);
		Debug.Log("[IAP] Purchase deferred: " + productId, this);
		NotifyPurchase(productId, false, "Покупка ожидает подтверждения магазина");
	}

	static Product GetFirstProductInOrder(Order order)
	{
		if (order == null || order.CartOrdered == null)
			return null;

		return order.CartOrdered.Items().FirstOrDefault()?.Product;
	}

	static string GetProductId(Product product)
	{
		return SaveDataSanitizer.SanitizeIdentifier(product != null && product.definition != null
			? product.definition.id
			: "");
	}

	void NotifyPurchase(string productId, bool ok, string message)
	{
		productId = SaveDataSanitizer.SanitizeIdentifier(productId);
		if (string.IsNullOrEmpty(productId))
			return;

		if (!_purchaseCallbacksByProductId.TryGetValue(productId, out var callback))
			return;

		_purchaseCallbacksByProductId.Remove(productId);
		callback?.Invoke(ok, message);
	}

	static string ExtractApiError(string payload, string fallback)
	{
		string error = NetworkJson.GetString(payload, "error");
		if (string.IsNullOrWhiteSpace(error))
			error = NetworkJson.GetString(payload, "message");

		error = SaveDataSanitizer.SanitizeContentText(error);
		if (string.IsNullOrEmpty(error))
			return fallback;

		return error.Length <= 96 ? error : error.Substring(0, 96);
	}

	static string StableHash(string value)
	{
		if (string.IsNullOrEmpty(value))
			return "empty";

		unchecked
		{
			uint hash = 2166136261;
			for (int i = 0; i < value.Length; i++)
				hash = (hash ^ value[i]) * 16777619;
			return hash.ToString("x8");
		}
	}

	static string GetStoreName()
	{
#if UNITY_ANDROID
		return GooglePlay.Name;
#elif UNITY_IOS
        return AppleAppStore.Name;
#elif UNITY_STANDALONE_OSX
        return MacAppStore.Name;
#else
        return Application.platform.ToString();
#endif
	}

	static string ResolveStoreName(string receipt)
	{
		string receiptStore = SaveDataSanitizer.SanitizeIdentifier(NetworkJson.GetString(receipt, "Store"));
		return string.IsNullOrEmpty(receiptStore) ? GetStoreName() : receiptStore;
	}
}
