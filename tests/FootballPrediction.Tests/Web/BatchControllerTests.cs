using FluentAssertions;
using FootballPrediction.Web.Controllers;
using FootballPrediction.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FootballPrediction.Tests.Web;

public class BatchControllerTests
{
    [Fact]
    public void Index_Get_ShouldReturnView()
    {
        var controller = new BatchController();

        var result = controller.Index();

        result.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public async Task Upload_NoFile_ShouldReturnViewWithError()
    {
        var controller = new BatchController();

        var result = await controller.Upload(null!);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.ViewName.Should().Be("Index");
        controller.ModelState.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Upload_ValidCsv_ShouldReturnResultView()
    {
        var csvContent = @"Date,League,Season,HomeTeam,AwayTeam,HomeGoals,AwayGoals,Result,HomeCoach,AwayCoach,Bet365_1,Bet365_X,Bet365_2
2024-08-16,Premier League,2024-2025,Manchester United,Fulham,1,0,1,Ten Hag,Silva,1.44,4.75,6.50
2024-08-17,Premier League,2024-2025,Ipswich,Liverpool,0,2,2,McKenna,Slot,9.00,5.50,1.30";

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

        var controller = new BatchController();

        var result = await controller.Upload(formFile);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        viewResult.ViewName.Should().Be("Result");
        viewResult.Model.Should().BeAssignableTo<BatchResultViewModel>();

        var model = (BatchResultViewModel)viewResult.Model!;
        model.TotalMatches.Should().Be(2);
        model.SuccessfulPredictions.Should().Be(2);
        model.Predictions.Should().HaveCount(2);
    }
}
