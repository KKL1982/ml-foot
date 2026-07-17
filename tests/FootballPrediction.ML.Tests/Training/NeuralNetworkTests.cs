using FluentAssertions;
using FootballPrediction.ML.Training;
using System.IO;
using Xunit;

namespace FootballPrediction.ML.Tests.Training;

public class NeuralNetworkTests
{
    private NeuralNetwork CreateSimpleNN(int? seed = 42)
    {
        return new NeuralNetwork(new[] { 4 }, new[] { 2 }, numFeatures: 2, hidden1: 8, hidden2: 4, outputSize: 3, seed: seed);
    }

    private NeuralNetwork CreateBinaryNN(int? seed = 42)
    {
        return new NeuralNetwork(new[] { 4 }, new[] { 2 }, numFeatures: 2, hidden1: 8, hidden2: 4, outputSize: 1, seed: seed);
    }

    [Fact]
    public void Constructor_ShouldInitializeWeights()
    {
        var nn = CreateSimpleNN();
        nn.Hidden1.Should().Be(8);
        nn.Hidden2.Should().Be(4);
        nn.OutputSize.Should().Be(3);
        nn.NumFeatures.Should().Be(2);
    }

    [Fact]
    public void Forward_3Class_ShouldReturn3ProbabilitiesThatSumToOne()
    {
        var nn = CreateSimpleNN();
        var (probs, _, _) = nn.Forward(new[] { 0 }, new[] { 0.5f, 0.3f });
        probs.Should().HaveCount(3);
        (probs[0] + probs[1] + probs[2]).Should().BeApproximately(1.0f, 0.001f);
    }

    [Fact]
    public void Forward_Binary_ShouldReturnSigmoidProbability()
    {
        var nn = CreateBinaryNN();
        var (probs, _, _) = nn.Forward(new[] { 2 }, new[] { 0.5f, 0.3f });
        probs.Should().HaveCount(1);
        probs[0].Should().BeInRange(0, 1);
    }

    [Fact]
    public void TrainStep_3Class_ShouldReduceLoss()
    {
        var nn = CreateSimpleNN(seed: 42);
        var losses = new List<float>();
        for (int epoch = 0; epoch < 100; epoch++)
        {
            float l = 0;
            l += nn.TrainStep(new[] { 0 }, new[] { 0.1f, 0.1f }, 0, learningRate: 0.01f);
            l += nn.TrainStep(new[] { 1 }, new[] { 0.5f, 0.5f }, 1, learningRate: 0.01f);
            l += nn.TrainStep(new[] { 2 }, new[] { 0.5f, 0.5f }, 1, learningRate: 0.01f);
            l += nn.TrainStep(new[] { 3 }, new[] { 0.9f, 0.9f }, 2, learningRate: 0.01f);
            losses.Add(l / 4);
        }
        losses[0].Should().BeGreaterThan(losses[^1]);
    }

    [Fact]
    public void TrainStep_Binary_ShouldLearn()
    {
        var nn = CreateBinaryNN(seed: 42);
        // Train cat 0 → class 0 (away), cat 3 → class 1 (home)
        for (int epoch = 0; epoch < 200; epoch++)
        {
            nn.TrainStep(new[] { 0 }, new[] { 0.1f, 0.1f }, 0, learningRate: 0.02f);
            nn.TrainStep(new[] { 3 }, new[] { 0.9f, 0.9f }, 1, learningRate: 0.02f);
        }
        var (probs0, _, _) = nn.Forward(new[] { 0 }, new[] { 0.1f, 0.1f });
        var (probs3, _, _) = nn.Forward(new[] { 3 }, new[] { 0.9f, 0.9f });
        probs0[0].Should().BeLessThan(0.5f); // away
        probs3[0].Should().BeGreaterThan(0.5f); // home
    }

