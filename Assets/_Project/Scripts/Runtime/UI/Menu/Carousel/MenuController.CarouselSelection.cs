using UnityEngine;

public partial class MenuController
{
    [Header("Story Carousel Selection")]
    [SerializeField, Tooltip("First tap selects a side card; a tap on the selected card opens it.")]
    private bool _selectSideCardBeforeOpening = true;

    private bool TrySelectCarouselCard(GameData data)
    {
        if (!_selectSideCardBeforeOpening || !IsStoryCarouselEnabled() || data == null)
            return false;

        var games = BuildAvailableGameList();
        for (int i = 0; i < games.Count; i++)
        {
            if (games[i] != data)
                continue;

            if (i == _selectedGameIndex)
                return false;

            _selectedGameIndex = i;
            BuildGameList();
            return true;
        }

        return false;
    }
}
