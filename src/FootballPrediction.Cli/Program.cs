using System.CommandLine;
using FootballPrediction.Application.Services;
using FootballPrediction.ML.FeatureEngineering;
using FootballPrediction.ML.ModelEvaluation;
using FootballPrediction.ML.Prediction;
using FootballPrediction.ML.Training;
using Microsoft.ML.Data;

namespace FootballPrediction.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var binaryOpt = new Option<bool>("--binary", () => false, "Train binary model (Home/Away only, excludes draws)");

        var trainCommand = new Command("train", "Train a 1X2 prediction model from CSV match data");
        var trainInputOpt = new Option<string>("--input", "Path to training CSV file") { IsRequired = true };
        var trainOutputOpt = new Option<string>("--output", "Path to save trained model") { IsRequired = true };
        var trainerOpt = new Option<string>("--trainer", () => "sdca", "Trainer: sdca, lbfgs, lightgbm, nn, or all");
        trainCommand.AddOption(trainInputOpt);
        trainCommand.AddOption(trainOutputOpt);
        trainCommand.AddOption(trainerOpt);
        trainCommand.AddOption(binaryOpt);
        trainCommand.SetHandler(TrainAsync, trainInputOpt, trainOutputOpt, trainerOpt, binaryOpt);

        var predictCommand = new Command("predict", "Predict match results from CSV input");
        var predictModelOpt = new Option<string>("--model", "Path to trained model") { IsRequired = true };
        var predictInputOpt = new Option<string>("--input", "Path to CSV file with matches to predict") { IsRequired = true };
        var predictOutputOpt = new Option<string>("--output", "Path to output CSV with predictions") { IsRequired = true };
        var thresholdOpt = new Option<double>("--threshold", () => 0.5, "Confidence threshold for binary betting (0.5-1.0)");
        predictCommand.AddOption(predictModelOpt);
        predictCommand.AddOption(predictInputOpt);
        predictCommand.AddOption(predictOutputOpt);
        predictCommand.AddOption(thresholdOpt);
        predictCommand.AddOption(binaryOpt);
        predictCommand.SetHandler(PredictAsync, predictModelOpt, predictInputOpt, predictOutputOpt, thresholdOpt, binaryOpt);

        var compareCommand = new Command("compare", "Train all models and compare performance");
        var compareInputOpt = new Option<string>("--input", "Path to training CSV file") { IsRequired = true };
        compareCommand.AddOption(compareInputOpt);
        compareCommand.AddOption(binaryOpt);
        compareCommand.SetHandler(CompareAsync, compareInputOpt, binaryOpt);

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

    static async Task TrainAsync(string input, string output, string trainer, bool binary)
    {
        Console.WriteLine($"Loading matches from: {input} (binary={binary})");
        if (!File.Exists(input)) { Console.Error.WriteLine($"Error: File not found: {input}"); return; }

        var parser = new CsvParserService();
        var matches = await parser.ParseMatchesAsync(input);
        var features = FeatureEngineer.BuildFeatures(matches, binaryMode: binary);
        Console.WriteLine($"Parsed {matches.Count} matches → {features.Count} features.");

        if (features.Count == 0) { Console.Error.WriteLine("Error: No valid matches found."); return; }

        var trainerKind = ResolveTrainer(trainer, binary);
        var mlTrainer = new ModelTrainer();
        var result = mlTrainer.TrainAndEvaluate(features, trainerKind, trainFraction: 0.8, binaryMode: binary);

        if (result.Trainer == ModelTrainer.TrainerKind.NeuralNetwork)
        {
            Console.WriteLine();
            Console.WriteLine("========== Neural Network Results ==========");
            Console.WriteLine($"Accuracy:        {result.Accuracy:P2}");
            Console.WriteLine($"Log Loss:        {result.LogLoss:F4}");
            Console.WriteLine($"Hyperparams:     {result.HyperParams}");
            if (result.F1Score.HasValue) Console.WriteLine($"F1 Score:        {result.F1Score:F4}");
        }
        else if (result.BinaryMetrics != null)
        {
            Console.WriteLine(ModelEvaluator.FormatBinaryMetrics(result.BinaryMetrics));
        }
        else if (result.Metrics != null)
        {
            Console.WriteLine(ModelEvaluator.FormatMetrics(result.Metrics));
            Console.WriteLine(ModelEvaluator.FormatConfusionMatrix(result.Metrics));
        }

        var schema = mlTrainer.GetDataView(features);
        mlTrainer.SaveModel(result.Model!, schema, output);
        Console.WriteLine($"Model saved to: {output}");
    }

    static async Task PredictAsync(string model, string input, string output, double threshold, bool binary)
    {
        if (!File.Exists(model)) { Console.Error.WriteLine($"Error: Model file not found: {model}"); return; }
        if (!File.Exists(input)) { Console.Error.WriteLine($"Error: File not found: {input}"); return; }

        Console.WriteLine($"Loading model from: {model}");
        var parser = new CsvParserService();
        var matches = await parser.ParseMatchesAsync(input);
        var features = FeatureEngineer.BuildFeatures(matches, binaryMode: binary);
        Console.WriteLine($"Parsed {matches.Count} matches → {features.Count} features.");

        if (features.Count == 0) { Console.Error.WriteLine("Error: No valid matches found."); return; }

        IReadOnlyList<FootballPrediction.ML.DataModels.MatchPrediction> predictions;
        if (model.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
        {
            var nnPredictor = NeuralNetworkTrainer.Load(model);
            predictions = nnPredictor.Predict(features);
        }
        else
        {
            var predictor = new MatchPredictor();
            predictor.LoadModel(model);
            var rawPredictions = predictor.Predict(features);
            // In binary mode, PredictedResult comes from HomeWinProbability
            if (binary)
            {
                var fixedPreds = new List<FootballPrediction.ML.DataModels.MatchPrediction>();
                foreach (var p in rawPredictions)
                {
                    p.PredictedResult = p.HomeWinProbability >= 0.5f ? "1" : "2";
                    p.Probability1 = p.HomeWinProbability;
                    p.Probability2 = 1 - p.HomeWinProbability;
                    fixedPreds.Add(p);
                }
                predictions = fixedPreds;
            }
            else
                predictions = rawPredictions;
        }
        Console.WriteLine($"Generated {predictions.Count} predictions.");

        await using var writer = new StreamWriter(output);

        if (binary)
        {
            await writer.WriteLineAsync("Date,League,HomeTeam,AwayTeam,PredictedResult,HomeWinProbability,Confidence,Bet,Comment");
            for (int i = 0; i < features.Count; i++)
            {
                var match = matches[i];
                var pred = predictions[i];
                float prob = pred.HomeWinProbability;
                float confidence = Math.Max(prob, 1 - prob);
                string bet = confidence >= (float)threshold ? (prob >= 0.5f ? "HOME" : "AWAY") : "SKIP";
                string comment = bet switch
                {
                    "HOME" => $"Strong home signal ({prob:P0})",
                    "AWAY" => $"Strong away signal ({1 - prob:P0})",
                    _ => "Below threshold"
                };
                await writer.WriteLineAsync(
                    $"{match.Date:yyyy-MM-dd},{Escape(match.League)},{Escape(match.HomeTeam)},{Escape(match.AwayTeam)}," +
                    $"{pred.PredictedResult},{prob:F3},{confidence:F3},{bet},{comment}");
            }
        }
        else
        {
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
        }

        Console.WriteLine($"Predictions saved to: {output}");
    }

    static async Task CompareAsync(string input, bool binary)
    {
        Console.WriteLine($"Loading matches from: {input} (binary={binary})");
        if (!File.Exists(input)) { Console.Error.WriteLine($"Error: File not found: {input}"); return; }

        var parser = new CsvParserService();
        var matches = await parser.ParseMatchesAsync(input);
        var features = FeatureEngineer.BuildFeatures(matches, binaryMode: binary);
        Console.WriteLine($"Parsed {matches.Count} matches → {features.Count} features.");

        if (features.Count < 20) { Console.Error.WriteLine("Error: Need at least 20 matches."); return; }

        var mlTrainer = new ModelTrainer();
        var results = mlTrainer.TrainAllAndCompare(features, trainFraction: 0.8, binaryMode: binary);
        Console.WriteLine(ModelEvaluator.FormatComparison(results));

        foreach (var r in results)
        {
            if (r.BinaryMetrics != null)
                Console.WriteLine($"\n--- {r.Trainer} ---\n{ModelEvaluator.FormatBinaryMetrics(r.BinaryMetrics)}");
            else if (r.Metrics != null)
                Console.WriteLine($"\n--- {r.Trainer} ---\n{ModelEvaluator.FormatMetrics(r.Metrics)}\n{ModelEvaluator.FormatConfusionMatrix(r.Metrics)}");
        }
    }

    static async Task TuneAsync(string input, string output)
    {
        Console.WriteLine($"Loading matches from: {input}");
        if (!File.Exists(input)) { Console.Error.WriteLine($"Error: File not found: {input}"); return; }

        var parser = new CsvParserService();
        var matches = await parser.ParseMatchesAsync(input);
        if (matches.Count < 20) { Console.Error.WriteLine("Error: Need at least 20 matches."); return; }

        var features = FeatureEngineer.BuildFeatures(matches);
        Console.WriteLine($"Built features for {features.Count} matches.");
        var mlTrainer = new ModelTrainer();
        Console.WriteLine("Running LightGbm grid search...");
        var results = mlTrainer.TuneLightGbm(features);
        var best = results[0];
        Console.WriteLine($"\nBest: {best.HyperParams} => Accuracy={best.Accuracy:P2} LogLoss={best.LogLoss:F4}");
        var schema = mlTrainer.GetDataView(features);
        mlTrainer.SaveModel(best.Model!, schema, output);
        Console.WriteLine($"Best model saved to: {output}");
    }

    static ModelTrainer.TrainerKind ResolveTrainer(string trainer, bool binary) => trainer.ToLowerInvariant() switch
    {
        "sdca" => binary ? ModelTrainer.TrainerKind.SdcaLogisticRegression : ModelTrainer.TrainerKind.SdcaMaximumEntropy,
        "lbfgs" => binary ? ModelTrainer.TrainerKind.LbfgsLogisticRegression : ModelTrainer.TrainerKind.LbfgsMaximumEntropy,
        "lightgbm" => binary ? ModelTrainer.TrainerKind.LightGbmBinary : ModelTrainer.TrainerKind.LightGbm,
        "nn" or "neuralnetwork" => ModelTrainer.TrainerKind.NeuralNetwork,
        _ => binary ? ModelTrainer.TrainerKind.SdcaLogisticRegression : ModelTrainer.TrainerKind.SdcaMaximumEntropy
    };

    static string Escape(string value) => value.Contains(',') ? $"\"{value}\"" : value;
}
