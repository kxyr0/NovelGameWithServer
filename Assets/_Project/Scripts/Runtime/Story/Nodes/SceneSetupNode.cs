using UnityEngine;

public class SceneSetupNode : BaseStoryNode
{
    [TextArea] public string sceneLabel;
    public SceneSetupData sceneData;

    /// <summary>
    /// Подсказки из импортированного текста: имена или описания фона и музыки.
    /// StoryGraphAssetMatcher использует их для поиска реальных ассетов.
    /// Заполняется автоматически, можно менять вручную.
    /// </summary>
    [HideInInspector] public string suggestedBackground;
    [HideInInspector] public string suggestedMusic;
}
