using FootballPrediction.ML.DataModels;
using Microsoft.ML.Data;

namespace FootballPrediction.ML.Training;

/// <summary>
/// Encodes MatchData → NeuralNetwork inputs, trains the model, and evaluates.
/// Supports both multiclass (1/X/2) and binary (HomeWin/AwayWin) modes.
/// </summary>
public class NeuralNetworkTrainer
{
    private readonly Dictionary<string, int> _leagueIdx = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _seasonIdx = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _teamIdx = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _coachIdx = new(StringComparer.OrdinalIgnoreCase);

    private readonly List<string> _leagues = new();
    private readonly List<string> _seasons = new();
    private readonly List<string> _teams = new();
    private readonly List<string> _coaches = new();

    private NeuralNetwork? _network;
    private bool _binaryMode;

    public record NnTrainingResult(
        double Accuracy, double LogLoss, int Epochs, double LearningRate,
        MulticlassClassificationMetrics? Metrics = null);

    public NnTrainingResult Train(
        List<MatchData> trainData,
        List<MatchData> testData,
        int epochs = 30,
        double learningRate = 0.001,
        int batchSize = 64,
        bool binaryMode = false)
    {
        _binaryMode = binaryMode;
        int outputSize = binaryMode ? 1 : 3;

        var allData = new List<MatchData>(trainData);
        allData.AddRange(testData);
        FitEncoders(allData);

        _network = new NeuralNetwork(
            categoryCounts: new[] { _leagues.Count, _seasons.Count, _teams.Count, _teams.Count, _coaches.Count, _coaches.Count },
            embeddingDims: new[] { 2, 4, 16, 16, 16, 16 },
            numFeatures: 15,
            hidden1: 64,
            hidden2: 32,
            outputSize: outputSize,
            seed: 42);

        var (trainXCat, trainXNum, trainY) = Encode(trainData);
        var (testXCat, testXNum, testY) = Encode(testData);

        int n = trainData.Count;
        float bestAcc = 0;
        double bestLogLoss = double.MaxValue;

        for (int epoch = 0; epoch < epochs; epoch++)
        {
            float epochLoss = 0;
            var indices = Enumerable.Range(0, n).OrderBy(_ => Random.Shared.Next()).ToArray();

            for (int i = 0; i < n; i += batchSize)
            {
                int end = Math.Min(i + batchSize, n);
                for (int j = i; j < end; j++)
                {
                    int idx = indices[j];
                    epochLoss += _network.TrainStep(
                        trainXCat[idx], trainXNum[idx], trainY[idx],
                        learningRate: (float)learningRate);
                }
            }

            if (testData.Count >= 10)
            {
                var evalResult = Evaluate(testXCat, testXNum, testY);
                if (evalResult.Accuracy >= bestAcc)
                {
                    bestAcc = evalResult.Accuracy;
                    bestLogLoss = evalResult.LogLoss;
                }
            }

            if ((epoch + 1) % 5 == 0 || epoch == 0)
                Console.WriteLine($"  Epoch {epoch + 1}/{epochs}: loss={epochLoss / n:F4}, bestAcc={bestAcc:P2}");
        }

        var finalEval = Evaluate(testXCat, testXNum, testY);
        return new NnTrainingResult(finalEval.Accuracy, finalEval.LogLoss, epochs, learningRate);
    }

    public IReadOnlyList<MatchPrediction> Predict(IReadOnlyList<MatchData> matches)
    {
        if (_network == null)
            throw new InvalidOperationException("Network not trained.");

        var results = new List<MatchPrediction>(matches.Count);
        var (catIndices, numFeatures, _) = Encode(matches.ToList());

        for (int i = 0; i < matches.Count; i++)
        {
            var (probs, _, _) = _network.Forward(catIndices[i], numFeatures[i]);
            MatchPrediction pred;

            if (_binaryMode)
            {
                float homeProb = probs[0];
                pred = new MatchPrediction
                {
                    PredictedResult = homeProb >= 0.5f ? "1" : "2",
                    Probability1 = homeProb,
                    Probability2 = 1 - homeProb,
                    HomeWinProbability = homeProb
                };
            }
            else
            {
                int bestClass = probs[0] >= probs[1] && probs[0] >= probs[2] ? 0 :
                                probs[1] >= probs[2] ? 1 : 2;
                string result = bestClass switch { 0 => "1", 1 => "X", _ => "2" };
                pred = new MatchPrediction
                {
                    PredictedResult = result,
                    Probability1 = probs[0],
                    ProbabilityX = probs[1],
                    Probability2 = probs[2]
                };
            }

            results.Add(pred);
        }

        return results;
    }

