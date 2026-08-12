using System;
using System.Collections;
using UnityEngine;

public sealed partial class NetworkManager
{
	private const string PlayerPromoApplyPath =
		"/player/promo/apply";

	public void ApplyPromocodeAsync(
		string code,
		Action<bool, string> callback = null)
	{
		code = (code ?? "").Trim();

		if (string.IsNullOrEmpty(code))
		{
			callback?.Invoke(false, "empty_code");
			return;
		}

		Debug.Log(
			$"[PROMO DEBUG] " +
			$"Promo={BuildUrl(PlayerPromoApplyPath)} | " +
			$"Balance={BuildUrl(ApiRoutes.PlayerBalance)}");

		StartCoroutine(
			ApplyPromocodeCoroutine(code, callback));
	}

	private IEnumerator ApplyPromocodeCoroutine(
		string code,
		Action<bool, string> callback)
	{
		if (!IsAuthenticated)
		{
			callback?.Invoke(
				false,
				"not_authenticated");

			yield break;
		}

		var body = new PromoApplyRequest
		{
			code = code
		};

		yield return PostRawInternalResult(
			PlayerPromoApplyPath,
			NetworkJson.ToJson(body),
			_authToken,
			result =>
			{
				if (result == null)
				{
					callback?.Invoke(
						false,
						"no_response");

					return;
				}

				callback?.Invoke(
					result.IsSuccess,
					result.IsSuccess
						? result.Text
						: result.Error);
			});
	}

	[Serializable]
	private sealed class PromoApplyRequest
	{
		public string code;
	}
}