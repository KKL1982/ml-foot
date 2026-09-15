using FluentAssertions;
using FootballPrediction.Application.DTOs;
using FootballPrediction.Application.Interfaces;
using FootballPrediction.Domain.Entities;
using FootballPrediction.Web.Controllers;
using FootballPrediction.Web.Models;
using FootballPrediction.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FootballPrediction.Tests.Web;

public class ApiControllerBatchTests
{
    private static ApiController CreateController(IPredictionService? service = null)
    {
        service ??= new Mock<IPredictionService>().Object;
        var batch = new BatchPredictionService(service);
        var settings = Options.Create(new ModelSettings { DefaultMode = "Multiclass" });
        var logger = new Mock<ILogger<ApiController>>().Object;
        return new ApiController(service, batch, settings, logger);
    }

    [Fact]
    public void PredictBatch_Valid_ReturnsItems()
    {
        var predServiceMock = new Mock<IPredictionService>();
        predServiceMock.Setup(s => s.PredictBatchMulticlass(It.IsAny<IReadOnlyList<PredictionInputDto>>()))
            .Returns(new List<PredictionOutputDto>
            {
                new() { PredictedResult = "1", Probability1 = 0.6, ProbabilityX = 0.25, Probability2 = 0.15, Confidence = 0.6, Comment = "Home win likely" }
            });

        var controller = CreateController(predServiceMock.Object);
        var body = new List<PredictionInputViewModel>
        {
            new() { Date = new DateTime(2026, 9, 20), League = "Premier League", HomeTeam = "Arsenal", AwayTeam = "Chelsea", HomeCoach = "Arteta" }
        };

        var result = controller.PredictBatch(body);
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var items = ok.Value.Should().BeAssignableTo<IReadOnlyList<BatchPredictionItem>>().Subject;
        items.Should().HaveCount(1);
        items[0].HomeTeam.Should().Be("Arsenal");
        items[0].PredictedResult.Should().Be("1");
    }

    [Fact]
    public void PredictBatch_InvalidModel_ReturnsBadRequest()
    {
        var controller = CreateController();
        controller.ModelState.AddModelError("HomeTeam", "Required");

        var result = controller.PredictBatch(new List<PredictionInputViewModel>());
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }
}
