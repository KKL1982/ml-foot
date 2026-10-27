using System.Net;
using FluentAssertions;
using FootballPrediction.Application.DTOs;
using FootballPrediction.Application.Interfaces;
using FootballPrediction.Application.Services;
using FootballPrediction.Web.Controllers;
using FootballPrediction.Web.Models;
using FootballPrediction.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Match = FootballPrediction.Domain.Entities.Match;

namespace FootballPrediction.Tests.Web;

public class FootballDataClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public HttpRequestMessage? LastRequest { get; private set; }
        public StubHandler(HttpStatusCode status, string body) { _status = status; _body = body; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_status) { Content = new StringContent(_body) });
        }
    }

    private static FootballDataClient Create(StubHandler handler, string apiKey = "k")
        => new(new HttpClient(handler), new MemoryCache(new MemoryCacheOptions()),
               Options.Create(new FootballDataOptions { ApiKey = apiKey }),
               new Mock<ILogger<FootballDataClient>>().Object);

    [Fact]
    public async Task GetMatches_MapsJson_AndSendsTokenAndCompetitionCode()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """
            {"matches":[
              {"utcDate":"2026-11-01T15:00:00Z","homeTeam":{"name":"Arsenal FC"},"awayTeam":{"name":"Chelsea FC"}},
              {"utcDate":"2026-11-02T15:00:00Z","homeTeam":{"name":null},"awayTeam":{"name":"X"}}
            ]}
            """);

        var matches = await Create(handler).GetMatchesAsync("LL", new DateTime(2026, 11, 1), new DateTime(2026, 11, 7));

        matches.Should().ContainSingle();
        matches[0].HomeTeam.Should().Be("Arsenal FC");
        matches[0].League.Should().Be("LaLiga");
        matches[0].Season.Should().Be("2026-2027");
        matches[0].Date.Should().Be(new DateTime(2026, 11, 1));
        matches[0].Result.Should().BeNull();
        handler.LastRequest!.RequestUri!.ToString().Should().Contain("/competitions/PD/matches?dateFrom=2026-11-01&dateTo=2026-11-07");
        handler.LastRequest.Headers.GetValues("X-Auth-Token").Should().ContainSingle("k");
    }

    [Fact]
    public async Task GetTeamCoaches_ReadsCoachName()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"teams":[{"name":"Arsenal FC","coach":{"name":"Mikel Arteta"}},{"name":"Vide FC","coach":null}]}""");

        var coaches = await Create(handler).GetTeamCoachesAsync("PL");

        coaches.Should().HaveCount(2);
        coaches[0].Should().Be(new ExternalTeamCoach("Arsenal FC", "Mikel Arteta"));
        coaches[1].Coach.Should().BeNull();
    }

    [Fact]
    public async Task MissingKey_ThrowsReadableError()
    {
        var act = () => Create(new StubHandler(HttpStatusCode.OK, "{}"), apiKey: "").GetTeamCoachesAsync("PL");
        (await act.Should().ThrowAsync<FootballDataException>()).Which.Message.Should().Contain("ApiKey");
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "offre")]
    [InlineData(HttpStatusCode.TooManyRequests, "minute")]
    public async Task HttpErrors_AreTranslated(HttpStatusCode status, string expected)
    {
        var act = () => Create(new StubHandler(status, "{}")).GetTeamCoachesAsync("PL");
        (await act.Should().ThrowAsync<FootballDataException>()).Which.Message.Should().Contain(expected);
    }

    [Theory]
    [InlineData(2026, 8, 15, "2026-2027")]
    [InlineData(2027, 3, 1, "2026-2027")]
    public void SeasonFor_UsesEuropeanSeason(int y, int m, int d, string expected)
        => FootballDataClient.SeasonFor(new DateTime(y, m, d)).Should().Be(expected);
}

public class BatchControllerOnlineTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mlfoot_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private (BatchController controller, Mock<IFootballDataClient> client, MatchFileStore store) Create()
    {
        var client = new Mock<IFootballDataClient>();
        var store = new MatchFileStore(_dir);
        var repo = new InMemoryCoachRepository();
        var pred = new Mock<IPredictionService>();
        pred.Setup(p => p.PredictBatchMulticlass(It.IsAny<IReadOnlyList<PredictionInputDto>>()))
            .Returns((IReadOnlyList<PredictionInputDto> inputs) => inputs.Select(_ => new PredictionOutputDto
            {
                PredictedResult = "1", Probability1 = 0.6, ProbabilityX = 0.25, Probability2 = 0.15, Confidence = 0.6
            }).ToList());

        var controller = new BatchController(
            pred.Object,
            new BatchPredictionService(pred.Object, repo),
            new CsvParserService(),
            Options.Create(new ModelSettings { DefaultMode = "Multiclass" }),
            new Mock<ILogger<BatchController>>().Object,
            new MatchFetchService(client.Object, store),
            new CoachRefreshService(client.Object, repo, new CoachService(repo)),
            store);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.TempData = new TempDataDictionary(controller.ControllerContext.HttpContext, Mock.Of<ITempDataProvider>());
        return (controller, client, store);
    }

    [Fact]
    public async Task FetchMatches_Valid_CreatesCsv_AndPredictsFromIt()
    {
        var (controller, client, store) = Create();
        client.Setup(c => c.GetMatchesAsync("PL", It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ReturnsAsync(new List<Match>
            {
                new() { Date = new DateTime(2026, 11, 1), League = "Premier League", Season = "2026-2027",
                        HomeTeam = "Arsenal FC", AwayTeam = "Chelsea FC" }
            });

        var result = await controller.FetchMatches(new[] { "PL" }, new DateTime(2026, 11, 1), new DateTime(2026, 11, 7), default);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.ViewName.Should().Be("Result");
        var model = view.Model.Should().BeOfType<BatchResultViewModel>().Subject;
        model.Predictions.Should().ContainSingle();
        model.Predictions[0].HomeTeam.Should().Be("Arsenal");
        model.SourceFileName.Should().Be("matches_PL_20261101_20261107.csv");
        store.FindExisting(model.SourceFileName).Should().NotBeNull();
    }

    [Fact]
    public async Task FetchMatches_NoLeague_ReturnsIndexWithError()
    {
        var (controller, _, _) = Create();
        var result = await controller.FetchMatches(Array.Empty<string>(), DateTime.Today, DateTime.Today, default);
        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.ViewName.Should().Be("Index");
        ((BatchIndexViewModel)view.Model!).Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task FetchMatches_PeriodTooLong_ReturnsIndexWithError()
    {
        var (controller, _, _) = Create();
        var result = await controller.FetchMatches(new[] { "PL" }, DateTime.Today, DateTime.Today.AddDays(90), default);
        ((BatchIndexViewModel)((ViewResult)result).Model!).Error.Should().Contain("31");
    }

    [Fact]
    public async Task FetchMatches_ApiError_ShowsErrorOnIndex()
    {
        var (controller, client, _) = Create();
        client.Setup(c => c.GetMatchesAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
            .ThrowsAsync(new FootballDataException("clé absente"));

        var result = await controller.FetchMatches(new[] { "PL" }, DateTime.Today, DateTime.Today.AddDays(2), default);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.ViewName.Should().Be("Index");
        ((BatchIndexViewModel)view.Model!).Error.Should().Contain("clé absente");
    }

    [Fact]
    public async Task RefreshCoaches_ReturnsIndexWithSummary()
    {
        var (controller, client, _) = Create();
        client.Setup(c => c.GetTeamCoachesAsync("PL", default))
            .ReturnsAsync(new List<ExternalTeamCoach> { new("Arsenal FC", "Mikel Arteta") });
        client.Setup(c => c.GetTeamCoachesAsync(It.Is<string>(s => s != "PL"), default))
            .ReturnsAsync(new List<ExternalTeamCoach>());

        var result = await controller.RefreshCoaches(default);

        var model = ((ViewResult)result).Model.Should().BeOfType<BatchIndexViewModel>().Subject;
        model.CoachRefresh!.Changed.Should().ContainSingle();
    }

    [Fact]
    public async Task PredictFile_UnknownFile_ReturnsIndexWithError()
    {
        var (controller, _, _) = Create();
        var result = await controller.PredictFile("../etc/passwd");
        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.ViewName.Should().Be("Index");
        ((BatchIndexViewModel)view.Model!).Error.Should().Contain("introuvable");
    }

    [Fact]
    public void DownloadFile_Invalid_ReturnsNotFound()
    {
        var (controller, _, _) = Create();
        controller.DownloadFile("../x.csv").Should().BeOfType<NotFoundResult>();
    }
}
