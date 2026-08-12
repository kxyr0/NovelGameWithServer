using VContainer;
using VContainer.Unity;

public sealed class NovelTemplateLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterInstance(SubscriptionFeatureConfig.LoadOrDisabled());

        builder.Register<SystemSubscriptionClock>(Lifetime.Singleton)
            .As<ISubscriptionClock>();

        builder.Register<SubscriptionSignedTokenVerifier>(Lifetime.Singleton)
            .As<ISubscriptionSignatureVerifier>();

        builder.Register<SubscriptionEntitlementSerializer>(Lifetime.Singleton);

        builder.Register<FileSubscriptionEntitlementStore>(Lifetime.Singleton)
            .As<ISubscriptionEntitlementStore>();

        builder.Register<UnavailableSubscriptionEntitlementProvider>(Lifetime.Singleton)
            .As<ISubscriptionEntitlementProvider>();

        builder.Register<SubscriptionEntitlementEvaluator>(Lifetime.Singleton);

        builder.Register<SubscriptionEntitlementService>(Lifetime.Singleton)
            .As<ISubscriptionEntitlementService>();

        builder.Register<SubscriptionEpisodeAccessPolicy>(Lifetime.Singleton)
            .As<ISubscriptionEpisodeAccessPolicy>();

        builder.Register<SubscriptionEpisodeAccessService>(Lifetime.Singleton)
            .As<ISubscriptionEpisodeAccessService>();

        builder.Register<StoryChoiceTimelineStore>(Lifetime.Singleton)
            .As<IStoryChoiceTimelineStore>();

        builder.Register<StoryChoiceTimelineService>(Lifetime.Singleton)
            .As<IStoryChoiceTimelineService>();

        builder.RegisterEntryPoint<SubscriptionBootstrapper>();

        builder.Register<StoryLoadingMediaPolicy>(Lifetime.Singleton)
            .As<IStoryLoadingMediaPolicy>();

        builder.Register<StoryLoadingMediaReadinessPolicy>(Lifetime.Singleton)
            .As<IStoryLoadingMediaReadinessPolicy>();

        builder.Register<AddressablesStoryLoadingMediaAssetLoader>(Lifetime.Singleton)
            .As<IStoryLoadingMediaAssetLoader>();

        builder.Register<StoryLoadingMediaService>(Lifetime.Singleton)
            .As<IStoryLoadingMediaService>();

        builder.Register<StoryStartAssetPreloadService>(Lifetime.Singleton)
            .As<IStoryStartAssetPreloadService>();

        builder.Register<SavedOrFirstStoryStartChapterSelector>(Lifetime.Singleton)
            .As<IStoryStartChapterSelector>();

        builder.Register<StoryStartPreloadAssetCollector>(Lifetime.Singleton)
            .As<IStoryStartPreloadAssetCollector>();

        builder.Register<StoryStartVideoCoverLayoutPolicy>(Lifetime.Singleton)
            .As<IStoryStartVideoCoverLayoutPolicy>();

        builder.Register<StoryStartLoadingFlow>(Lifetime.Singleton)
            .As<IStoryStartLoadingFlow>();

        builder.RegisterComponentInHierarchy<StoryStartLoadingScreen>()
            .As<IStoryStartLoadingScreen>();

        builder.RegisterComponentInHierarchy<MenuController>();

        builder.RegisterComponentInHierarchy<StoryManager>();
    }
}
