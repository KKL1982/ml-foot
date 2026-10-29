using System.Net;
using FluentAssertions;
using FootballPrediction.Web.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Xunit;

namespace FootballPrediction.Tests.Web;

public class ForwardedHeadersSetupTests
{
    private static bool Trusts(ForwardedHeadersOptions o, string ip) =>
        o.KnownNetworks.Any(n => n.Contains(IPAddress.Parse(ip)));

    [Fact]
    public void Default_trusts_private_docker_ranges_only()
    {
        var o = new ForwardedHeadersOptions();
        ForwardedHeadersSetup.Configure(o, null);

        o.ForwardedHeaders.Should().HaveFlag(ForwardedHeaders.XForwardedProto);
        o.ForwardedHeaders.Should().HaveFlag(ForwardedHeaders.XForwardedFor);
        Trusts(o, "172.18.0.5").Should().BeTrue();
        Trusts(o, "203.0.113.9").Should().BeFalse();
    }

    [Fact]
    public void Configured_network_replaces_defaults()
    {
        var o = new ForwardedHeadersOptions();
        ForwardedHeadersSetup.Configure(o, "172.18.0.0/16");

        Trusts(o, "172.18.0.5").Should().BeTrue();
        Trusts(o, "172.19.0.5").Should().BeFalse();
        Trusts(o, "10.0.0.1").Should().BeFalse();
    }
}
