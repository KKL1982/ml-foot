namespace FootballPrediction.Web.Services;

/// <summary>
/// Version déployée : commit git court et date, injectés au build Docker (APP_VERSION, APP_BUILD_DATE).
/// Hors Docker (développement), la version vaut "dev".
/// </summary>
public static class AppVersion
{
    public static string Commit { get; } = Resolve(Environment.GetEnvironmentVariable("APP_VERSION"));
    public static string BuildDate { get; } = (Environment.GetEnvironmentVariable("APP_BUILD_DATE") ?? "").Trim();

    public static string Label => string.IsNullOrEmpty(BuildDate) ? Commit : $"{Commit} · {BuildDate}";

    public static string Resolve(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "dev" : value.Trim();
}
