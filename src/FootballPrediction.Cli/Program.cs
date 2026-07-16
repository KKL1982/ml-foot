using System.CommandLine;
using FootballPrediction.Application.Services;
using FootballPrediction.ML.FeatureEngineering;
using FootballPrediction.ML.ModelEvaluation;
using FootballPrediction.ML.Prediction;
using FootballPrediction.ML.Training;

namespace FootballPrediction.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var trainCommand = new Command("train", "Train a 1X2 prediction model from CSV match data");
        var trainInputOpt = new Option<string>("--input", "Path to training CSV file") { IsRequired = true };
        var trainOutputOpt = new Option<string>("--output", "Path to save trained model (.zip)") { IsRequired = true };
        trainCommand.AddOption(trainInputOpt);
        trainCommand.AddOption(trainOutputOpt);
        trainCommand.SetHandler(TrainAsync, trainInputOpt, trainOutputOpt);

        var predictCommand = new Command("predict", "Predict match results from CSV input");
        var predictModelOpt = new Option<string>("--model", "Path to trained model (.zip)") { IsRequired = true };
        var predictInputOpt = new Option<string>("--input", "Path to CSV file with matches to predict") { IsRequired = true };
        var predictOutputOpt = new Option<string>("--output", "Path to output CSV with predictions") { IsRequired = true };
        predictCommand.AddOption(predictModelOpt);
        predictCommand.AddOption(predictInputOpt);
        predictCommand.AddOption(predictOutputOpt);
        predictCommand.SetHandler(PredictAsync, predictModelOpt, predictInputOpt, predictOutputOpt);

        var root = new RootCommand("FootballPrediction — 1X2 match prediction tool");
        root.AddCommand(trainCommand);
        root.AddCommand(predictCommand);

        return await root.InvokeAsync(args);
    }

    static async Task TrainAsync(string input, string output)
    {
        Console.WriteLine($"Loading matches from: {input}");

        if (!File.Exists(input))
        {
            Console.Error.WriteLine($"Error: File not found: {input}");
            return;
        }

        var parser = new CsvParserService();
        var matches = await parser.ParseMatchesAsync(input);
        Console.WriteLine($"Parsed {matches.Count} matches.");

        if (matches.Count == 0)
        {
            Console.Error.WriteLine("Error: No valid matches found in CSV.");
            return;
        }

        // Build features (chronological order preserved)
        var features = FeatureEngineer.BuildFeatures(matches);
        Console.WriteLine($"Built features for {features.Count} matches.");

        // Train model
        var trainer = new ModelTrainer();
        var (model, metrics) = trainer.TrainAndEvaluate(features, testFraction: 0.2f);

        // Print metrics
        if (metrics != null)
        {
            Console.WriteLine(ModelEvaluator.FormatMetrics(metrics));
            Console.WriteLine(ModelEvaluator.FormatConfusionMatrix(metrics));
        }
        else
        {
            Console.WriteLine("Not enough data for evaluation (< 10 matches). Model trained on all data.");
        }

        // Save model
        var schema = trainer.GetDataView(features);
        trainer.SaveModel(model, schema, output);
        Console.WriteLine($"Model saved to: {output}");
    }

    static async Task PredictAsync(string model, string input, string output)
    {
        Console.WriteLine($"Loading model from: {model}");

        if (!File.Exists(model))
        {
            Console.Error.WriteLine($"Error: Model file not found: {model}");
            return;
        }

        var predictor = new MatchPredictor();
        predictor.LoadModel(model);

        Console.WriteLine($"Loading matches from: {input}");

        if (!File.Exists(input))
        {
            Console.Error.WriteLine($"Error: File not found: {input}");
            return;
        }

        var parser = new CsvParserService();
        var matches = await parser.ParseMatchesAsync(input);
        Console.WriteLine($"Parsed {matches.Count} matches.");

        var features = FeatureEngineer.BuildFeatures(matches);
        if (features.Count == 0)
        {
            Console.Error.WriteLine("Error: No valid matches found.");
            return;
        }

        var predictions = predictor.Predict(features);
        Console.WriteLine($"Generated {predictions.Count} predictions.");

        // Write output CSV
        await using var writer = new StreamWriter(output);
        await writer.WriteLineAsync("Date,League,HomeTeam,AwayTeam,PredictedResult,Probability1,ProbabilityX,Probability2,Confidence,Comment");

        for (int i = 0; i < features.Count; i++)
        {
            var match = matches[i];
            var pred = predictions[i];

            double maxProb = Math.Max(Math.Max(pred.Probability1, pred.ProbabilityX), pred.Probability2);
            string comment = maxProb switch
            {
                var p when p == pred.Probability1 && p > 0.5 => "Home win likely",
                var p when p == pred.Probability2 && p > 0.5 => "Away win likely",
                var p when p == pred.ProbabilityX && p > 0.4 => "Draw likely",
                _ => "Uncertain"
            };

            await writer.WriteLineAsync(
                $"{match.Date:yyyy-MM-dd},{Escape(match.League)},{Escape(match.HomeTeam)},{Escape(match.AwayTeam)}," +
                $"{pred.PredictedResult},{pred.Probability1:F3},{pred.ProbabilityX:F3},{pred.Probability2:F3},{maxProb:F3},{comment}");
        }

        Console.WriteLine($"Predictions saved to: {output}");
    }

    static string Escape(string value) => value.Contains(',') ? $"\"{value}\"" : value;
}
