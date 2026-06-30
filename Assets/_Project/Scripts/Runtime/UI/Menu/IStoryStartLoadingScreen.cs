using System;
using System.Collections;

public interface IStoryStartLoadingScreen
{
    bool IsVisible { get; }
    void Show(GameData data, Action onComplete);
    IEnumerator ShowAndWait(GameData data);
    void HideImmediate();
}
