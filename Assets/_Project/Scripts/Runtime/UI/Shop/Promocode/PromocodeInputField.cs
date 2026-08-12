using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Shop/Promocode InputField")]
public sealed class PromocodeInputField : MonoBehaviour
{
	[SerializeField] private Color endColor;
	[SerializeField] private Image buttonImage;

	[SerializeField] private Button applyButton;
	[SerializeField] private TMP_InputField inputField;

	[SerializeField] private CanvasGroup buttonGroup;
	[SerializeField] private CanvasGroup successGroup;
	[SerializeField, Min(0f)] private float successDuration = 2f;

	private Color _startColor;

	private void Awake()
	{
		_startColor = buttonImage.color;

		inputField.onValueChanged.AddListener(CheckSymbols);
		applyButton.onClick.AddListener(ApplyPromocode);

		CheckSymbols(inputField.text);

		SetGroupVisible(successGroup, false);
		SetGroupVisible(buttonGroup, true);
	}

	private void OnDestroy()
	{
		inputField.onValueChanged.RemoveListener(CheckSymbols);
		applyButton.onClick.RemoveListener(ApplyPromocode);
	}

	private void CheckSymbols(string value)
	{
		bool hasCode =
			!string.IsNullOrWhiteSpace(value);

		buttonImage.color =
			hasCode ? endColor : _startColor;

		applyButton.interactable = hasCode;
	}

	private void ApplyPromocode()
	{
		if (NetworkManager.Instance == null)
		{
			Debug.LogError(
				"Cannot apply promocode: NetworkManager is missing.",
				this);

			return;
		}

		string code = inputField.text.Trim();

		applyButton.interactable = false;

		NetworkManager.Instance.ApplyPromocodeAsync(
			code,
			(success, response) =>
			{
				Debug.Log(
				$"[PROMO] success={success}, response={response}",
				this);

				if (success)
				{
					ShowSuccess();
					return;
				}

				applyButton.interactable =
					!string.IsNullOrWhiteSpace(inputField.text);
			});
	}
	private void ShowSuccess()
	{
		SetGroupVisible(buttonGroup, false);
		SetGroupVisible(successGroup, true);

		CancelInvoke(nameof(HideSuccess));
		Invoke(nameof(HideSuccess), successDuration);
	}

	private void HideSuccess()
	{
		SetGroupVisible(successGroup, false);
		SetGroupVisible(buttonGroup, true);

		CheckSymbols(inputField.text);
	}

	private static void SetGroupVisible(
		CanvasGroup group,
		bool visible)
	{
		if (group == null)
			return;

		group.alpha = visible ? 1f : 0f;
		group.interactable = visible;
		group.blocksRaycasts = visible;
	}
}