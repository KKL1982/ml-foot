using FluentAssertions;
using FootballPrediction.Application.DTOs;
using FootballPrediction.Application.Interfaces;
using FootballPrediction.Web.Controllers;
using FootballPrediction.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FootballPrediction.Tests.Web;

public class PredictionControllerTests
{
    private static PredictionController CreateController(
        IPredictionService? service = null,
        string defaultMode = "Binary")
    {
        service ??= new Mock<IPredictionService>().Object;
        var settings = Options.Create(new ModelSettings { DefaultMode = defaultMode });
        var logger = new Mock<ILogger<PredictionController>>().Object;
        return new PredictionController(service, settings, logger);
    }

    [Fact]
    public void Index_Get_ShouldReturnView()
    {
        var controller = CreateController();
        var result = controller.Index();
        result.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public void Index_Get_ShouldSetModelLoaded()
    {
        var controller = CreateController();
        var result = (ViewResult)controller.Index();
        var model = result.Model.Should().BeAssignableTo<PredictionInputViewModel>().Subject;
        model.ModelLoaded.Should().BeTrue();
    }

    [Fact]
    public void Predict_InvalidModel_ShouldReturnView()
    {
        var controller = CreateController();
        controller.ModelState.AddModelError("Date", "Required");
        var result = controller.Predict(new PredictionInputViewModel());
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.ViewName.Should().Be("Index");
    }

    [Fact]
    public void PredictBinary_ValidModel_ShouldReturnResultView()
    {
        var mockService = new Mock<IPredictionService>();
        mockService.Setup(s => s.PredictBinary(It.IsAny<PredictionInputDto>(), It.IsAny<double>()))
            .Returns(new BinaryPredictionOutputDto
            {
                HomeTeam = "Arsenal", AwayTeam = "Chelsea",
                HomeWinProbability = 0.65, AwayWinProbability = 0.35,
                Confidence = 0.65, Bet = "HOME", Comment = "Strong home signal"
            });

        var controller = CreateController(mockService.Object);
        var model = new PredictionInputViewModel
        {
            Date = DateTime.Today, League = "Premier League",
            HomeTeam = "Arsenal", AwayTeam = "Chelsea",
            Bet365Home = 2.0, Bet365Draw = 3.5, Bet365Away = 4.0
        };

        var result = controller.Predict(model);
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.ViewName.Should().Be("Result");
        viewResult.Model.Should().BeAssignableTo<PredictionResultViewModel>();

        var vm = (PredictionResultViewModel)viewResult.Model!;
        vm.PredictedResult.Should().Be("HOME");
        vm.Confidence.Should().Be(0.65);
    }

    [Fact]
    public void PredictMulticlass_ShouldReturnThreeProbabilities()
    {
        var mockService = new Mock<IPredictionService>();
        mockService.Setup(s => s.PredictMulticlass(It.IsAny<PredictionInputDto>()))
            .Returns(new PredictionOutputDto
            {
                HomeTeam = "Barca", AwayTeam = "Real",
                PredictedResult = "1", Probability1 = 0.48, ProbabilityX = 0.28, Probability2 = 0.24,
                Confidence = 0.48, Comment = "Home win likely"
            });

        var controller = CreateController(mockService.Object, "Multiclass");
        var model = new PredictionInputViewModel
        {
            Date = DateTime.Today, League = "LaLiga",
            HomeTeam = "Barca", AwayTeam = "Real",
            Mode = "Multiclass"
        };

        var result = controller.Predict(model);
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.Model.Should().BeAssignableTo<PredictionResultViewModel>();

        var vm = (PredictionResultViewModel)viewResult.Model!;
        vm.PredictedResult.Should().Be("1");
        vm.Probability1.Should().Be(0.48);
        vm.ProbabilityX.Should().Be(0.28);
        vm.Probability2.Should().Be(0.24);
    }

    [Fact]
    public void Predict_WithoutOdds_ShouldStillPredict()
    {
        var mockService = new Mock<IPredictionService>();
        mockService.Setup(s => s.PredictBinary(It.IsAny<PredictionInputDto>(), It.IsAny<double>()))
            .Returns(new BinaryPredictionOutputDto
            {
                HomeTeam = "Barca", AwayTeam = "Real",
                HomeWinProbability = 0.55, AwayWinProbability = 0.45,
                Confidence = 0.55, Bet = "HOME", Comment = "Signal"
            });

        var controller = CreateController(mockService.Object);
        var model = new PredictionInputViewModel
        {
            Date = DateTime.Today, League = "LaLiga",
            HomeTeam = "Barcelona", AwayTeam = "Real Madrid"
        };

        var result = controller.Predict(model);
        result.Should().BeOfType<ViewResult>();
    }
}
