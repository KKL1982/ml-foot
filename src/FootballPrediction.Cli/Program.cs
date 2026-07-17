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
        var trainerOpt = new Option<string>("--trainer", () => "sdca", "Trainer: sdca, lbfgs, lightgbm, or all");
        trainCommand.AddOption(trainInputOpt);
        trainCommand.AddOption(trainOutputOpt);
        trainCommand.AddOption(trainerOpt);
        trainCommand.SetHandler(TrainAsync, trainInputOpt, trainOutputOpt, trainerOpt);

        var predictCommand = new Command("predict", "Predict match results from CSV input");
        var predictModelOpt = new Option<string>("--model", "Path to trained model (.zip)") { IsRequired = true };
        var predictInputOpt = new Option<string>("--input", "Path to CSV file with matches to predict") { IsRequired = true };
        var predictOutputOpt = new Option<string>("--output", "Path to output CSV with predictions") { IsRequired = true };
        predictCommand.AddOption(predictModelOpt);
        predictCommand.AddOption(predictInputOpt);
        predictCommand.AddOption(predictOutputOpt);
        predictCommand.SetHandler(PredictAsync, predictModelOpt, predictInputOpt, predictOutputOpt);

        var compareCommand = new Command("compare", "Train all models and compare performance");
        var compareInputOpt = new Option<string>("--input", "Path to training CSV file") { IsRequired = true };
        compareCommand.AddOption(compareInputOpt);
        compareCommand.SetHandler(CompareAsync, compareInputOpt);

        var tuneCommand = new Command("tune", "Grid search LightGbm hyperparameters");
        var tuneInputOpt = new Option<string>("--input", "Path to training CSV file") { IsRequired = true };
        var tuneOutputOpt = new Option<string>("--output", () => "models/model.zip", "Path to save best model");
        tuneCommand.AddOption(tuneInputOpt);
        tuneCommand.AddOption(tuneOutputOpt);
        tuneCommand.SetHandler(TuneAsync, tuneInputOpt, tuneOutputOpt);

        var root = new RootCommand("FootballPrediction — 1X2 match prediction tool");
        root.AddCommand(trainCommand);
        root.AddCommand(predictCommand);
        root.AddCommand(compareCommand);
        root.AddCommand(tuneCommand);

        return await root.InvokeAsync(args);
    }

    static async Task TrainAsync(string input, string output, string trainer)
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

        var features = FeatureEngineer.BuildFeatures(matches);
        Console.WriteLine($"Built features for {features.Count} matches.");

        var trainerKind = trainer.ToLowerInvariant() switch
        {
            "sdca" => ModelTrainer.TrainerKind.SdcaMaximumEntropy,
            "lbfgs" => ModelTrainer.TrainerKind.LbfgsMaximumEntropy,
            "lightgbm" => ModelTrainer.TrainerKind.LightGbm,
            _ => ModelTrainer.TrainerKind.SdcaMaximumEntropy
        };

        var mlTrainer = new ModelTrainer();
        var result = mlTrainer.TrainAndEvaluate(features, trainerKind, trainFraction: 0.8);

        if (result.Metrics != null)
        {
            Console.WriteLine(ModelEvaluator.FormatMetrics(result.Metrics));
            Console.WriteLine(ModelEvaluator.FormatConfusionMatrix(result.Metrics));
        }
        else
        {
            Console.WriteLine("Not enough data for evaluation (< 10 test matches). Model trained on all data.");
        }

        var schema = mlTrainer.GetDataView(features);
        mlTrainer.SaveModel(result.Model, schema, output);
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

    static async Task CompareAsync(string input)
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

        if (matches.Count < 20)
        {
            Console.Error.WriteLine("Error: Need at least 20 matches for comparison.");
            return;
        }

        var features = FeatureEngineer.BuildFeatures(matches);
        Console.WriteLine($"Built features for {features.Count} matches.");

        var mlTrainer = new ModelTrainer();
        var results = mlTrainer.TrainAllAndCompare(features, trainFraction: 0.8);

        Console.WriteLine(ModelEvaluator.FormatComparison(results));

        foreach (var r in results)
        {
            if (r.Metrics != null)
            {
                Console.WriteLine($"\n--- {r.Trainer} ---");
                Console.WriteLine(ModelEvaluator.FormatMetrics(r.Metrics));
                Console.WriteLine(ModelEvaluator.FormatConfusionMatrix(r.Metrics));
            }
        }
    }

    static async Task TuneAsync(string input, string output)
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

        if (matches.Count < 20)
        {
            Console.Error.WriteLine("Error: Need at least 20 matches.");
            return;
        }

        var features = FeatureEngineer.BuildFeatures(matches);
        Console.WriteLine($"Built features for {features.Count} matches.");

        var mlTrainer = new ModelTrainer();

        Console.WriteLine("\n=== LightGbm Grid Search ===\n");
        var results = mlTrainer.TuneLightGbm(features, trainFraction: 0.8);

        Console.WriteLine($"\n=== Top 5 Results ===\n");
        Console.WriteLine($"{"Rank",-5} {"Accuracy",-12} {"LogLoss",-10} {"HyperParams",-40}");
        Console.WriteLine(new string('-', 70));

        for (int i = 0; i < Math.Min(5, results.Count); i++)
        {
            var r = results[i];
            Console.WriteLine($"{i + 1,-5} {r.Accuracy:P2,-12} {r.LogLoss:F4,-10} {r.HyperParams,-40}");
        }

        // Save best model
        var best = results[0];
        var schema = mlTrainer.GetDataView(features);
        mlTrainer.SaveModel(best.Model, schema, output);
        Console.WriteLine($"\nBest model saved to: {output}");
        Console.WriteLine($"Best params: {best.HyperParams}");
        Console.WriteLine($"Best accuracy: {best.Accuracy:P2}");
    }

    static string Escape(string value) => value.Contains(',') ? $"\"{value}\"" : value;
}