    [Fact]
    public void SaveAndLoad_3Class_ShouldPreservePredictions()
    {
        var nn = CreateSimpleNN(seed: 42);
        var (before, _, _) = nn.Forward(new[] { 1 }, new[] { 0.4f, 0.6f });

        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) { nn.Save(w); }
        ms.Position = 0;
        var loaded = NeuralNetwork.Load(new BinaryReader(ms, System.Text.Encoding.UTF8, true));
        var (after, _, _) = loaded.Forward(new[] { 1 }, new[] { 0.4f, 0.6f });

        after[0].Should().BeApproximately(before[0], 0.0001f);
        after[1].Should().BeApproximately(before[1], 0.0001f);
        after[2].Should().BeApproximately(before[2], 0.0001f);
    }

    [Fact]
    public void SaveAndLoad_Binary_ShouldPreservePredictions()
    {
        var nn = CreateBinaryNN(seed: 42);
        var (before, _, _) = nn.Forward(new[] { 2 }, new[] { 0.7f, 0.3f });

        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) { nn.Save(w); }
        ms.Position = 0;
        var loaded = NeuralNetwork.Load(new BinaryReader(ms, System.Text.Encoding.UTF8, true));
        var (after, _, _) = loaded.Forward(new[] { 2 }, new[] { 0.7f, 0.3f });

        after[0].Should().BeApproximately(before[0], 0.0001f);
    }

    [Fact]
    public void Deterministic_WithSameSeed_ShouldBeIdentical()
    {
        var n1 = CreateSimpleNN(42);
        var n2 = CreateSimpleNN(42);
        var (p1, _, _) = n1.Forward(new[] { 2 }, new[] { 0.3f, 0.7f });
        var (p2, _, _) = n2.Forward(new[] { 2 }, new[] { 0.3f, 0.7f });
        p1[0].Should().Be(p2[0]);
        p1[1].Should().Be(p2[1]);
        p1[2].Should().Be(p2[2]);
    }

    [Fact]
    public void FullArchitecture_ShouldWorkWithFootballDimensions()
    {
        var nn = new NeuralNetwork(new[] { 4, 10, 126, 126, 300, 300 }, new[] { 2, 4, 16, 16, 16, 16 }, numFeatures: 15, hidden1: 64, hidden2: 32, outputSize: 3, seed: 42);
        var cat = new[] { 0, 5, 42, 100, 7, 150 };
        var num = new float[] { 0.45f, 0.30f, 0.25f, 0.40f, 0.33f, 0.27f, 0.5f, 0.3f, 1.2f, 0.8f, 1.0f, 1.5f, 0.2f, 365f, 180f };
        var (probs, _, _) = nn.Forward(cat, num);
        probs.Should().HaveCount(3);
        (probs[0] + probs[1] + probs[2]).Should().BeApproximately(1.0f, 0.001f);
        for (int i = 0; i < 10; i++) nn.TrainStep(cat, num, 1, learningRate: 0.001f);
    }

    [Fact]
    public void FullArchitecture_Binary_ShouldWork()
    {
        var nn = new NeuralNetwork(new[] { 4, 10, 126, 126, 300, 300 }, new[] { 2, 4, 16, 16, 16, 16 }, numFeatures: 15, hidden1: 64, hidden2: 32, outputSize: 1, seed: 42);
        var cat = new[] { 0, 5, 42, 100, 7, 150 };
        var num = new float[] { 0.45f, 0.30f, 0.25f, 0.40f, 0.33f, 0.27f, 0.5f, 0.3f, 1.2f, 0.8f, 1.0f, 1.5f, 0.2f, 365f, 180f };
        var (probs, _, _) = nn.Forward(cat, num);
        probs.Should().HaveCount(1);
        probs[0].Should().BeInRange(0, 1);
        for (int i = 0; i < 10; i++) nn.TrainStep(cat, num, 1, learningRate: 0.001f);
    }

    [Fact]
    public void Save_ShouldBeReadable()
    {
        var nn = CreateSimpleNN(42);
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) { nn.Save(w); }
        ms.Length.Should().BeGreaterThan(100);
    }
}