    private (float Accuracy, double LogLoss) Evaluate(
        int[][] catIndices, float[][] numFeatures, int[] labels)
    {
        if (_network == null) throw new InvalidOperationException("Network not trained.");

        int correct = 0;
        double totalLogLoss = 0;
        int n = labels.Length;

        for (int i = 0; i < n; i++)
        {
            var (probs, _, _) = _network.Forward(catIndices[i], numFeatures[i]);
            int predicted;

            if (_binaryMode)
            {
                predicted = probs[0] >= 0.5f ? 1 : 0;
                float p = Math.Clamp(probs[0], 1e-7f, 1 - 1e-7f);
                totalLogLoss += -(labels[i] * Math.Log(p) + (1 - labels[i]) * Math.Log(1 - p));
            }
            else
            {
                predicted = probs[0] >= probs[1] && probs[0] >= probs[2] ? 0 :
                            probs[1] >= probs[2] ? 1 : 2;
                totalLogLoss += -Math.Log(Math.Max(probs[labels[i]], 1e-7f));
            }

            if (predicted == labels[i]) correct++;
        }

        return ((float)correct / n, totalLogLoss / n);
    }

    private void FitEncoders(List<MatchData> data)
    {
        foreach (var m in data)
        {
            AddOrGet(_leagueIdx, _leagues, m.League);
            AddOrGet(_seasonIdx, _seasons, m.Season);
            AddOrGet(_teamIdx, _teams, m.HomeTeam);
            AddOrGet(_teamIdx, _teams, m.AwayTeam);
            AddOrGet(_coachIdx, _coaches, m.HomeCoach);
            AddOrGet(_coachIdx, _coaches, m.AwayCoach);
        }
    }

    private static int AddOrGet(Dictionary<string, int> dict, List<string> list, string key)
    {
        if (dict.TryGetValue(key, out int idx)) return idx;
        idx = list.Count;
        list.Add(key);
        dict[key] = idx;
        return idx;
    }

    private (int[][] cat, float[][] num, int[] labels) Encode(List<MatchData> data)
    {
        int n = data.Count;
        var cat = new int[n][];
        var num = new float[n][];
        var labels = new int[n];

        for (int i = 0; i < n; i++)
        {
            var m = data[i];
            cat[i] = new[]
            {
                _leagueIdx.GetValueOrDefault(m.League, 0),
                _seasonIdx.GetValueOrDefault(m.Season, 0),
                _teamIdx.GetValueOrDefault(m.HomeTeam, 0),
                _teamIdx.GetValueOrDefault(m.AwayTeam, 0),
                _coachIdx.GetValueOrDefault(m.HomeCoach, 0),
                _coachIdx.GetValueOrDefault(m.AwayCoach, 0),
            };

            num[i] = new float[]
            {
                m.Bet365HomeProb, m.Bet365DrawProb, m.Bet365AwayProb,
                m.PinnacleHomeProb, m.PinnacleDrawProb, m.PinnacleAwayProb,
                m.HomeForm5, m.AwayForm5,
                m.HomeGoalsForAvg, m.AwayGoalsForAvg,
                m.HomeGoalsAgainstAvg, m.AwayGoalsAgainstAvg,
                m.FormDiff,
                m.HomeCoachTenure, m.AwayCoachTenure
            };

            labels[i] = _binaryMode
                ? (m.Label == "1" ? 1 : 0)
                : m.Label switch { "1" => 0, "X" => 1, "2" => 2, _ => 0 };
        }

        return (cat, num, labels);
    }

    public void Save(string path)
    {
        if (_network == null)
            throw new InvalidOperationException("Network not trained.");

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);

        writer.Write(_binaryMode);
        _network.Save(writer);

        SaveVocab(writer, _leagues);
        SaveVocab(writer, _seasons);
        SaveVocab(writer, _teams);
        SaveVocab(writer, _coaches);
    }

    public static NeuralNetworkTrainer Load(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        var binaryMode = reader.ReadBoolean();
        var network = NeuralNetwork.Load(reader);

        var trainer = new NeuralNetworkTrainer { _network = network, _binaryMode = binaryMode };

        trainer._leagues.AddRange(LoadVocab(reader));
        trainer._seasons.AddRange(LoadVocab(reader));
        trainer._teams.AddRange(LoadVocab(reader));
        trainer._coaches.AddRange(LoadVocab(reader));

        for (int i = 0; i < trainer._leagues.Count; i++) trainer._leagueIdx[trainer._leagues[i]] = i;
        for (int i = 0; i < trainer._seasons.Count; i++) trainer._seasonIdx[trainer._seasons[i]] = i;
        for (int i = 0; i < trainer._teams.Count; i++) trainer._teamIdx[trainer._teams[i]] = i;
        for (int i = 0; i < trainer._coaches.Count; i++) trainer._coachIdx[trainer._coaches[i]] = i;

        return trainer;
    }

    private static void SaveVocab(BinaryWriter writer, List<string> vocab)
    {
        writer.Write(vocab.Count);
        foreach (var item in vocab) writer.Write(item);
    }

    private static List<string> LoadVocab(BinaryReader reader)
    {
        int count = reader.ReadInt32();
        var list = new List<string>(count);
        for (int i = 0; i < count; i++) list.Add(reader.ReadString());
        return list;
    }

    public NeuralNetwork Network => _network ?? throw new InvalidOperationException("Network not trained.");
    public bool IsBinary => _binaryMode;
}
