using UnityEngine;

public readonly struct StoryStartVideoCoverBaseLayout
{
    public StoryStartVideoCoverBaseLayout(Vector2 size, Vector2 anchoredPosition, Vector3 scale, float rotationZ)
    {
        Size = size;
        AnchoredPosition = anchoredPosition;
        Scale = scale;
        RotationZ = NormalizeAngle(rotationZ);
    }

    public Vector2 Size { get; }
    public Vector2 AnchoredPosition { get; }
    public Vector3 Scale { get; }
    public float RotationZ { get; }

    public static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
            angle -= 360f;
        if (angle < -180f)
            angle += 360f;
        return angle;
    }
}

public readonly struct StoryStartVideoCoverLayoutRequest
{
    public StoryStartVideoCoverLayoutRequest(
        StoryStartVideoCoverBaseLayout baseLayout,
        GameMenuCardOverrideSettings overrides,
        bool stretchByDefault,
        Vector2 defaultStretchScale,
        float defaultStretchRotationZ)
    {
        BaseLayout = baseLayout;
        Overrides = overrides;
        StretchByDefault = stretchByDefault;
        DefaultStretchScale = NormalizeScale(defaultStretchScale);
        DefaultStretchRotationZ = StoryStartVideoCoverBaseLayout.NormalizeAngle(defaultStretchRotationZ);
    }

    public StoryStartVideoCoverBaseLayout BaseLayout { get; }
    public GameMenuCardOverrideSettings Overrides { get; }
    public bool StretchByDefault { get; }
    public Vector2 DefaultStretchScale { get; }
    public float DefaultStretchRotationZ { get; }

    private static Vector2 NormalizeScale(Vector2 scale)
    {
        if (Mathf.Approximately(scale.x, 0f))
            scale.x = 1f;
        if (Mathf.Approximately(scale.y, 0f))
            scale.y = 1f;

        return scale;
    }
}

public readonly struct StoryStartVideoCoverLayout
{
    private StoryStartVideoCoverLayout(
        bool stretch,
        Vector2 size,
        Vector2 anchoredPosition,
        Vector3 scale,
        float rotationZ)
    {
        Stretch = stretch;
        Size = size;
        AnchoredPosition = anchoredPosition;
        Scale = scale;
        RotationZ = StoryStartVideoCoverBaseLayout.NormalizeAngle(rotationZ);
    }

    public bool Stretch { get; }
    public Vector2 Size { get; }
    public Vector2 AnchoredPosition { get; }
    public Vector3 Scale { get; }
    public float RotationZ { get; }

    public static StoryStartVideoCoverLayout Stretched(Vector2 scale, float rotationZ)
    {
        return new StoryStartVideoCoverLayout(
            stretch: true,
            size: Vector2.zero,
            anchoredPosition: Vector2.zero,
            scale: new Vector3(scale.x, scale.y, 1f),
            rotationZ: rotationZ);
    }

    public static StoryStartVideoCoverLayout Framed(
        Vector2 size,
        Vector2 anchoredPosition,
        Vector3 scale,
        float rotationZ)
    {
        return new StoryStartVideoCoverLayout(
            stretch: false,
            size: size,
            anchoredPosition: anchoredPosition,
            scale: scale,
            rotationZ: rotationZ);
    }
}

public interface IStoryStartVideoCoverLayoutPolicy
{
    StoryStartVideoCoverLayout Resolve(StoryStartVideoCoverLayoutRequest request);
}

public sealed class StoryStartVideoCoverLayoutPolicy : IStoryStartVideoCoverLayoutPolicy
{
    public StoryStartVideoCoverLayout Resolve(StoryStartVideoCoverLayoutRequest request)
    {
        GameMenuCardOverrideSettings overrides = request.Overrides;
        if (overrides != null && overrides.StretchVideoOnLoadingScreen)
            return StoryStartVideoCoverLayout.Stretched(overrides.LoadingVideoStretchScale, overrides.LoadingVideoRotationZ);

        if (request.StretchByDefault)
            return StoryStartVideoCoverLayout.Stretched(request.DefaultStretchScale, request.DefaultStretchRotationZ);

        Vector2 videoSize = request.BaseLayout.Size;
        Vector2 videoPosition = request.BaseLayout.AnchoredPosition;
        float videoRotationZ = request.BaseLayout.RotationZ;

        if (overrides != null)
        {
            if (overrides.OverrideVideoSize)
                videoSize = overrides.VideoSize;

            if (overrides.OverrideVideoPosition)
                videoPosition = overrides.VideoPosition;

            if (overrides.OverrideVideoRotation)
                videoRotationZ = overrides.VideoRotationZ;
        }

        return StoryStartVideoCoverLayout.Framed(
            videoSize,
            videoPosition,
            request.BaseLayout.Scale,
            videoRotationZ);
    }
}

public static class StoryStartVideoCoverLayoutPolicies
{
    private static readonly IStoryStartVideoCoverLayoutPolicy SharedPolicy = new StoryStartVideoCoverLayoutPolicy();

    public static IStoryStartVideoCoverLayoutPolicy Shared => SharedPolicy;
}
