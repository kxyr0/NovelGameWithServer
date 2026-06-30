using System;

[Serializable]
public sealed class PlayerNameCaseForms
{
    public string nom;
    public string gen;
    public string dat;
    public string acc;
    public string ins;
    public string prep;

    public string Get(PlayerNameCase grammaticalCase)
    {
        switch (grammaticalCase)
        {
            case PlayerNameCase.Nominative:
                return nom;
            case PlayerNameCase.Genitive:
                return gen;
            case PlayerNameCase.Dative:
                return dat;
            case PlayerNameCase.Accusative:
                return acc;
            case PlayerNameCase.Instrumental:
                return ins;
            case PlayerNameCase.Prepositional:
                return prep;
            default:
                return "";
        }
    }

    public bool HasAny()
    {
        return !string.IsNullOrWhiteSpace(nom) ||
               !string.IsNullOrWhiteSpace(gen) ||
               !string.IsNullOrWhiteSpace(dat) ||
               !string.IsNullOrWhiteSpace(acc) ||
               !string.IsNullOrWhiteSpace(ins) ||
               !string.IsNullOrWhiteSpace(prep);
    }
}
