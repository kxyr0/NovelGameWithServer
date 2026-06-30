using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "Game Catalog", menuName = "VN/Menu/Game Catalog")]
public class GameCatalog : ScriptableObject
{
    static readonly IReadOnlyList<GameData> EmptyGames = Array.Empty<GameData>();

    [SerializeField]
    [FormerlySerializedAs("GameData")]
    private List<GameData> _games = new List<GameData>();

    public IReadOnlyList<GameData> Games => _games ?? EmptyGames;
    public int Count => Games.Count;

    public bool HasGames => Count > 0;

    public bool Contains(GameData gameData)
    {
        return gameData != null && _games != null && _games.Contains(gameData);
    }

    public bool AddGame(GameData gameData)
    {
        if (gameData == null)
            return false;

        if (_games == null)
            _games = new List<GameData>();

        if (_games.Contains(gameData))
            return false;

        _games.Add(gameData);
        return true;
    }

    public bool RemoveGame(GameData gameData)
    {
        return gameData != null && _games != null && _games.Remove(gameData);
    }

    public void Configure(IEnumerable<GameData> games)
    {
        _games = games != null ? new List<GameData>(games) : new List<GameData>();
    }

    private void OnValidate()
    {
        if (_games == null)
            _games = new List<GameData>();
    }
}
