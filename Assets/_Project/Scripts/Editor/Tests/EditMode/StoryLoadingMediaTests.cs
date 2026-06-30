using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class StoryLoadingMediaTests
{
    [Test]
    public void CustomAssetReferenceTypes_AreSerializable()
    {
        Assert.That(
            typeof(AssetReferenceVideoClip).IsDefined(typeof(SerializableAttribute), false),
            Is.True);
        Assert.That(
            typeof(AssetReferenceTextAsset).IsDefined(typeof(SerializableAttribute), false),
            Is.True);
    }

    [Test]
    public void GameData_LoadingMedia_InitializesConcreteReferences()
    {
        var data = ScriptableObject.CreateInstance<GameData>();
        try
        {
            GameStoryLoadingMediaSettings loadingMedia = data.LoadingMedia;

            Assert.That(loadingMedia, Is.Not.Null);
            Assert.That(loadingMedia.ImageReference, Is.Not.Null);
            Assert.That(loadingMedia.VideoReference, Is.Not.Null);
            Assert.That(loadingMedia.GifReference, Is.Not.Null);
            Assert.That(loadingMedia.VideoReference, Is.TypeOf<AssetReferenceVideoClip>());
            Assert.That(loadingMedia.GifReference, Is.TypeOf<AssetReferenceTextAsset>());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void StoryLoadingMediaService_CanceledBeforeStart_ReturnsEmptyLease()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        StoryLoadingMediaLease lease = new StoryLoadingMediaService()
            .LoadAsync(null, cancellation.Token)
            .GetAwaiter()
            .GetResult();

        try
        {
            Assert.That(lease, Is.Not.Null);
            Assert.That(lease.HasAnyMedia, Is.False);
            Assert.That(lease.HasAddressableHandles, Is.False);
        }
        finally
        {
            lease?.Dispose();
        }
    }

    [Test]
    public void StoryLoadingMediaService_AddressableCustomMedia_UsesInjectedAssetLoader()
    {
        var data = ScriptableObject.CreateInstance<GameData>();
        Texture2D texture = null;
        Sprite sprite = null;
        StoryLoadingMediaLease lease = null;

        try
        {
            sprite = CreateSprite(ref texture);
            GameStoryLoadingMediaSettings settings = data.LoadingMedia;
            SetPrivateField(settings, "_overrideLoadingMedia", true);
            SetPrivateField(settings, "_imageReference", new AssetReferenceSprite("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));

            var assetLoader = new RecordingLoadingMediaAssetLoader(sprite);
            lease = new StoryLoadingMediaService(new StoryLoadingMediaPolicy(), assetLoader)
                .LoadAsync(data, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(assetLoader.ValidReferenceCalls, Is.EqualTo(1));
            Assert.That(lease.CoverSprite, Is.SameAs(sprite));
            Assert.That(lease.UsesCustomMedia, Is.True);
            Assert.That(lease.UsesMenuFallback, Is.False);
        }
        finally
        {
            lease?.Dispose();
            if (sprite != null)
                UnityEngine.Object.DestroyImmediate(sprite);
            if (texture != null)
                UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void StoryLoadingMediaLease_Dispose_ReleasesTrackedHandlesOnce()
    {
        var data = ScriptableObject.CreateInstance<GameData>();
        var trackedHandle = new RecordingTrackedHandle();
        Texture2D texture = null;
        Sprite sprite = null;
        StoryLoadingMediaLease lease = null;

        try
        {
            sprite = CreateSprite(ref texture);
            GameStoryLoadingMediaSettings settings = data.LoadingMedia;
            SetPrivateField(settings, "_overrideLoadingMedia", true);
            SetPrivateField(settings, "_imageReference", new AssetReferenceSprite("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));

            var assetLoader = new RecordingLoadingMediaAssetLoader(sprite, trackedHandle);
            lease = new StoryLoadingMediaService(new StoryLoadingMediaPolicy(), assetLoader)
                .LoadAsync(data, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(lease.HasAddressableHandles, Is.True);

            lease.Dispose();
            lease.Dispose();

            Assert.That(trackedHandle.DisposeCount, Is.EqualTo(1));
            Assert.That(lease.HasAddressableHandles, Is.False);

            var lateHandle = new RecordingTrackedHandle();
            lease.RegisterOwnedResource(lateHandle);

            Assert.That(lateHandle.DisposeCount, Is.EqualTo(1));
            Assert.That(lease.HasAddressableHandles, Is.False);
        }
        finally
        {
            if (sprite != null)
                UnityEngine.Object.DestroyImmediate(sprite);
            if (texture != null)
                UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void StoryLoadingMediaService_CanceledAddressableLoad_DisposesOwnedHandles()
    {
        var data = ScriptableObject.CreateInstance<GameData>();
        var trackedHandle = new RecordingTrackedHandle();

        try
        {
            GameStoryLoadingMediaSettings settings = data.LoadingMedia;
            SetPrivateField(settings, "_overrideLoadingMedia", true);
            SetPrivateField(settings, "_imageReference", new AssetReferenceSprite("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));

            var assetLoader = new CancelingLoadingMediaAssetLoader(trackedHandle);
            Assert.Throws<OperationCanceledException>(() =>
                new StoryLoadingMediaService(new StoryLoadingMediaPolicy(), assetLoader)
                    .LoadAsync(data, CancellationToken.None)
                    .GetAwaiter()
                    .GetResult());

            Assert.That(assetLoader.ValidReferenceCalls, Is.EqualTo(1));
            Assert.That(trackedHandle.DisposeCount, Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void StoryStartAssetPreloadService_CanceledBeforeStart_Cancels()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var request = new StoryStartAssetPreloadRequest(
            new StoryStartPreloadAssetSet(),
            StoryLoadingMediaSelection.Empty(null),
            Array.Empty<string>(),
            preloadCoverTexture: true,
            preloadStoryTextures: true,
            preloadAudioData: true,
            waitForTextureStreaming: true,
            useUnscaledTime: true,
            textureStreamingTimeout: 0.1f,
            assetsPerFrame: 1,
            asyncOperationProgressScale: 1f,
            coverStatus: "",
            textureStatus: "",
            audioStatus: "",
            videoStatus: "",
            dataStatus: "",
            resourcesStatus: "");

        Assert.Throws<OperationCanceledException>(() =>
            new StoryStartAssetPreloadService()
                .PreloadAsync(request, null, cancellation.Token)
                .GetAwaiter()
                .GetResult());
    }

    [Test]
    public void StoryStartPreloadAssetCollector_CoverOnly_CollectsLegacyCover()
    {
        var data = ScriptableObject.CreateInstance<GameData>();
        Texture2D texture = null;
        Sprite sprite = null;

        try
        {
            sprite = CreateSprite(ref texture);
            SetPrivateField(data, "_gameIcon", sprite);

            StoryStartPreloadAssetSet assets = new StoryStartPreloadAssetCollector()
                .Collect(data, StoryStartLoadingAssetScope.CoverOnly, null);

            Assert.That(assets.Sprites, Has.Member(sprite));
        }
        finally
        {
            if (sprite != null)
                UnityEngine.Object.DestroyImmediate(sprite);
            if (texture != null)
                UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void StoryStartPreloadAssetCollector_SavedOrFirstChapter_UsesInjectedChapterSelector()
    {
        var data = ScriptableObject.CreateInstance<GameData>();
        var story = ScriptableObject.CreateInstance<StoryData>();
        var firstChapter = ScriptableObject.CreateInstance<ChapterData>();
        var selectedChapter = ScriptableObject.CreateInstance<ChapterData>();
        var firstJson = new TextAsset("first");
        var selectedJson = new TextAsset("selected");

        try
        {
            firstChapter.Configure("first", "First", null, firstJson, false, 0);
            selectedChapter.Configure("selected", "Selected", null, selectedJson, false, 0);
            story.Configure("story", "Story", new[] { firstChapter, selectedChapter });
            SetPrivateField(data, "_story", story);

            var chapterSelector = new FixedStoryStartChapterSelector(selectedChapter);
            StoryStartPreloadAssetSet assets = new StoryStartPreloadAssetCollector(
                    new StoryLoadingMediaPolicy(),
                    chapterSelector)
                .Collect(data, StoryStartLoadingAssetScope.SavedOrFirstChapter, null);

            Assert.That(chapterSelector.Called, Is.True);
            Assert.That(chapterSelector.Story, Is.SameAs(story));
            Assert.That(assets.TextAssets, Has.Member(selectedJson));
            Assert.That(assets.TextAssets, Has.No.Member(firstJson));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(firstJson);
            UnityEngine.Object.DestroyImmediate(selectedJson);
            UnityEngine.Object.DestroyImmediate(firstChapter);
            UnityEngine.Object.DestroyImmediate(selectedChapter);
            UnityEngine.Object.DestroyImmediate(story);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void StoryStartPreloadAssetSet_Add_DeduplicatesAssetsAndIgnoresNulls()
    {
        Texture2D texture = null;
        Sprite sprite = null;

        try
        {
            sprite = CreateSprite(ref texture);
            var assets = new StoryStartPreloadAssetSet();

            assets.Add(sprite);
            assets.Add(sprite);
            assets.Add((Sprite)null);

            Assert.That(assets.TotalCount, Is.EqualTo(1));
            Assert.That(assets.Sprites, Has.Member(sprite));
        }
        finally
        {
            if (sprite != null)
                UnityEngine.Object.DestroyImmediate(sprite);
            if (texture != null)
                UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void StoryStartLoadingProgressModel_TickLoading_UsesReportedProgressAndStatus()
    {
        var model = new StoryStartLoadingProgressModel(
            fakeProgressCeiling: 0.9f,
            fakeProgressDuration: 2f,
            progressCatchUpSpeed: 10f,
            fakeProgressCurve: AnimationCurve.Linear(0f, 0f, 1f, 1f),
            initialStatus: "Initial");

        model.Report(0.5f, "Loading assets");
        StoryStartLoadingProgressSnapshot snapshot = model.TickLoading(elapsed: 0f, deltaTime: 1f);

        Assert.That(snapshot.VisibleProgress, Is.EqualTo(0.45f).Within(0.0001f));
        Assert.That(snapshot.Status, Is.EqualTo("Loading assets"));
        Assert.That(snapshot.Phase, Is.EqualTo(StoryStartLoadingProgressPhase.Loading));
    }

    [Test]
    public void StoryStartLoadingProgressModel_Complete_ReturnsCompleteSnapshot()
    {
        var model = new StoryStartLoadingProgressModel(
            fakeProgressCeiling: 0.9f,
            fakeProgressDuration: 2f,
            progressCatchUpSpeed: 10f,
            fakeProgressCurve: null,
            initialStatus: "Initial");

        StoryStartLoadingProgressSnapshot snapshot = model.Complete("Done");

        Assert.That(snapshot.VisibleProgress, Is.EqualTo(1f));
        Assert.That(snapshot.Status, Is.EqualTo("Done"));
        Assert.That(snapshot.Phase, Is.EqualTo(StoryStartLoadingProgressPhase.Complete));
    }

    [Test]
    public void StoryStartPreloadProgress_Complete_ReportsTypedStage()
    {
        StoryStartPreloadProgress progress = StoryStartPreloadProgress.Complete("Done");

        Assert.That(progress.Stage, Is.EqualTo(StoryStartPreloadStage.Complete));
        Assert.That(progress.NormalizedProgress, Is.EqualTo(1f));
        Assert.That(progress.Status, Is.EqualTo("Done"));
    }

    [Test]
    public void StoryStartPreloadProgressReporter_ForwardsProgressToCallback()
    {
        StoryStartPreloadProgress received = default;
        bool called = false;
        var reporter = new StoryStartPreloadProgressReporter(value =>
        {
            received = value;
            called = true;
        });

        reporter.Report(new StoryStartPreloadProgress(
            1,
            2,
            0.5f,
            "Loading",
            StoryStartPreloadStage.Texture));

        Assert.That(called, Is.True);
        Assert.That(received.Stage, Is.EqualTo(StoryStartPreloadStage.Texture));
        Assert.That(received.NormalizedProgress, Is.EqualTo(0.5f));
        Assert.That(received.Status, Is.EqualTo("Loading"));
    }

    [Test]
    public void StoryStartVideoCoverLayoutPolicy_DefaultStretch_ReturnsStretchedLayout()
    {
        var request = new StoryStartVideoCoverLayoutRequest(
            new StoryStartVideoCoverBaseLayout(
                new Vector2(100f, 200f),
                new Vector2(5f, 6f),
                Vector3.one,
                15f),
            overrides: null,
            stretchByDefault: true,
            defaultStretchScale: new Vector2(1.25f, 0.75f),
            defaultStretchRotationZ: 90f);

        StoryStartVideoCoverLayout layout = new StoryStartVideoCoverLayoutPolicy().Resolve(request);

        Assert.That(layout.Stretch, Is.True);
        Assert.That(layout.Scale, Is.EqualTo(new Vector3(1.25f, 0.75f, 1f)));
        Assert.That(layout.RotationZ, Is.EqualTo(90f));
    }

    [Test]
    public void StoryStartVideoCoverLayoutPolicy_StoryOverrides_ReturnsFramedLayout()
    {
        var overrides = new GameMenuCardOverrideSettings();
        SetPrivateField(overrides, "_overrideVideoSize", true);
        SetPrivateField(overrides, "_videoSize", new Vector2(300f, 400f));
        SetPrivateField(overrides, "_overrideVideoPosition", true);
        SetPrivateField(overrides, "_videoPosition", new Vector2(7f, 8f));
        SetPrivateField(overrides, "_overrideVideoRotation", true);
        SetPrivateField(overrides, "_videoRotationZ", -90f);

        var request = new StoryStartVideoCoverLayoutRequest(
            new StoryStartVideoCoverBaseLayout(
                new Vector2(100f, 200f),
                new Vector2(5f, 6f),
                new Vector3(2f, 2f, 1f),
                15f),
            overrides,
            stretchByDefault: false,
            defaultStretchScale: Vector2.one,
            defaultStretchRotationZ: 0f);

        StoryStartVideoCoverLayout layout = new StoryStartVideoCoverLayoutPolicy().Resolve(request);

        Assert.That(layout.Stretch, Is.False);
        Assert.That(layout.Size, Is.EqualTo(new Vector2(300f, 400f)));
        Assert.That(layout.AnchoredPosition, Is.EqualTo(new Vector2(7f, 8f)));
        Assert.That(layout.Scale, Is.EqualTo(new Vector3(2f, 2f, 1f)));
        Assert.That(layout.RotationZ, Is.EqualTo(-90f));
    }

    [Test]
    public void StoryLoadingMediaPolicy_UsesLegacyCoverWhenCustomMediaIsDisabled()
    {
        var data = ScriptableObject.CreateInstance<GameData>();
        Texture2D texture = null;
        Sprite sprite = null;

        try
        {
            sprite = CreateSprite(ref texture);
            SetPrivateField(data, "_gameIcon", sprite);

            StoryLoadingMediaSelection selection = new StoryLoadingMediaPolicy().SelectInitial(data);

            Assert.That(selection.CoverSprite, Is.SameAs(sprite));
            Assert.That(selection.UsesCustomMedia, Is.False);
            Assert.That(selection.UsesMenuFallback, Is.True);
        }
        finally
        {
            if (sprite != null)
                UnityEngine.Object.DestroyImmediate(sprite);
            if (texture != null)
                UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void StoryLoadingMediaPolicy_CustomFallbackWinsOverLegacyCover()
    {
        var data = ScriptableObject.CreateInstance<GameData>();
        Texture2D legacyTexture = null;
        Texture2D customTexture = null;
        Sprite legacySprite = null;
        Sprite customSprite = null;

        try
        {
            legacySprite = CreateSprite(ref legacyTexture);
            customSprite = CreateSprite(ref customTexture);
            SetPrivateField(data, "_gameIcon", legacySprite);

            GameStoryLoadingMediaSettings settings = data.LoadingMedia;
            SetPrivateField(settings, "_overrideLoadingMedia", true);
            SetPrivateField(settings, "_imageFallback", customSprite);

            StoryLoadingMediaSelection selection = new StoryLoadingMediaPolicy().SelectInitial(data);

            Assert.That(selection.CoverSprite, Is.SameAs(customSprite));
            Assert.That(selection.UsesCustomMedia, Is.True);
            Assert.That(selection.UsesMenuFallback, Is.False);
        }
        finally
        {
            if (legacySprite != null)
                UnityEngine.Object.DestroyImmediate(legacySprite);
            if (customSprite != null)
                UnityEngine.Object.DestroyImmediate(customSprite);
            if (legacyTexture != null)
                UnityEngine.Object.DestroyImmediate(legacyTexture);
            if (customTexture != null)
                UnityEngine.Object.DestroyImmediate(customTexture);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void StoryLoadingMediaReadinessPolicy_LegacyMenuMedia_IsLazyLoadSafe()
    {
        var data = ScriptableObject.CreateInstance<GameData>();
        Texture2D texture = null;
        Sprite sprite = null;

        try
        {
            sprite = CreateSprite(ref texture);
            SetPrivateField(data, "_gameIcon", sprite);

            StoryLoadingMediaReadinessReport report = new StoryLoadingMediaReadinessPolicy()
                .Evaluate(data);

            Assert.That(report.Severity, Is.EqualTo(StoryLoadingMediaReadinessSeverity.Ok));
            Assert.That(report.OverrideEnabled, Is.False);
            Assert.That(report.UsesCustomMedia, Is.False);
            Assert.That(report.IsLazyLoadSafe, Is.True);
        }
        finally
        {
            if (sprite != null)
                UnityEngine.Object.DestroyImmediate(sprite);
            if (texture != null)
                UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void StoryLoadingMediaReadinessPolicy_AddressableCustomMedia_IsLazyLoadSafe()
    {
        var data = ScriptableObject.CreateInstance<GameData>();

        try
        {
            GameStoryLoadingMediaSettings settings = data.LoadingMedia;
            SetPrivateField(settings, "_overrideLoadingMedia", true);
            SetPrivateField(settings, "_imageReference", new AssetReferenceSprite("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));

            StoryLoadingMediaReadinessReport report = new StoryLoadingMediaReadinessPolicy()
                .Evaluate(data);

            Assert.That(report.Severity, Is.EqualTo(StoryLoadingMediaReadinessSeverity.Ok));
            Assert.That(report.OverrideEnabled, Is.True);
            Assert.That(report.HasAddressableMedia, Is.True);
            Assert.That(report.HasDirectMedia, Is.False);
            Assert.That(report.IsLazyLoadSafe, Is.True);
            Assert.That(report.BlocksStrictLazyLoading, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void StoryLoadingMediaReadinessPolicy_DirectCustomFallback_WarnsAboutLazyLoadRisk()
    {
        var data = ScriptableObject.CreateInstance<GameData>();
        Texture2D texture = null;
        Sprite sprite = null;

        try
        {
            sprite = CreateSprite(ref texture);
            GameStoryLoadingMediaSettings settings = data.LoadingMedia;
            SetPrivateField(settings, "_overrideLoadingMedia", true);
            SetPrivateField(settings, "_imageFallback", sprite);

            StoryLoadingMediaReadinessReport report = new StoryLoadingMediaReadinessPolicy()
                .Evaluate(data);

            Assert.That(report.Severity, Is.EqualTo(StoryLoadingMediaReadinessSeverity.Warning));
            Assert.That(report.UsesCustomMedia, Is.True);
            Assert.That(report.HasAddressableMedia, Is.False);
            Assert.That(report.HasDirectMedia, Is.True);
            Assert.That(report.IsLazyLoadSafe, Is.False);
            Assert.That(report.BlocksStrictLazyLoading, Is.True);
            Assert.That(report.Message, Does.Contain("Addressables"));
        }
        finally
        {
            if (sprite != null)
                UnityEngine.Object.DestroyImmediate(sprite);
            if (texture != null)
                UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void GameStoryLoadingMediaSettings_EditorMigrationCandidate_PrefersDirectFallback()
    {
        var data = ScriptableObject.CreateInstance<GameData>();
        Texture2D menuTexture = null;
        Texture2D replacementTexture = null;
        Sprite menuSprite = null;
        Sprite replacementSprite = null;

        try
        {
            menuSprite = CreateSprite(ref menuTexture);
            replacementSprite = CreateSprite(ref replacementTexture);
            GameStoryLoadingMediaSettings settings = data.LoadingMedia;
            SetPrivateField(settings, "_imageFallback", replacementSprite);

            Sprite candidate = settings.ResolveEditorImageCandidate(menuSprite);

            Assert.That(candidate, Is.SameAs(replacementSprite));
        }
        finally
        {
            if (menuSprite != null)
                UnityEngine.Object.DestroyImmediate(menuSprite);
            if (replacementSprite != null)
                UnityEngine.Object.DestroyImmediate(replacementSprite);
            if (menuTexture != null)
                UnityEngine.Object.DestroyImmediate(menuTexture);
            if (replacementTexture != null)
                UnityEngine.Object.DestroyImmediate(replacementTexture);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void GameStoryLoadingMediaSettings_ClearDirectFallback_KeepsUnmigratedMedia()
    {
        var data = ScriptableObject.CreateInstance<GameData>();
        Texture2D texture = null;
        Sprite sprite = null;

        try
        {
            sprite = CreateSprite(ref texture);
            GameStoryLoadingMediaSettings settings = data.LoadingMedia;
            SetPrivateField(settings, "_imageFallback", sprite);

            bool changed = settings.ClearEditorDirectFallbackMediaWithAddressableReferences();

            Assert.That(changed, Is.False);
            Assert.That(settings.ImageFallback, Is.SameAs(sprite));
            Assert.That(settings.HasDirectMedia, Is.True);
        }
        finally
        {
            if (sprite != null)
                UnityEngine.Object.DestroyImmediate(sprite);
            if (texture != null)
                UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void GameStoryLoadingMediaSettings_ClearDirectFallback_RemovesMigratedMedia()
    {
        var data = ScriptableObject.CreateInstance<GameData>();
        Texture2D texture = null;
        Sprite sprite = null;

        try
        {
            sprite = CreateSprite(ref texture);
            GameStoryLoadingMediaSettings settings = data.LoadingMedia;
            SetPrivateField(settings, "_imageFallback", sprite);
            SetPrivateField(settings, "_imageReference", new AssetReferenceSprite("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));

            bool changed = settings.ClearEditorDirectFallbackMediaWithAddressableReferences();

            Assert.That(changed, Is.True);
            Assert.That(settings.ImageFallback, Is.Null);
            Assert.That(settings.HasDirectMedia, Is.False);
        }
        finally
        {
            if (sprite != null)
                UnityEngine.Object.DestroyImmediate(sprite);
            if (texture != null)
                UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void StoryStartLoadingFlow_LoadsMediaBeforeCollectingAndPreloading()
    {
        var data = ScriptableObject.CreateInstance<GameData>();
        Texture2D texture = null;
        Sprite sprite = null;
        StoryStartLoadingFlowResult result = default;

        try
        {
            sprite = CreateSprite(ref texture);
            SetPrivateField(data, "_gameIcon", sprite);

            var loadingMediaService = new RecordingLoadingMediaService();
            var assetCollector = new RecordingPreloadAssetCollector();
            var assetPreloadService = new RecordingAssetPreloadService();
            var observer = new RecordingLoadingFlowObserver();
            var progress = new RecordingPreloadProgress();
            var flow = new StoryStartLoadingFlow(
                loadingMediaService,
                new StoryLoadingMediaPolicy(),
                assetCollector,
                assetPreloadService);

            result = flow.RunAsync(
                    CreateFlowRequest(data),
                    progress,
                    observer,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.That(loadingMediaService.Called, Is.True);
            Assert.That(observer.LoadingMedia, Is.SameAs(result.LoadingMedia));
            Assert.That(assetCollector.LoadingMedia, Is.SameAs(result.LoadingMedia));
            Assert.That(assetPreloadService.Called, Is.True);
            Assert.That(assetPreloadService.Request.LoadingMedia.CoverSprite, Is.SameAs(sprite));
            Assert.That(result.SelectedMedia.CoverSprite, Is.SameAs(sprite));
            Assert.That(progress.Values[0].Stage, Is.EqualTo(StoryStartPreloadStage.LoadingMedia));
            Assert.That(progress.Values[progress.Values.Count - 1].Stage, Is.EqualTo(StoryStartPreloadStage.Complete));
        }
        finally
        {
            result.LoadingMedia?.Dispose();
            if (sprite != null)
                UnityEngine.Object.DestroyImmediate(sprite);
            if (texture != null)
                UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void StoryStartLoadingFlow_CanceledBeforeStart_DoesNotTouchServices()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var loadingMediaService = new RecordingLoadingMediaService();
        var assetCollector = new RecordingPreloadAssetCollector();
        var assetPreloadService = new RecordingAssetPreloadService();
        var flow = new StoryStartLoadingFlow(
            loadingMediaService,
            new StoryLoadingMediaPolicy(),
            assetCollector,
            assetPreloadService);

        Assert.Throws<OperationCanceledException>(() =>
            flow.RunAsync(
                    CreateFlowRequest(null),
                    null,
                    null,
                    cancellation.Token)
                .GetAwaiter()
                .GetResult());

        Assert.That(loadingMediaService.Called, Is.False);
        Assert.That(assetCollector.Called, Is.False);
        Assert.That(assetPreloadService.Called, Is.False);
    }

    [Test]
    public void StoryStartLoadingFlow_ObserverRejectsMedia_DisposesLeaseAndStops()
    {
        var data = ScriptableObject.CreateInstance<GameData>();
        Texture2D texture = null;
        Sprite sprite = null;
        var trackedHandle = new RecordingTrackedHandle();

        try
        {
            sprite = CreateSprite(ref texture);
            GameStoryLoadingMediaSettings settings = data.LoadingMedia;
            SetPrivateField(settings, "_overrideLoadingMedia", true);
            SetPrivateField(settings, "_imageReference", new AssetReferenceSprite("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));

            var loadingMediaService = new StoryLoadingMediaService(
                new StoryLoadingMediaPolicy(),
                new RecordingLoadingMediaAssetLoader(sprite, trackedHandle));
            var assetCollector = new RecordingPreloadAssetCollector();
            var assetPreloadService = new RecordingAssetPreloadService();
            var observer = new RecordingLoadingFlowObserver { AcceptMedia = false };
            var flow = new StoryStartLoadingFlow(
                loadingMediaService,
                new StoryLoadingMediaPolicy(),
                assetCollector,
                assetPreloadService);

            Assert.Throws<OperationCanceledException>(() =>
                flow.RunAsync(
                        CreateFlowRequest(data),
                        null,
                        observer,
                        CancellationToken.None)
                    .GetAwaiter()
                    .GetResult());

            Assert.That(observer.LoadingMedia, Is.Null);
            Assert.That(trackedHandle.DisposeCount, Is.EqualTo(1));
            Assert.That(assetCollector.Called, Is.False);
            Assert.That(assetPreloadService.Called, Is.False);
        }
        finally
        {
            if (sprite != null)
                UnityEngine.Object.DestroyImmediate(sprite);
            if (texture != null)
                UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private static Sprite CreateSprite(ref Texture2D texture)
    {
        texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f);
    }

    private static StoryStartLoadingFlowRequest CreateFlowRequest(GameData data)
    {
        return new StoryStartLoadingFlowRequest(
            data,
            StoryStartLoadingAssetScope.CoverOnly,
            Array.Empty<string>(),
            preloadCoverTexture: true,
            preloadStoryTextures: true,
            preloadAudioData: true,
            waitForTextureStreaming: true,
            useUnscaledTime: true,
            textureStreamingTimeout: 0.1f,
            assetsPerFrame: 1,
            asyncOperationProgressScale: 1f,
            loadingMediaStatus: "media",
            coverStatus: "cover",
            textureStatus: "texture",
            audioStatus: "audio",
            videoStatus: "video",
            dataStatus: "data",
            resourcesStatus: "resources");
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Missing field " + fieldName);
        field.SetValue(target, value);
    }

    private sealed class RecordingLoadingMediaService : IStoryLoadingMediaService
    {
        public bool Called { get; private set; }

        public UniTask<StoryLoadingMediaLease> LoadAsync(GameData data, CancellationToken cancellationToken)
        {
            Called = true;
            return new StoryLoadingMediaService().LoadAsync(data, cancellationToken);
        }
    }

    private sealed class RecordingLoadingMediaAssetLoader : IStoryLoadingMediaAssetLoader
    {
        private readonly Sprite _sprite;
        private readonly IDisposable _trackedHandle;

        public RecordingLoadingMediaAssetLoader(Sprite sprite, IDisposable trackedHandle = null)
        {
            _sprite = sprite;
            _trackedHandle = trackedHandle;
        }

        public int ValidReferenceCalls { get; private set; }

        public UniTask<T> LoadAddressableOrDefaultAsync<T>(
            AssetReference reference,
            T fallback,
            StoryLoadingMediaLease lease,
            GameData data,
            string label,
            CancellationToken cancellationToken)
            where T : UnityEngine.Object
        {
            if (!GameStoryLoadingMediaSettings.HasAddressableReference(reference))
                return UniTask.FromResult(fallback);

            ValidReferenceCalls++;
            lease.RegisterOwnedResource(_trackedHandle);
            if (typeof(T) == typeof(Sprite) && _sprite != null)
                return UniTask.FromResult((T)(UnityEngine.Object)_sprite);

            return UniTask.FromResult(fallback);
        }
    }

    private sealed class CancelingLoadingMediaAssetLoader : IStoryLoadingMediaAssetLoader
    {
        private readonly IDisposable _trackedHandle;

        public CancelingLoadingMediaAssetLoader(IDisposable trackedHandle)
        {
            _trackedHandle = trackedHandle;
        }

        public int ValidReferenceCalls { get; private set; }

        public UniTask<T> LoadAddressableOrDefaultAsync<T>(
            AssetReference reference,
            T fallback,
            StoryLoadingMediaLease lease,
            GameData data,
            string label,
            CancellationToken cancellationToken)
            where T : UnityEngine.Object
        {
            if (!GameStoryLoadingMediaSettings.HasAddressableReference(reference))
                return UniTask.FromResult(fallback);

            ValidReferenceCalls++;
            lease.RegisterOwnedResource(_trackedHandle);
            return UniTask.FromCanceled<T>(new CancellationToken(true));
        }
    }

    private sealed class RecordingTrackedHandle : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private sealed class RecordingPreloadAssetCollector : IStoryStartPreloadAssetCollector
    {
        public bool Called { get; private set; }
        public StoryLoadingMediaLease LoadingMedia { get; private set; }

        public StoryStartPreloadAssetSet Collect(
            GameData data,
            StoryStartLoadingAssetScope assetScope,
            StoryLoadingMediaLease loadingMedia)
        {
            Called = true;
            LoadingMedia = loadingMedia;
            return new StoryStartPreloadAssetSet();
        }
    }

    private sealed class FixedStoryStartChapterSelector : IStoryStartChapterSelector
    {
        private readonly ChapterData _selectedChapter;

        public FixedStoryStartChapterSelector(ChapterData selectedChapter)
        {
            _selectedChapter = selectedChapter;
        }

        public bool Called { get; private set; }
        public StoryData Story { get; private set; }

        public ChapterData SelectSavedOrFirstChapter(
            StoryData story,
            System.Collections.Generic.IReadOnlyList<ChapterData> chapters)
        {
            Called = true;
            Story = story;
            return _selectedChapter;
        }
    }

    private sealed class RecordingAssetPreloadService : IStoryStartAssetPreloadService
    {
        public bool Called { get; private set; }
        public StoryStartAssetPreloadRequest Request { get; private set; }

        public UniTask PreloadAsync(
            StoryStartAssetPreloadRequest request,
            IProgress<StoryStartPreloadProgress> progress,
            CancellationToken cancellationToken)
        {
            Called = true;
            Request = request;
            progress?.Report(StoryStartPreloadProgress.Complete("done"));
            return UniTask.CompletedTask;
        }
    }

    private sealed class RecordingPreloadProgress : IProgress<StoryStartPreloadProgress>
    {
        private readonly System.Collections.Generic.List<StoryStartPreloadProgress> _values =
            new System.Collections.Generic.List<StoryStartPreloadProgress>();

        public System.Collections.Generic.IReadOnlyList<StoryStartPreloadProgress> Values => _values;

        public void Report(StoryStartPreloadProgress value)
        {
            _values.Add(value);
        }
    }

    private sealed class RecordingLoadingFlowObserver : IStoryStartLoadingFlowObserver
    {
        public StoryLoadingMediaLease LoadingMedia { get; private set; }
        public bool AcceptMedia { get; set; } = true;

        public bool OnLoadingMediaLoaded(GameData data, StoryLoadingMediaLease loadingMedia)
        {
            if (!AcceptMedia)
                return false;

            LoadingMedia = loadingMedia;
            return true;
        }
    }
}
