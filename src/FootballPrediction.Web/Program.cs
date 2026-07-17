using FootballPrediction.Application.Interfaces;
using FootballPrediction.Application.Services;
using FootballPrediction.ML.Prediction;
using FootballPrediction.Web.Models;
using FootballPrediction.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddControllers(); // API controllers

// Configuration
builder.Services.Configure<ModelSettings>(
    builder.Configuration.GetSection(ModelSettings.SectionName));

// Services
builder.Services.AddSingleton(sp =>
{
    var settings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ModelSettings>>().Value;
    var predictor = new MatchPredictor();
    var modelPath = Path.Combine(Directory.GetCurrentDirectory(), settings.BinaryModelPath);
    if (File.Exists(modelPath))
        predictor.LoadModel(modelPath);
    return predictor;
});
builder.Services.AddTransient<ICsvParser, CsvParserService>();
builder.Services.AddSingleton<IPredictionService, PredictionService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllers(); // API attribute routing

app.Run();
