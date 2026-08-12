using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Nocturne/UI/Divination API Client")]
public sealed class DivinationApiClient : MonoBehaviour
{
    private const string LogPrefix = "[Divination]";

    [SerializeField]
    [Tooltip("Требовать авторизованную сессию NetworkManager перед запросами к /player/tarot/status и /player/tarot/draw.")]
    private bool _requireAuthenticatedSession = true;

    public IEnumerator FetchStatus(Action<DivinationTarotStatusResponseDto, string> callback)
    {
        if (!IsNetworkReady(out string readinessError))
        {
            callback?.Invoke(null, readinessError);
            yield break;
        }

        string payload = null;
        string error = null;
        yield return NetworkManager.Instance.FetchTarotStatus((json, err) =>
        {
            payload = json;
            error = err;
        });

        if (!string.IsNullOrWhiteSpace(error))
        {
            Debug.LogWarning(LogPrefix + " tarot status request failed: " + error, this);
            callback?.Invoke(null, error);
            yield break;
        }

        DivinationTarotStatusResponseDto status = DivinationBackendJsonParser.ParseStatusResponse(payload);
        callback?.Invoke(status, status == null ? "Tarot status parse failed." : "");
    }

    public IEnumerator DrawCard(Action<bool, DivinationTarotDrawResponseDto, string> callback)
    {
        if (!IsNetworkReady(out string readinessError))
        {
            callback?.Invoke(false, null, readinessError);
            yield break;
        }

        string payload = null;
        string error = null;
        yield return NetworkManager.Instance.DrawTarot((ok, json) =>
        {
            if (ok)
                payload = json;
            else
                error = json;
        });

        if (!string.IsNullOrWhiteSpace(error))
        {
            Debug.LogWarning(LogPrefix + " tarot draw request failed: " + error, this);
            callback?.Invoke(false, null, error);
            yield break;
        }

        DivinationTarotDrawResponseDto response = DivinationBackendJsonParser.ParseDrawResponse(payload);
        bool okResponse = response != null && (!response.hasOkValue || response.ok);
        callback?.Invoke(okResponse, response, okResponse ? "" : "Tarot draw parse failed.");
    }

    private bool IsNetworkReady(out string error)
    {
        if (NetworkManager.Instance == null)
        {
            error = "NetworkManager is missing.";
            return false;
        }

        if (_requireAuthenticatedSession && !NetworkManager.IsAuthenticated)
        {
            error = "Network session is not authenticated.";
            return false;
        }

        error = "";
        return true;
    }
}
