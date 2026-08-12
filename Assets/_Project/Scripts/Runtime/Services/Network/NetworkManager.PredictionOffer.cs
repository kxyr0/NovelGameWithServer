using System;
using System.Collections;

public sealed partial class NetworkManager
{
    public const string MainMenuPredictionScreenId = "MainScreen";
    public const string MainMenuPredictionEnabledTextId = "prediction_offer_enabled";
    public const string MainMenuPredictionCardIdTextId = "prediction_offer_card_id";
    public const string MainMenuPredictionTitleTextId = "prediction_offer_title";
    public const string MainMenuPredictionDescriptionTextId = "prediction_offer_description";
    public const string MainMenuPredictionImageUrlTextId = "prediction_offer_image_url";

    public IEnumerator FetchMainMenuPredictionOffer(
        Action<MainMenuPredictionOfferContent, string> callback,
        bool force = false)
    {
        bool refreshed = false;
        string refreshError = "";
        string locale = ResolveUiTextLocale();

        yield return RefreshUiTexts(
            MainMenuPredictionScreenId,
            "",
            locale,
            (ok, error) =>
            {
                refreshed = ok;
                refreshError = error ?? "";
            },
            force);

        if (!refreshed)
        {
            callback?.Invoke(null, refreshError);
            yield break;
        }

        if (!TryGetMainMenuPredictionOffer(locale, out MainMenuPredictionOfferContent content))
        {
            callback?.Invoke(null, "Main menu prediction offer is disabled or incomplete.");
            yield break;
        }

        callback?.Invoke(content, "");
    }

    public static bool TryGetMainMenuPredictionOffer(
        string locale,
        out MainMenuPredictionOfferContent content)
    {
        content = null;
        locale = ResolveUiTextLocale(locale);

        if (!TryGetUiText(
                MainMenuPredictionEnabledTextId,
                MainMenuPredictionScreenId,
                "",
                locale,
                out string enabledText) ||
            !TryParsePredictionOfferEnabled(enabledText))
        {
            return false;
        }

        if (!TryGetUiText(
                MainMenuPredictionTitleTextId,
                MainMenuPredictionScreenId,
                "",
                locale,
                out string title) ||
            !TryGetUiText(
                MainMenuPredictionDescriptionTextId,
                MainMenuPredictionScreenId,
                "",
                locale,
                out string description))
        {
            return false;
        }

        TryGetUiText(
            MainMenuPredictionCardIdTextId,
            MainMenuPredictionScreenId,
            "",
            locale,
            out string cardId);
        TryGetUiText(
            MainMenuPredictionImageUrlTextId,
            MainMenuPredictionScreenId,
            "",
            locale,
            out string imageUrl);

        content = new MainMenuPredictionOfferContent
        {
            CardId = SaveDataSanitizer.SanitizeIdentifier(cardId),
            Title = title ?? "",
            Description = description ?? "",
            ImageUrl = SanitizePredictionImageUrl(imageUrl)
        };
        return content.IsValid;
    }

    private static bool TryParsePredictionOfferEnabled(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        switch (value.Trim().ToLowerInvariant())
        {
            case "1":
            case "true":
            case "yes":
            case "on":
            case "enabled":
                return true;
            default:
                return false;
        }
    }

    private static string SanitizePredictionImageUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        string candidate = value.Trim();
        if (candidate.Length > 2048)
            return "";

        if (candidate.StartsWith("/", StringComparison.Ordinal))
            return candidate;

        if (Uri.TryCreate(candidate, UriKind.Absolute, out Uri uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
        {
            return candidate;
        }

        return "";
    }
}

[Serializable]
public sealed class MainMenuPredictionOfferContent
{
    public string CardId = "";
    public string Title = "";
    public string Description = "";
    public string ImageUrl = "";

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Title) &&
        !string.IsNullOrWhiteSpace(Description);
}
