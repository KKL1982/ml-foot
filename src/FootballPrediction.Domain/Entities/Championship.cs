namespace FootballPrediction.Domain.Entities;

/// <summary>
/// Identifie un championnat supporté par le système de prédiction.
/// Un code court (ex. "PL") + le nom canonique (ex. "Premier League").
/// </summary>
public sealed class Championship
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public Championship() { }

    public Championship(string code, string name)
    {
        Code = code;
        Name = name;
    }
}
