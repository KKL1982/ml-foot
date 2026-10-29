using Microsoft.AspNetCore.HttpOverrides;

namespace FootballPrediction.Web.Services;

/// <summary>
/// Fait confiance à X-Forwarded-For / X-Forwarded-Proto uniquement s'ils viennent du reverse proxy
/// (plages privées par défaut, ou celles de ForwardedHeaders:KnownNetworks, séparées par des virgules).
/// </summary>
public static class ForwardedHeadersSetup
{
    public const string KnownNetworksKey = "ForwardedHeaders:KnownNetworks";
    public const string DefaultNetworks = "172.16.0.0/12,10.0.0.0/8,192.168.0.0/16";

    public static void Configure(ForwardedHeadersOptions options, string? networks)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
        var list = string.IsNullOrWhiteSpace(networks) ? DefaultNetworks : networks;
        foreach (var cidr in list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = cidr.Split('/');
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(
                System.Net.IPAddress.Parse(parts[0]), int.Parse(parts[1])));
        }
    }
}
