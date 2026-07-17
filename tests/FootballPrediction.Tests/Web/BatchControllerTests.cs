using FluentAssertions;
using FootballPrediction.Application.DTOs;
using FootballPrediction.Application.Interfaces;
using FootballPrediction.Web.Controllers;
using FootballPrediction.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FootballPrediction.Tests.Web;

public class BatchControllerTests
{
    private static BatchController CreateController(
        IPredictionService? predService = null,
        ICsvParser? csvParser = null,
        string defaultMode = "Binary")
    {
        predService ??= new Mock<IPredictionService>().Object;
        csvParser ??= new Mock<ICsvParser>().Object;
        var settings = Options.Create(new ModelSettings { DefaultMode = defaultMode });
        var logger = new Mock<ILogger<BatchController>>().Object;
        var controller = new BatchController(predService, csvParser, settings, logger);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.TempData = new TempDataDictionary(controller.ControllerContext.HttpContext, Mock.Of<ITempDataProvider>());
        return controller;
    }

    [Fact]
    public void Index_Get_ShouldReturnView()
    {
        var controller = CreateController();
        var result = controller.Index();
        result.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public async Task Upload_NoFile_ShouldReturnViewWithError()
    {
        var controller = CreateController();
        var result = await controller.Upload(null!);
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.ViewName.Should().Be("Index");
        controller.ModelState.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Upload_ValidCsv_ShouldReturnResultView()
    {
        var csvParserMock = new Mock<ICsvParser>();
        csvParserMock.Setup(p => p.ParseMatchesAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<FootballPrediction.Domain.Entities.Match>
            {
                new() { Date = DateTime.Today, League = "PL", HomeTeam = "A", AwayTeam = "B", HomeGoals = 1, AwayGoals = 0,
                    Result = FootballPrediction.Domain.Enums.MatchResult.HomeWin,
                    BookmakerOdds = new Dictionary<string, FootballPrediction.Domain.Entities.Odds>
                    {
                        ["Bet365"] = new() { Bookmaker = "Bet365", HomeWin = 1.80m, Draw = 3.50m, AwayWin = 4.50m }
                    }},
                new() { Date = DateTime.Today, League = "PL", HomeTeam = "C", AwayTeam = "D", HomeGoals = 0, AwayGoals = 2,
                    Result = FootballPrediction.Domain.Enums.MatchResult.AwayWin,
                    BookmakerOdds = new Dictionary<string, FootballPrediction.Domain.Entities.Odds>
                    {
                        ["Bet365"] = new() { Bookmaker = "Bet365", HomeWin = 3.00m, Draw = 3.25m, AwayWin = 2.20m }
                    }}
            });

        var predServiceMock = new Mock<IPredictionService>();
        predServiceMock.Setup(s => s.PredictBatchBinary(It.IsAny<IReadOnlyList<PredictionInputDto>>(), It.IsAny<double>()))
            .Returns(new List<BinaryPredictionOutputDto>
            {
                new() { HomeTeam = "A", AwayTeam = "B", Bet = "HOME", HomeWinProbability = 0.65, Confidence = 0.65 },
                new() { HomeTeam = "C", AwayTeam = "D", Bet = "AWAY", HomeWinProbability = 0.35, Confidence = 0.65 }
            });

        var csvContent = @"Date,League,Season,HomeTeam,AwayTeam,HomeGoals,AwayGoals,Result,Bet365_1,Bet365_X,Bet365_2
2024-08-16,PL,2024,A,B,1,0,1,1.80,3.50,4.50
2024-08-17,PL,2024,C,D,0,2,2,3.00,3.25,2.20";

        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        await writer.WriteAsync(csvContent);
        await writer.FlushAsync();
        stream.Position = 0;

        var formFile = new FormFile(stream, 0, stream.Length, "file", "matches.csv")
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };

        var controller = CreateController(predServiceMock.Object, csvParserMock.Object);
        var result = await controller.Upload(formFile);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.ViewName.Should().Be("Result");
        viewResult.Model.Should().BeAssignableTo<BatchResultViewModel>();

        var model = (BatchResultViewModel)viewResult.Model!;
        model.TotalMatches.Should().Be(2);
        model.Predictions.Should().HaveCount(2);
    }

    [Fact]
    public async Task Download_ShouldReturnCsvFile()
    {
        var controller = CreateController();
        controller.TempData["BatchPredictionsCsv"] = "Date,League,Home,Away,Bet,HomeProb,Conf\n2024-01-01,PL,A,B,HOME,0.65,0.65";

        var result = controller.Download();
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("text/csv");
        fileResult.FileDownloadName.Should().Contain("predictions");
    }

    [Fact]
    public void Download_NoData_ShouldRedirect()
    {
        var controller = CreateController();
        var result = controller.Download();
        result.Should().BeOfType<RedirectToActionResult>();
    }
}
