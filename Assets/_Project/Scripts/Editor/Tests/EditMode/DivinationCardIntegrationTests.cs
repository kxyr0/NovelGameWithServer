using NUnit.Framework;

public sealed class DivinationCardIntegrationTests
{
    [Test]
    public void ParseDrawResponse_ReadsDocumentedTarotCardShape()
    {
        const string json =
            "{\"ok\":true,\"card\":{\"id\":\"veil\",\"name\":\"\\u0412\\u0443\\u0430\\u043b\\u044c\",\"description\":\"hidden meaning\",\"resultText\":\"legacy result text\",\"imageUrl\":\"/files/tarot/veil.png\",\"weight\":1.5,\"isPublished\":true,\"reward\":{\"hearts\":2}},\"hearts\":10}";

        DivinationTarotDrawResponseDto response = DivinationBackendJsonParser.ParseDrawResponse(json);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.SelectedCard, Is.Not.Null);
        Assert.That(response.SelectedCard.EffectiveId, Is.EqualTo("veil"));
        Assert.That(response.SelectedCard.EffectiveTitle, Is.EqualTo("\u0412\u0443\u0430\u043b\u044c"));
        Assert.That(response.SelectedCard.description, Is.EqualTo("hidden meaning"));
        Assert.That(response.SelectedCard.resultText, Is.EqualTo("legacy result text"));
        Assert.That(response.SelectedCard.EffectiveDescription, Is.EqualTo("hidden meaning"));
        Assert.That(response.SelectedCard.EffectiveImageUrl, Is.EqualTo("/files/tarot/veil.png"));
        Assert.That(response.SelectedCard.weight, Is.EqualTo(1.5f).Within(0.001f));
        Assert.That(response.rewards, Has.Length.EqualTo(1));
        Assert.That(response.rewards[0].hearts, Is.EqualTo(2));
    }

    [Test]
    public void ParseDrawResponse_UsesDescriptionForVisibleCardTextAndIgnoresResultText()
    {
        const string json =
            "{\"ok\":true,\"canDraw\":true,\"card\":{\"id\":\"veil\",\"name\":\"Вуаль\",\"description\":\"Сегодня многое останется за завесой.\",\"resultText\":\"Не показывать это поле\",\"active\":true,\"weight\":1.0}}";

        DivinationTarotDrawResponseDto response = DivinationBackendJsonParser.ParseDrawResponse(json);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.SelectedCard, Is.Not.Null);
        Assert.That(response.SelectedCard.description, Is.EqualTo("Сегодня многое останется за завесой."));
        Assert.That(response.SelectedCard.resultText, Is.EqualTo("Не показывать это поле"));
        Assert.That(response.SelectedCard.EffectiveDescription, Is.EqualTo("Сегодня многое останется за завесой."));
        Assert.That(response.SelectedCard.EffectiveDescription, Is.Not.EqualTo(response.SelectedCard.resultText));
    }

    [Test]
    public void ParseDrawResponse_ReadsExtendedCardTextCooldownAndMultipleRewards()
    {
        const string json =
            "{\"data\":{\"canDraw\":false,\"card\":{\"cardId\":\"choice\",\"title\":\"Choice\",\"description\":\"backend description\"},\"rewards\":[{\"type\":\"currency\",\"currency\":\"hearts\",\"amount\":3,\"displayName\":\"hearts\"},{\"type\":\"item\",\"itemId\":\"premium_key\",\"count\":1,\"displayName\":\"premium_key\"}],\"cooldown\":{\"available\":false,\"nextAvailableAtUtc\":\"2026-07-12T12:00:00Z\",\"remainingSeconds\":3600}}}";

        DivinationTarotDrawResponseDto response = DivinationBackendJsonParser.ParseDrawResponse(json);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.SelectedCard.EffectiveId, Is.EqualTo("choice"));
        Assert.That(response.SelectedCard.description, Is.EqualTo("backend description"));
        Assert.That(response.IsDrawAvailable(true), Is.False);
        Assert.That(response.cooldown.remainingSeconds, Is.EqualTo(3600));
        Assert.That(response.rewards, Has.Length.EqualTo(2));

        string formatted = DivinationRewardDisplayFormatter.FormatRewards(response.rewards);
        Assert.That(formatted, Does.Contain("hearts +3"));
        Assert.That(formatted, Does.Contain("premium_key +1"));
    }

    [Test]
    public void ParseStatusResponse_UsesCanDrawAndNextDrawAt()
    {
        const string json = "{\"canDraw\":false,\"lastDrawAt\":\"2026-07-05T12:00:00Z\",\"nextDrawAt\":\"2026-07-12T12:00:00Z\",\"remainingSeconds\":604800}";

        DivinationTarotStatusResponseDto status = DivinationBackendJsonParser.ParseStatusResponse(json);

        Assert.That(status, Is.Not.Null);
        Assert.That(status.IsDrawAvailable(true), Is.False);
        Assert.That(status.cooldown.nextDrawAt, Is.EqualTo("2026-07-12T12:00:00Z"));
        Assert.That(DivinationCooldownFormatter.FormatDuration(604800), Is.EqualTo("7d 0h"));
    }

    [Test]
    public void RewardFormatter_UnknownRewardTypeDoesNotCrash()
    {
        var rewards = new[]
        {
            new DivinationRewardDto
            {
                type = "future_reward",
                id = "mystery",
                amount = 1
            }
        };

        string formatted = DivinationRewardDisplayFormatter.FormatRewards(rewards);

        Assert.That(formatted, Is.EqualTo("mystery +1"));
    }

    [Test]
    public void CardIdNormalize_IsCaseInsensitiveAndTrimmed()
    {
        Assert.That(DivinationCardIdUtility.Normalize(" Veil "), Is.EqualTo("veil"));
    }
}
