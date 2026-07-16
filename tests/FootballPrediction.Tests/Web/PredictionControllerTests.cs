using FluentAssertions;
using FootballPrediction.Web.Controllers;
using FootballPrediction.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace FootballPrediction.Tests.Web;

public class PredictionControllerTests
{
    [Fact]
    public void Index_Get_ShouldReturnView()
    {
        var controller = new PredictionController();

        var result = controller.Index();

        result.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public void Predict_InvalidModel_ShouldReturnView()
    {
        var controller = new PredictionController();
        controller.ModelState.AddModelError("Date", "Required");

        var model = new PredictionInputViewModel();
        var result = controller.Predict(model);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.ViewName.Should().Be("Index");
    }

    [Fact]
    public void Predict_ValidModel_ShouldReturnResultView()
    {
        var controller = new PredictionController();
        var model = new PredictionInputViewModel
        {
            Date = DateTime.Today,
            League = "Premier League",
            HomeTeam = "Arsenal",
            AwayTeam = "Chelsea",
            Bet365Home = 2.0,
            Bet365Draw = 3.5,
            Bet365Away = 4.0
        };

        var result = controller.Predict(model);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.ViewName.Should().Be("Result");
        viewResult.Model.Should().BeAssignableTo<PredictionResultViewModel>();
    }

    [Fact]
    public void Predict_WithoutOdds_ShouldStillPredict()
    {
        var controller = new PredictionController();
        var model = new PredictionInputViewModel
        {
            Date = DateTime.Today,
            League = "LaLiga",
            HomeTeam = "Barcelona",
            AwayTeam = "Real Madrid"
        };

        var result = controller.Predict(model);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.Model.Should().BeAssignableTo<PredictionResultViewModel>();
    }
}
