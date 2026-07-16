using FootballPrediction.Domain.Entities;

namespace FootballPrediction.Application.Interfaces;

public interface ICsvParser
{
    Task<IReadOnlyList<Match>> ParseMatchesAsync(string filePath);
}
