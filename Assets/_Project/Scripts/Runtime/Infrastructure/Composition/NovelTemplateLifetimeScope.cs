using VContainer;
using VContainer.Unity;

public sealed class NovelTemplateLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
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
    }
}
