using UnityEngine;

/// <summary>
/// Bridges the actual EndScreen GameObject lifecycle to StoryEndScreenController.
/// The controller can live on a persistent UIRoot without relying on UIRoot.OnEnable.
/// </summary>
[DisallowMultipleComponent]
public sealed class StoryEndScreenActivationRelay : MonoBehaviour
{
    StoryEndScreenController _owner;

    public void Bind(StoryEndScreenController owner)
    {
        _owner = owner;
    }

    void OnEnable()
    {
        _owner?.NotifyEndScreenRootEnabled(gameObject);
    }
}
